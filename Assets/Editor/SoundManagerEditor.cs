using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SoundManager))]
public class SoundManagerEditor : Editor
{
    private const string PrefabPath = "Assets/Sounds/SFX/SoundManager.prefab";

    public override void OnInspectorGUI()
    {
        DrawSceneOnlyOverridesWarning();

        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        if (GUILayout.Button("Sync Lists To Enums", GUILayout.Height(22)))
            SyncPrefabAssetToEnums(logWhenAlreadyInSync: true);

        if (Application.isPlaying)
        {
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button("Save Play-Mode Values to Prefab", GUILayout.Height(32)))
                SaveToPrefab();
            GUI.backgroundColor = Color.white;
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Enter Play mode to tune values live, then click 'Save Play-Mode Values to Prefab' to persist them.",
                MessageType.Info);
        }
    }

    // ── Scene-only override guard ─────────────────────────────────────────

    /// <summary>Warn when audio data is authored on a *scene instance* of the SoundManager prefab.
    /// <para/>
    /// Such edits are stored as prefab overrides inside that scene's file; the prefab asset never
    /// changes. That is silently wrong here because the two readers disagree: the game hears the
    /// override (SoundManager is DontDestroyOnLoad and the only instance in the project lives in
    /// TitleScene, so the overridden instance *is* the one that runs), while every editor tool that
    /// reads the asset directly — the Comic Editor's SFX/music preview, via
    /// <c>SoundManager.GetSoundListEntryEditorOnly</c>, which cannot use the Play-Mode-only
    /// singleton — sees the original empty entry and plays nothing at all. Without this warning the
    /// only symptom is silence in one tool and correct audio everywhere else.</summary>
    private void DrawSceneOnlyOverridesWarning()
    {
        var component = target as SoundManager;
        if (component == null) return;
        if (PrefabUtility.GetPrefabInstanceStatus(component.gameObject) != PrefabInstanceStatus.Connected) return;

        var modifications = PrefabUtility.GetPropertyModifications(component.gameObject);
        if (modifications == null) return;

        var audioOverrides = modifications
            .Where(m => m.target is SoundManager && IsAudioDataPath(m.propertyPath))
            .ToArray();
        if (audioOverrides.Length == 0) return;

        EditorGUILayout.HelpBox(
            $"{audioOverrides.Length} audio value(s) are overridden on this scene instance only.\n\n" +
            "They are stored in the scene file, not in SoundManager.prefab. The game will play them, " +
            "but the Comic Editor reads the prefab asset — so it will find nothing and stay silent. " +
            "Apply them to the prefab.",
            MessageType.Warning);

        var affected = DescribeAffectedEntries(audioOverrides);
        if (affected.Count > 0)
            EditorGUILayout.LabelField("Affected", string.Join(", ", affected), EditorStyles.wordWrappedMiniLabel);

        GUI.backgroundColor = new Color(1f, 0.8f, 0.3f);
        if (GUILayout.Button("Apply Audio Overrides To Prefab", GUILayout.Height(26)))
        {
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component);
            if (string.IsNullOrEmpty(assetPath)) assetPath = PrefabPath;

            PrefabUtility.ApplyObjectOverride(component, assetPath, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SoundManagerEditor] Applied {audioOverrides.Length} audio override(s) to '{assetPath}'.");
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);
    }

    private static bool IsAudioDataPath(string propertyPath) =>
        propertyPath.StartsWith("soundList") ||
        propertyPath.StartsWith("musicList") ||
        propertyPath.StartsWith("sceneAudio");

    /// <summary>Turn raw override paths like <c>soundList.Array.data[12].volume</c> into the enum
    /// names a designer recognises (<c>EMP</c>), so the warning says which sounds are affected.</summary>
    private static List<string> DescribeAffectedEntries(PropertyModification[] audioOverrides)
    {
        string[] soundNames = System.Enum.GetNames(typeof(SoundType));
        string[] musicNames = System.Enum.GetNames(typeof(MusicType));
        var seen = new List<string>();

        foreach (var modification in audioOverrides)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                modification.propertyPath, @"^(soundList|musicList)\.Array\.data\[(\d+)\]");
            if (!match.Success) continue;

            string[] names = match.Groups[1].Value == "soundList" ? soundNames : musicNames;
            int index = int.Parse(match.Groups[2].Value);
            string label = index < names.Length ? names[index] : $"index {index}";

            if (!seen.Contains(label)) seen.Add(label);
        }

        return seen;
    }

    // ── Enum ↔ list sync ──────────────────────────────────────────────────

    /// <summary>Keep the prefab asset's lists enum-aligned on disk after every script reload.
    /// <para/>
    /// <c>SoundManager.OnEnable</c> already does this, but only for components that get *enabled* —
    /// scene instances and Prefab Mode — never for the prefab asset in the Project window, and it
    /// writes to the backing fields without dirtying anything so the resize is not persisted even
    /// then. Adding a SoundType therefore left the asset's array at its old length, and
    /// <c>GetSoundListEntryEditorOnly</c> threw IndexOutOfRangeException for the new value in the
    /// Comic Editor. Running here means a new enum value is on disk before anything reads it.</summary>
    [InitializeOnLoadMethod]
    private static void SyncPrefabAssetOnScriptReload()
    {
        // AssetDatabase is not usable during InitializeOnLoad itself — defer one editor tick.
        EditorApplication.delayCall += () => SyncPrefabAssetToEnums(logWhenAlreadyInSync: false);
    }

    private static void SyncPrefabAssetToEnums(bool logWhenAlreadyInSync)
    {
        var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var prefabComponent = prefabRoot != null ? prefabRoot.GetComponent<SoundManager>() : null;
        if (prefabComponent == null)
        {
            if (logWhenAlreadyInSync)
                Debug.LogError($"[SoundManagerEditor] Could not locate SoundManager at '{PrefabPath}'.");
            return;
        }

        if (!prefabComponent.SyncListsToEnumsEditorOnly())
        {
            if (logWhenAlreadyInSync)
                Debug.Log($"[SoundManagerEditor] '{PrefabPath}' already matches the SoundType/MusicType enums.");
            return;
        }

        EditorUtility.SetDirty(prefabComponent);
        PrefabUtility.SavePrefabAsset(prefabRoot);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SoundManagerEditor] Synced '{PrefabPath}' to the SoundType/MusicType enums " +
                  "(new entries added — assign their clips in the prefab, not on a scene instance).");
    }

    // ── Play-mode capture ─────────────────────────────────────────────────

    private void SaveToPrefab()
    {
        var live = target as SoundManager;
        if (live == null) return;

        // Try the prefab asset the instance came from first; fall back to the known path.
        var prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(live.gameObject) as GameObject;
        if (prefabRoot == null)
            prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        if (prefabRoot == null)
        {
            Debug.LogError($"[SoundManagerEditor] Could not locate prefab at '{PrefabPath}'.");
            return;
        }

        var prefabComponent = prefabRoot.GetComponent<SoundManager>();
        if (prefabComponent == null)
        {
            Debug.LogError("[SoundManagerEditor] SoundManager component not found on prefab.");
            return;
        }

        string json = EditorJsonUtility.ToJson(live);
        EditorJsonUtility.FromJsonOverwrite(json, prefabComponent);

        EditorUtility.SetDirty(prefabRoot);
        PrefabUtility.SavePrefabAsset(prefabRoot);

        Debug.Log($"[SoundManagerEditor] Saved play-mode values to '{PrefabPath}'.");
    }
}
