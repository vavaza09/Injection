using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Designer-facing GUI for adding new <c>SoundType</c>/<c>MusicType</c> values without hand-editing C#.
/// Drop in a pile of AudioClips, adjust names, press one button — the whole batch lands in one
/// recompile.
/// <para/>
/// The work has to straddle a domain reload, which is what makes this more than a text edit: adding
/// enum values means editing <see cref="SourcePath"/>, which triggers a recompile, and the new values
/// do not exist as symbols until that finishes — so the clips cannot be written to the prefab in the
/// same managed session that wrote the enum. Phase 1 (<see cref="AddAll"/>) appends every queued value
/// and parks the clip payload in <see cref="SessionState"/>; phase 2
/// (<see cref="ApplyPendingAfterReload"/>) picks it back up on the far side and writes it into the
/// prefab asset. SessionState is the right store: it survives domain reloads but is dropped when the
/// Editor closes, so a payload orphaned by a compile error cannot rot the way an EditorPrefs entry
/// would.
/// <para/>
/// Values are only ever <b>appended</b>. <c>soundList</c> is matched to the enum by array index
/// (<c>soundList[(int)sound]</c>) and the per-entry <c>name</c> is only a label, so inserting a value
/// mid-enum shifts every later entry's clips by one — silently, with no error, until someone notices
/// the jump sound playing on landing.
/// </summary>
public class SoundTypeCreatorWindow : EditorWindow
{
    private const string SourcePath = "Assets/Script/Audio_script/SoundManager.cs";
    private const string PrefabPath = "Assets/Sounds/SFX/SoundManager.prefab";
    private const string PendingKey = "SoundTypeCreatorWindow.PendingBatch";

    private enum Kind { Sfx, Music }

    /// <summary>One queued sound, as authored in the window before anything is written.</summary>
    private class Draft
    {
        public Kind kind = Kind.Sfx;
        public string rawName = "";
        public List<AudioClip> clips = new() { null };
        public float volume = 1f;
        public float boostDb;
        public float pitchMin = 1f;
        public float pitchMax = 1f;
        public bool loop;
        public bool expanded = true;
    }

    private readonly List<Draft> _drafts = new();
    private Kind _dropKind = Kind.Sfx;
    private Vector2 _scroll;
    private bool _showExisting;

    [MenuItem("Tools/Sound Manager/Add Sound", priority = 20)]
    public static void Open()
    {
        var window = GetWindow<SoundTypeCreatorWindow>(true, "Add Sounds");
        window.minSize = new Vector2(440f, 520f);
        window.Show();
    }

    // ── GUI ───────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("Add new sounds to the SoundManager", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Creates the enum values and writes the clips into SoundManager.prefab. " +
            "Unity recompiles once for the whole batch; the clips land automatically afterwards.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(6);

        DrawDropArea();
        EditorGUILayout.Space(8);

        var validity = ValidateAll();
        DrawQueue(validity);

        EditorGUILayout.Space(4);
        if (GUILayout.Button("+ Add Empty Entry")) _drafts.Add(new Draft());

        EditorGUILayout.Space(8);
        DrawSubmit(validity);

        EditorGUILayout.Space(10);
        DrawExistingEmptyEntries();

        EditorGUILayout.EndScrollView();
    }

    private void DrawDropArea()
    {
        var rect = GUILayoutUtility.GetRect(0f, 58f, GUILayout.ExpandWidth(true));
        var style = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
        };
        GUI.Box(rect, "Drop AudioClips here\nOne entry per clip, named from the filename", style);

        var evt = Event.current;
        if (!rect.Contains(evt.mousePosition)) return;

        if (evt.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDrop.objectReferences.Any(o => o is AudioClip)
                ? DragAndDropVisualMode.Copy
                : DragAndDropVisualMode.Rejected;
            evt.Use();
        }
        else if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (var clip in DragAndDrop.objectReferences.OfType<AudioClip>())
                _drafts.Add(new Draft
                {
                    kind = _dropKind,
                    rawName = clip.name,
                    clips = new List<AudioClip> { clip },
                    expanded = false,
                });
            evt.Use();
            GUI.changed = true;
        }
    }

    private void DrawQueue(IReadOnlyList<string> validity)
    {
        EditorGUILayout.BeginHorizontal();
        _dropKind = (Kind)EditorGUILayout.EnumPopup(
            new GUIContent("Type for dropped clips", "Applied to clips dropped above. Each queued entry can still be switched individually."),
            _dropKind);
        using (new EditorGUI.DisabledScope(_drafts.Count == 0))
            if (GUILayout.Button("Clear All", GUILayout.Width(70))) _drafts.Clear();
        EditorGUILayout.EndHorizontal();

        if (_drafts.Count == 0)
        {
            EditorGUILayout.HelpBox("Queue is empty — drop some clips above, or press '+ Add Empty Entry'.", MessageType.None);
            return;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Queue ({_drafts.Count})", EditorStyles.boldLabel);

        int removeAt = -1;
        for (int i = 0; i < _drafts.Count; i++)
        {
            if (DrawDraft(i, _drafts[i], validity[i])) removeAt = i;
        }
        if (removeAt >= 0) _drafts.RemoveAt(removeAt);
    }

    /// <summary>Returns true if this row's remove button was pressed.</summary>
    private bool DrawDraft(int index, Draft draft, string problem)
    {
        string identifier = Sanitize(draft.rawName);
        bool remove = false;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        string summary = string.IsNullOrEmpty(identifier) ? "<unnamed>" : identifier;
        draft.expanded = EditorGUILayout.Foldout(draft.expanded, $"{index + 1}.  {summary}   ({draft.kind})", true);
        if (problem != null)
            EditorGUILayout.LabelField(new GUIContent("  ⚠", problem), EditorStyles.miniLabel, GUILayout.Width(20));
        if (GUILayout.Button("X", GUILayout.Width(22))) remove = true;
        EditorGUILayout.EndHorizontal();

        if (problem != null)
            EditorGUILayout.HelpBox(problem, MessageType.Error);

        if (draft.expanded)
        {
            EditorGUI.indentLevel++;
            draft.kind = (Kind)EditorGUILayout.EnumPopup("Type", draft.kind);
            draft.rawName = EditorGUILayout.TextField("Name", draft.rawName);

            if (problem == null && !string.IsNullOrEmpty(identifier))
                EditorGUILayout.LabelField(" ",
                    $"→ {(draft.kind == Kind.Sfx ? "SoundType" : "MusicType")}.{identifier}",
                    EditorStyles.miniLabel);

            DrawClips(draft);

            if (draft.kind == Kind.Sfx)
            {
                draft.volume = EditorGUILayout.Slider(
                    new GUIContent("Volume", "0 means 'use the default of 1.0' at playback time."), draft.volume, 0f, 10f);
                draft.boostDb = EditorGUILayout.Slider(
                    new GUIContent("Boost (dB)", "Extra gain on top of Volume. Affects one-shots only — looping SFX " +
                                                 "clamp at unity gain because AudioSource.volume is clamped [0,1]."),
                    draft.boostDb, -24f, 12f);
                draft.pitchMin = EditorGUILayout.Slider("Pitch Min", draft.pitchMin, 0.5f, 2f);
                draft.pitchMax = EditorGUILayout.Slider("Pitch Max", Mathf.Max(draft.pitchMax, draft.pitchMin), draft.pitchMin, 2f);
                draft.loop = EditorGUILayout.Toggle(
                    new GUIContent("Loop", "Marks the sound as looping. StartInstance/StartRange auto-detect this."), draft.loop);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        return remove;
    }

    private static void DrawClips(Draft draft)
    {
        if (draft.kind == Kind.Music)
        {
            if (draft.clips.Count == 0) draft.clips.Add(null);
            draft.clips[0] = (AudioClip)EditorGUILayout.ObjectField("Clip", draft.clips[0], typeof(AudioClip), false);
            return;
        }

        EditorGUILayout.LabelField(
            new GUIContent("Clips", "More than one clip means a random pick per play — good for footsteps and hits."));

        int removeAt = -1;
        for (int i = 0; i < draft.clips.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            draft.clips[i] = (AudioClip)EditorGUILayout.ObjectField(" ", draft.clips[i], typeof(AudioClip), false);
            using (new EditorGUI.DisabledScope(draft.clips.Count <= 1))
                if (GUILayout.Button("-", GUILayout.Width(22))) removeAt = i;
            EditorGUILayout.EndHorizontal();
        }
        if (removeAt >= 0) draft.clips.RemoveAt(removeAt);

        if (GUILayout.Button("+ Add Clip Slot (variation)", GUILayout.Height(18)))
            draft.clips.Add(null);
    }

    private void DrawSubmit(IReadOnlyList<string> validity)
    {
        EditorGUILayout.HelpBox(
            "Added to the END of each enum. Order is index order — inserting or reordering values would " +
            "silently shift every later sound's clips, so this tool only ever appends.",
            MessageType.Info);

        int problems = validity.Count(v => v != null);
        bool ready = _drafts.Count > 0 && problems == 0
                     && !EditorApplication.isCompiling && !EditorApplication.isPlaying;

        if (problems > 0)
            EditorGUILayout.HelpBox($"{problems} of {_drafts.Count} entries need fixing before anything is written.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(!ready))
        {
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
            string label = EditorApplication.isPlaying
                ? "Exit Play Mode first"
                : _drafts.Count == 1 ? "Add 1 Sound" : $"Add {_drafts.Count} Sounds";
            if (GUILayout.Button(label, GUILayout.Height(34))) AddAll();
            GUI.backgroundColor = Color.white;
        }
    }

    /// <summary>Steer designers away from creating a duplicate: most "missing" sounds already have an
    /// enum value sitting empty, and filling that is the correct move.</summary>
    private void DrawExistingEmptyEntries()
    {
        var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var sm = prefabRoot != null ? prefabRoot.GetComponent<SoundManager>() : null;
        if (sm == null) return;

        var empty = Enum.GetValues(typeof(SoundType)).Cast<SoundType>()
            .Where(s => { var e = sm.GetSoundListEntryEditorOnly(s); return e.Sounds == null || e.Sounds.Length == 0; })
            .ToArray();
        if (empty.Length == 0) return;

        _showExisting = EditorGUILayout.Foldout(_showExisting,
            $"{empty.Length} existing sound(s) have no clip yet — fill one of these instead?", true);
        if (!_showExisting) return;

        EditorGUILayout.LabelField(string.Join(", ", empty), EditorStyles.wordWrappedMiniLabel);
        if (GUILayout.Button("Open SoundManager.prefab"))
        {
            AssetDatabase.OpenAsset(prefabRoot);
            EditorGUIUtility.PingObject(prefabRoot);
        }
    }

    // ── Validation ────────────────────────────────────────────────────────

    /// <summary>One problem string per draft (null = fine). Names must be unique against the compiled
    /// enums <b>and</b> against each other — a duplicate inside the batch would emit C# that does not
    /// compile, stranding the whole queue half-applied.</summary>
    private IReadOnlyList<string> ValidateAll()
    {
        var results = new string[_drafts.Count];
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string[] existingSfx = Enum.GetNames(typeof(SoundType));
        string[] existingMusic = Enum.GetNames(typeof(MusicType));

        for (int i = 0; i < _drafts.Count; i++)
        {
            var draft = _drafts[i];
            string identifier = Sanitize(draft.rawName);

            if (string.IsNullOrEmpty(identifier))
            {
                results[i] = "Name is empty. Use A-Z, 0-9 and _ (English only).";
                continue;
            }

            string[] existing = draft.kind == Kind.Sfx ? existingSfx : existingMusic;
            if (existing.Any(n => string.Equals(n, identifier, StringComparison.OrdinalIgnoreCase)))
            {
                results[i] = $"{draft.kind} '{identifier}' already exists — assign its clip in SoundManager.prefab instead.";
                continue;
            }

            string key = draft.kind + ":" + identifier;
            if (seen.TryGetValue(key, out int first))
            {
                results[i] = $"Duplicate of entry {first + 1} in this queue.";
                continue;
            }
            seen[key] = i;

            if (!draft.clips.Any(c => c != null))
                results[i] = "Pick at least one audio clip.";
        }

        return results;
    }

    // ── Phase 1: append the enum values, park the payload ──────────────────

    private void AddAll()
    {
        var sfx = _drafts.Where(d => d.kind == Kind.Sfx).ToList();
        var music = _drafts.Where(d => d.kind == Kind.Music).ToList();

        if (sfx.Count > 0 && !TryAppendEnumValues("SoundType", sfx.Select(d => Sanitize(d.rawName)).ToList(), out string sfxError))
        {
            EditorUtility.DisplayDialog("Add Sounds failed", sfxError, "OK");
            return;
        }
        if (music.Count > 0 && !TryAppendEnumValues("MusicType", music.Select(d => Sanitize(d.rawName)).ToList(), out string musicError))
        {
            EditorUtility.DisplayDialog("Add Sounds failed", musicError, "OK");
            return;
        }

        var batch = new PendingBatch
        {
            entries = _drafts.Select(d => new PendingEntry
            {
                name = Sanitize(d.rawName),
                isMusic = d.kind == Kind.Music,
                clipGuids = d.clips.Where(c => c != null)
                                   .Select(c => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(c)))
                                   .ToArray(),
                volume = d.volume,
                boostDb = d.boostDb,
                pitchMin = d.pitchMin,
                pitchMax = d.pitchMax,
                loop = d.loop,
            }).ToArray(),
        };
        // JsonUtility cannot serialize a top-level array — it returns "{}" with no error — hence the
        // PendingBatch wrapper rather than PendingEntry[].
        SessionState.SetString(PendingKey, JsonUtility.ToJson(batch));

        Debug.Log($"[SoundTypeCreator] Added {batch.entries.Length} value(s) to '{SourcePath}'. " +
                  "Recompiling — the clips will be written to the prefab automatically once that finishes.");

        // Defer the reimport out of OnGUI — it kicks off a compile and domain reload, and this window
        // is mid-layout right now.
        EditorApplication.delayCall += () => AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceUpdate);
        Close();
    }

    /// <summary>Append <paramref name="values"/> as the last members of <paramref name="enumName"/>, in
    /// one file write. Enums contain no nested braces, so the first line-leading <c>}</c> after the
    /// declaration is reliably the closing one.</summary>
    private static bool TryAppendEnumValues(string enumName, IReadOnlyList<string> values, out string error)
    {
        string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, SourcePath);
        if (!File.Exists(fullPath))
        {
            error = $"Source file not found: {SourcePath}";
            return false;
        }

        string text = File.ReadAllText(fullPath);
        string newline = text.Contains("\r\n") ? "\r\n" : "\n";

        var declaration = Regex.Match(text, $@"public\s+enum\s+{Regex.Escape(enumName)}\s*\{{");
        if (!declaration.Success)
        {
            error = $"Could not find 'public enum {enumName}' in {SourcePath}.";
            return false;
        }

        int bodyStart = declaration.Index + declaration.Length;
        int closingBrace = text.IndexOf(newline + "}", bodyStart, StringComparison.Ordinal);
        if (closingBrace < 0)
        {
            error = $"Could not find the closing brace of enum {enumName} in {SourcePath}.";
            return false;
        }

        string body = text.Substring(bodyStart, closingBrace - bodyStart).TrimEnd();
        if (!body.EndsWith(",", StringComparison.Ordinal)) body += ",";
        foreach (string value in values) body += $"{newline}    {value},";

        File.WriteAllText(fullPath, text.Substring(0, bodyStart) + body + text.Substring(closingBrace));
        error = null;
        return true;
    }

    // ── Phase 2: after the recompile, write the clips into the prefab ──────

    [Serializable]
    private class PendingEntry
    {
        public string name;
        public bool isMusic;
        public string[] clipGuids;
        public float volume;
        public float boostDb;
        public float pitchMin;
        public float pitchMax;
        public bool loop;
    }

    [Serializable]
    private class PendingBatch
    {
        public PendingEntry[] entries;
    }

    /// <summary>Primary hook: runs as part of the reload sequence itself, with the AssetDatabase ready.</summary>
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded() => ApplyPendingAfterReload();

    /// <summary>Backup hook. <see cref="EditorApplication.delayCall"/> is not pumped while the Editor
    /// sits in the background — observed first-hand: the batch stayed parked indefinitely until the
    /// window was given OS focus — so it cannot be the only path. It costs nothing here because
    /// <see cref="ApplyPendingAfterReload"/> erases the payload before using it, so whichever hook wins
    /// the other is a no-op.</summary>
    [InitializeOnLoadMethod]
    private static void QueueApplyPending() => EditorApplication.delayCall += ApplyPendingAfterReload;

    private static void ApplyPendingAfterReload()
    {
        string json = SessionState.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(json)) return;

        // Erase first: a payload that fails to apply must not retry on every reload forever, and this
        // is what makes the two hooks above safe to both fire.
        SessionState.EraseString(PendingKey);

        var batch = JsonUtility.FromJson<PendingBatch>(json);
        if (batch?.entries != null && batch.entries.Length > 0) ApplyPending(batch.entries);
    }

    private static void ApplyPending(PendingEntry[] entries)
    {
        var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var sm = prefabRoot != null ? prefabRoot.GetComponent<SoundManager>() : null;
        if (sm == null)
        {
            Debug.LogError($"[SoundTypeCreator] {entries.Length} value(s) were added to the enum but " +
                           $"SoundManager was not found at '{PrefabPath}' — assign their clips by hand.");
            return;
        }

        // Grow the prefab asset's lists to include the values we just compiled. Ordering against
        // SoundManagerEditor's own reload-time sync is undefined, and this is idempotent, so do it here
        // rather than depend on that one having run first.
        sm.SyncListsToEnumsEditorOnly();

        string[] sfxNames = Enum.GetNames(typeof(SoundType));
        string[] musicNames = Enum.GetNames(typeof(MusicType));
        var serialized = new SerializedObject(sm);
        var applied = new List<string>();

        foreach (var pending in entries)
        {
            string[] names = pending.isMusic ? musicNames : sfxNames;
            int index = Array.IndexOf(names, pending.name);
            if (index < 0)
            {
                Debug.LogError($"[SoundTypeCreator] '{pending.name}' is not in the compiled " +
                               $"{(pending.isMusic ? "MusicType" : "SoundType")} enum — the edit to " +
                               $"'{SourcePath}' may not have compiled. Its clips were not assigned.");
                continue;
            }

            var list = serialized.FindProperty(pending.isMusic ? "musicList" : "soundList");
            if (index >= list.arraySize)
            {
                Debug.LogError($"[SoundTypeCreator] Prefab list is shorter than the enum ({list.arraySize} vs " +
                               $"{names.Length}) — clips for '{pending.name}' were not assigned.");
                continue;
            }

            var element = list.GetArrayElementAtIndex(index);
            var clips = pending.clipGuids.Select(LoadClip).Where(c => c != null).ToArray();

            if (pending.isMusic)
            {
                element.FindPropertyRelative("music").objectReferenceValue = clips.FirstOrDefault();
            }
            else
            {
                var sounds = element.FindPropertyRelative("sounds");
                sounds.arraySize = clips.Length;
                for (int i = 0; i < clips.Length; i++)
                    sounds.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];

                element.FindPropertyRelative("volume").floatValue = pending.volume;
                element.FindPropertyRelative("boostDb").floatValue = pending.boostDb;
                element.FindPropertyRelative("pitchMin").floatValue = pending.pitchMin;
                element.FindPropertyRelative("pitchMax").floatValue = pending.pitchMax;
                element.FindPropertyRelative("loop").boolValue = pending.loop;
            }

            applied.Add(pending.name);
        }

        if (applied.Count == 0) return;

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(sm);
        PrefabUtility.SavePrefabAsset(prefabRoot);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SoundTypeCreator] {applied.Count} sound(s) ready and saved to '{PrefabPath}': " +
                  $"{string.Join(", ", applied)}. They now appear in the Comic Editor's SFX picker.",
                  prefabRoot);
    }

    private static AudioClip LoadClip(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    // ── Naming ────────────────────────────────────────────────────────────

    /// <summary>Fold free text into the project's SCREAMING_SNAKE_CASE convention. Restricted to ASCII
    /// letters/digits on purpose: C# would accept a Thai identifier, but it would be the only one in
    /// the codebase and <c>Enum.TryParse</c> round-trips it through comic asset strings.</summary>
    private static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        var builder = new StringBuilder();
        foreach (char c in raw.Trim().ToUpperInvariant())
            builder.Append((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ? c : '_');

        string identifier = Regex.Replace(builder.ToString(), "_+", "_").Trim('_');
        if (identifier.Length > 0 && char.IsDigit(identifier[0])) identifier = "_" + identifier;
        return identifier;
    }
}
