using System;
using UnityEditor;
using UnityEngine;
using Game.Comic;

/// <summary>
/// Comic Editor's <see cref="IComicSfxBackend"/> — plays real clips for auditioning while
/// scrubbing/playing in the tool, without going through <c>SoundManager</c>'s static API.
/// <c>SoundManager.instance</c> (and therefore every SoundManager.* static method) is only ever
/// assigned in <c>Awake</c> when <c>Application.isPlaying</c> — see its doc comment — so it's
/// simply unset in Edit Mode regardless of assembly access. Instead this reads clip/volume/pitch/
/// loop data straight off the SoundManager prefab asset (<see cref="SoundManagerPrefabPath"/>,
/// the same fixed path <c>SoundManagerEditor</c> already relies on) via
/// <see cref="SoundManager.GetSoundListEntryEditorOnly"/>, and plays it on a small pool of
/// <see cref="HideFlags.HideAndDontSave"/> AudioSources it owns itself — same lifetime pattern as
/// <see cref="ComicPreviewStage"/>'s camera/canvas, including defensive recreation if Unity
/// destroys them out from under the window (scene switch, domain reload).
/// <para/>
/// Loose in the default Editor assembly, not a custom asmdef — see the Design Conventions note in
/// CLAUDE.md on predefined-assembly referencing for why that's required here.
/// </summary>
public sealed class ComicEditorSfxBackend : IComicSfxBackend, IDisposable
{
    private const string SoundManagerPrefabPath = "Assets/Sounds/SFX/SoundManager.prefab";
    private const int PoolSize = 8;

    private SoundManager _soundManagerData;
    private GameObject _root;
    private AudioSource _oneShotSource;
    private AudioSource[] _pool;
    private bool _disposed;

    public void PlayOneShot(string sfxName)
    {
        if (!TryResolve(sfxName, out var sl)) return;
        var clip = PickClip(sl);
        if (clip == null) return;

        EnsureRoot();
        _oneShotSource.pitch = UnityEngine.Random.Range(sl.EffectivePitchMin, sl.EffectivePitchMax);
        _oneShotSource.PlayOneShot(clip, sl.EffectiveVolume);
    }

    public object StartRange(string sfxName)
    {
        if (!TryResolve(sfxName, out var sl)) return null;
        var clip = PickClip(sl);
        if (clip == null) return null;

        EnsureRoot();
        AudioSource src = GetFreeSource();
        if (src == null)
        {
            Debug.LogWarning($"[ComicEditorSfxBackend] Preview instance pool exhausted ({PoolSize} slots).");
            return null;
        }

        src.clip = clip;
        src.volume = sl.EffectiveVolume;
        src.pitch = UnityEngine.Random.Range(sl.EffectivePitchMin, sl.EffectivePitchMax);
        src.loop = sl.loop;
        src.Play();
        return src;
    }

    public void StopRange(object handle)
    {
        if (handle is AudioSource src && src != null) src.Stop();
    }

    /// <summary>Stops everything and destroys the owned GameObject. Call on window OnDisable.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        _root = null;
        _oneShotSource = null;
        _pool = null;
    }

    private bool TryResolve(string sfxName, out SoundList sl)
    {
        sl = default;
        if (string.IsNullOrEmpty(sfxName)) return false;
        if (!Enum.TryParse(sfxName, out SoundType sound))
        {
            Debug.LogWarning($"[ComicEditorSfxBackend] Unknown SFX name '{sfxName}'.");
            return false;
        }

        if (_soundManagerData == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoundManagerPrefabPath);
            _soundManagerData = prefab != null ? prefab.GetComponent<SoundManager>() : null;
            if (_soundManagerData == null)
            {
                Debug.LogWarning($"[ComicEditorSfxBackend] Could not find SoundManager on prefab '{SoundManagerPrefabPath}' — no audio preview available.");
                return false;
            }
        }

        sl = _soundManagerData.GetSoundListEntryEditorOnly(sound);
        if (sl.Sounds != null && sl.Sounds.Length > 0) return true;

        // An authored-but-empty entry used to return false silently, which is indistinguishable from
        // "the beat has no SFX" — the actual cause is almost always that the clip was assigned to the
        // SoundManager *scene instance* (stored as a prefab override in the scene file) rather than to
        // the prefab asset this reads. Say so once per SoundType instead of playing nothing.
        if (_warnedEmpty.Add(sound))
            Debug.LogWarning($"[ComicEditorSfxBackend] SoundType.{sound} has no clips on '{SoundManagerPrefabPath}' " +
                             "— nothing to preview. If it plays in-game, the clip is a scene-only prefab override: " +
                             "select the SoundManager in the scene and use 'Apply Audio Overrides To Prefab'.");
        return false;
    }

    // Warn once per SoundType — Reconcile can fire repeatedly while scrubbing beats.
    private readonly System.Collections.Generic.HashSet<SoundType> _warnedEmpty = new();

    private static AudioClip PickClip(SoundList sl) =>
        sl.Sounds[UnityEngine.Random.Range(0, sl.Sounds.Length)];

    private AudioSource GetFreeSource()
    {
        foreach (var src in _pool)
            if (src != null && !src.isPlaying) return src;
        return null;
    }

    private void EnsureRoot()
    {
        if (_disposed) return;
        if (_root != null && _oneShotSource != null && _pool != null) return;

        _root = new GameObject("ComicEditorSfxBackend") { hideFlags = HideFlags.HideAndDontSave };
        _oneShotSource = _root.AddComponent<AudioSource>();
        _oneShotSource.playOnAwake = false;
        _oneShotSource.spatialBlend = 0f;

        _pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var src = _root.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            _pool[i] = src;
        }
    }
}
