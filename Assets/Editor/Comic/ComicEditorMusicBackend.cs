using System;
using UnityEditor;
using UnityEngine;
using Game.Comic;

/// <summary>
/// Comic Editor's <see cref="IComicMusicBackend"/> — same rationale and lifecycle pattern as
/// <see cref="ComicEditorSfxBackend"/> (see its doc comment): <c>SoundManager.instance</c> is
/// Play-Mode-only, so this reads the clip straight off the SoundManager prefab asset instead and
/// plays it on its own looping, <see cref="HideFlags.HideAndDontSave"/> AudioSource.
/// </summary>
public sealed class ComicEditorMusicBackend : IComicMusicBackend, IDisposable
{
    private const string SoundManagerPrefabPath = "Assets/Sounds/SFX/SoundManager.prefab";

    private SoundManager _soundManagerData;
    private GameObject _root;
    private AudioSource _musicSource;
    private bool _disposed;

    // Warn once per MusicType — Reconcile can fire repeatedly while scrubbing beats.
    private readonly System.Collections.Generic.HashSet<MusicType> _warnedEmpty = new();

    public void PlayMusic(string musicName)
    {
        if (string.IsNullOrEmpty(musicName)) return;
        if (!Enum.TryParse(musicName, out MusicType music))
        {
            Debug.LogWarning($"[ComicEditorMusicBackend] Unknown Music name '{musicName}'.");
            return;
        }

        if (_soundManagerData == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoundManagerPrefabPath);
            _soundManagerData = prefab != null ? prefab.GetComponent<SoundManager>() : null;
            if (_soundManagerData == null)
            {
                Debug.LogWarning($"[ComicEditorMusicBackend] Could not find SoundManager on prefab '{SoundManagerPrefabPath}' — no music preview available.");
                return;
            }
        }

        var clip = _soundManagerData.GetMusicClipEditorOnly(music);
        if (clip == null)
        {
            // Same silent-failure trap as ComicEditorSfxBackend.TryResolve — see its comment.
            if (_warnedEmpty.Add(music))
                Debug.LogWarning($"[ComicEditorMusicBackend] MusicType.{music} has no clip on '{SoundManagerPrefabPath}' " +
                                 "— nothing to preview. If it plays in-game, the clip is a scene-only prefab override: " +
                                 "select the SoundManager in the scene and use 'Apply Audio Overrides To Prefab'.");
            return;
        }

        EnsureRoot();

        // Same dedup SoundManager.PlayMusic itself does — a repeat request for the already-playing
        // track (e.g. an editor rebuild re-reconciling at an unchanged beat) must not restart it.
        if (_musicSource.clip == clip && _musicSource.isPlaying) return;

        _musicSource.clip = clip;
        _musicSource.Play();
    }

    /// <summary>Stops preview music and destroys the owned GameObject. Call on window OnDisable.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        _root = null;
        _musicSource = null;
    }

    private void EnsureRoot()
    {
        if (_disposed) return;
        if (_root != null && _musicSource != null) return;

        _root = new GameObject("ComicEditorMusicBackend") { hideFlags = HideFlags.HideAndDontSave };
        _musicSource = _root.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.spatialBlend = 0f;
    }
}
