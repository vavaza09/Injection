using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public enum SoundType
{
    SWORD,
    DASH,
    JUMP,
    FOOTSTEP,
    FOOTSTEP_METAL,
    HURT,
    MAGIC,
    HITSTOP,
    DEATH,
    LAND,
    GRAB,
    ENERGY,
    EMP,
    TRUEDAMAGE_ARM,
    TRUEDAMAGE_CONSUME,
    SLOWMO,
    WALLSLIDE,

    // Enemies — shared
    ENEMY_HURT,
    ENEMY_DEATH,

    // Kiki
    KIKI_FLOAT_IDLE,
    KIKI_ATTACK_WINDUP,
    KIKI_ATTACK_DASH,
    KIKI_ATTACK_HIT,

    // Tank
    TANK_MOTOR,
    TANK_FOOTSTEP,
    TANK_CANNON_CHARGE,
    TANK_CANNON_FIRE,
    TANK_BULLET_FLY,

    // Furnace
    FURNACE_IDLE_CRACKLE,
    FURNACE_ATTACK_CHARGE,
    FURNACE_MORTAR_LAUNCH,
    FURNACE_MORTAR_ASCENDING,
    FURNACE_MORTAR_DESCENDING,
    FURNACE_EXPLOSION,

    // Stomper
    STOMPER_IDLE_CHITTER,
    STOMPER_WALK_SCUTTLE,
    STOMPER_JUMP,
    STOMPER_FALL_RUSH,
    STOMPER_STOMP_IMPACT,

    // Boss
    BOSS_AMBIENT,
    BOSS_HURT,
    BOSS_DEATH,
    BOSS_CLAW_ANTICIPATION,
    BOSS_CLAW_STRIKE,
    BOSS_CLAW_IMPACT,
    BOSS_CLAW_RETRACT,
    BOSS_HAMMER_WINDUP,
    BOSS_HAMMER_SWING,
    BOSS_HAMMER_IMPACT,
    BOSS_HAMMER_RECOVER,
    BOSS_GAS_RELEASE,
    BOSS_GAS_LOOP,
    BOSS_WEAKPOINT_REVEAL,
    BOSS_WEAKPOINT_CLOSE,

    // UI
    UI_HOVER,
    UI_CLICK,
}

public enum MusicType
{
    MENU,
    GAMEPLAY,
    BOSS,
    VICTORY,
    GAMEOVER,
}

[RequireComponent(typeof(AudioSource)), ExecuteInEditMode]
public class SoundManager : MonoBehaviour
{
    [Header("Sound Effects")]
    [SerializeField] private SoundList[] soundList;

    [Header("Background Music")]
    [SerializeField] private MusicList[] musicList;
    [SerializeField] private float musicVolume = 0.5f;
    [SerializeField] private float musicFadeDuration = 1f;

    [Header("Loop Pool")]
    [SerializeField, Min(2)] private int loopPoolSize = 8;

    private static SoundManager instance;
    private AudioSource sfxSource;
    private AudioSource musicSource;
    private AudioSource _footstepSource;

    // Pool of AudioSources for looping SFX — supports multiple concurrent loops
    private AudioSource[] _loopPool;
    private readonly Dictionary<int, AudioSource> _activeLoopers = new();

    private Coroutine musicFadeCoroutine;

    private void Awake()
    {
        if (!Application.isPlaying) return;

        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Get or create the two primary sources (RequireComponent guarantees at least one exists)
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length >= 2)
        {
            sfxSource = sources[0];
            musicSource = sources[1];
        }
        else
        {
            sfxSource = GetComponent<AudioSource>();
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;

        // Remove any stale AudioSources beyond the 2 we use (accumulated from editor bugs)
        AudioSource[] allSources = GetComponents<AudioSource>();
        for (int i = 2; i < allSources.Length; i++)
            Destroy(allSources[i]);

        _footstepSource = gameObject.AddComponent<AudioSource>();
        _footstepSource.playOnAwake = false;

        // Build loop pool — each slot is an independent looping AudioSource
        _loopPool = new AudioSource[loopPoolSize];
        for (int i = 0; i < loopPoolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            _loopPool[i] = src;
        }

        // Apply saved settings (PlayerPrefs) so in-game volume matches the player's preference
        GameSettings.ApplyVolumes();
    }

    #region Sound Effects

    /// <summary>Play a one-shot SFX through an entity's own 3D AudioSource (positional audio).</summary>
    public static void PlaySoundOn(SoundType sound, AudioSource source, float volumeOverride = -1f)
    {
        if (instance == null || source == null) return;

        SoundList sl = instance.soundList[(int)sound];
        AudioClip[] clips = sl.Sounds;
        if (clips == null || clips.Length == 0) return;

        float vol   = volumeOverride >= 0f ? volumeOverride : sl.EffectiveVolume;
        float pitch = UnityEngine.Random.Range(sl.EffectivePitchMin, sl.EffectivePitchMax);
        source.pitch = pitch;
        source.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)], vol);
    }

    /// <summary>Start a looping SFX on an entity's own 3D AudioSource.</summary>
    public static void StartLoopOn(SoundType sound, AudioSource source, float volumeOverride = -1f)
    {
        if (instance == null || source == null) return;

        SoundList sl = instance.soundList[(int)sound];
        AudioClip[] clips = sl.Sounds;
        if (clips == null || clips.Length == 0) return;

        source.clip   = clips[UnityEngine.Random.Range(0, clips.Length)];
        source.volume = volumeOverride >= 0f ? volumeOverride : sl.EffectiveVolume;
        source.pitch  = UnityEngine.Random.Range(sl.EffectivePitchMin, sl.EffectivePitchMax);
        source.loop   = true;
        source.Play();
    }

    /// <summary>Stop a looping SFX on an entity's own 3D AudioSource.</summary>
    public static void StopLoopOn(AudioSource source)
    {
        if (source != null) source.Stop();
    }

    public static void PlaySound(SoundType sound, float volumeOverride = -1f)
    {
        if (instance == null || instance.sfxSource == null) return;

        SoundList sl = instance.soundList[(int)sound];
        AudioClip[] clips = sl.Sounds;
        if (clips == null || clips.Length == 0) return;

        float vol   = volumeOverride >= 0f ? volumeOverride : sl.EffectiveVolume;
        float pitch = UnityEngine.Random.Range(sl.EffectivePitchMin, sl.EffectivePitchMax);
        instance.sfxSource.pitch = pitch;
        instance.sfxSource.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)], vol);
    }

    public static void PlayFootstep(SoundType sound, float volumeOverride = -1f)
    {
        if (instance == null || instance._footstepSource == null) return;

        SoundList sl = instance.soundList[(int)sound];
        AudioClip[] clips = sl.Sounds;
        if (clips == null || clips.Length == 0) return;

        float vol   = volumeOverride >= 0f ? volumeOverride : sl.EffectiveVolume;
        float pitch = UnityEngine.Random.Range(sl.EffectivePitchMin, sl.EffectivePitchMax);
        instance._footstepSource.pitch = pitch;
        instance._footstepSource.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)], vol);
    }

    public static void StopFootstep()
    {
        if (instance != null && instance._footstepSource != null)
            instance._footstepSource.Stop();
    }

    public static void SetSFXVolume(float volume)
    {
        if (instance == null) return;
        float v = Mathf.Clamp01(volume);
        if (instance.sfxSource != null) instance.sfxSource.volume = v;
        if (instance._footstepSource != null) instance._footstepSource.volume = v;
        if (instance._loopPool != null)
            foreach (var src in instance._loopPool)
                if (src != null) src.volume = v;
    }

    /// <summary>Start (or restart) a looping sound. Uses per-sound volume/pitch from the Inspector.</summary>
    public static void StartLoop(SoundType sound, float volumeOverride = -1f)
    {
        if (instance == null) return;
        instance.StartLoopInternal(sound, volumeOverride);
    }

    private void StartLoopInternal(SoundType sound, float volumeOverride)
    {
        int key = (int)sound;
        SoundList sl = soundList[key];
        AudioClip[] clips = sl.Sounds;
        if (clips == null || clips.Length == 0) return;

        // Already looping this exact sound — do nothing
        if (_activeLoopers.TryGetValue(key, out var existing) && existing.isPlaying)
            return;

        AudioSource src = GetFreeLoopSource();
        if (src == null)
        {
            Debug.LogWarning($"[SoundManager] Loop pool exhausted ({loopPoolSize} slots). Increase Loop Pool Size.");
            return;
        }

        src.clip   = clips[UnityEngine.Random.Range(0, clips.Length)];
        src.volume = volumeOverride >= 0f ? volumeOverride : sl.EffectiveVolume;
        src.pitch  = UnityEngine.Random.Range(sl.EffectivePitchMin, sl.EffectivePitchMax);
        src.Play();
        _activeLoopers[key] = src;
    }

    private AudioSource GetFreeLoopSource()
    {
        foreach (var src in _loopPool)
            if (!src.isPlaying) return src;
        return null;
    }

    /// <summary>Stop a specific looping sound.</summary>
    public static void StopLoop(SoundType sound)
    {
        if (instance == null) return;
        int key = (int)sound;
        if (instance._activeLoopers.TryGetValue(key, out var src))
        {
            src.Stop();
            instance._activeLoopers.Remove(key);
        }
    }

    /// <summary>Stop all currently looping sounds (original behaviour).</summary>
    public static void StopLoop()
    {
        if (instance == null) return;
        foreach (var src in instance._loopPool)
            src.Stop();
        instance._activeLoopers.Clear();
    }

    #endregion

    #region Background Music

    public static void PlayMusic(MusicType music, bool fade = true)
    {
        if (instance == null || instance.musicSource == null) return;

        AudioClip clip = instance.musicList[(int)music].Music;
        if (clip == null) return;

        // If same music is already playing, do nothing
        if (instance.musicSource.clip == clip && instance.musicSource.isPlaying)
            return;

        if (fade)
        {
            instance.PlayMusicWithFade(clip);
        }
        else
        {
            instance.musicSource.clip = clip;
            instance.musicSource.volume = instance.musicVolume;
            instance.musicSource.Play();
        }
    }


    public static void StopMusic(bool fade = true)
    {
        if (instance == null || instance.musicSource == null) return;

        if (fade)
        {
            instance.StopMusicWithFade();
        }
        else
        {
            instance.musicSource.Stop();
        }
    }

   
    public static void PauseMusic()
    {
        if (instance != null && instance.musicSource != null)
        {
            instance.musicSource.Pause();
        }
    }

    public static void ResumeMusic()
    {
        if (instance != null && instance.musicSource != null)
        {
            instance.musicSource.UnPause();
        }
    }

    public static void SetMusicVolume(float volume)
    {
        if (instance != null)
        {
            instance.musicVolume = Mathf.Clamp01(volume);
            if (instance.musicSource != null)
            {
                instance.musicSource.volume = instance.musicVolume;
            }
        }
    }

    public static float GetMusicVolume()
    {
        return instance != null ? instance.musicVolume : 0.5f;
    }


    public static bool IsMusicPlaying()
    {
        return instance != null && instance.musicSource != null && instance.musicSource.isPlaying;
    }

    #endregion

    #region Private Music Methods

    private void PlayMusicWithFade(AudioClip newClip)
    {
        if (musicFadeCoroutine != null)
        {
            StopCoroutine(musicFadeCoroutine);
        }

        musicFadeCoroutine = StartCoroutine(FadeMusicCoroutine(newClip));
    }

    private void StopMusicWithFade()
    {
        if (musicFadeCoroutine != null)
        {
            StopCoroutine(musicFadeCoroutine);
        }

        musicFadeCoroutine = StartCoroutine(FadeOutCoroutine());
    }

    private IEnumerator FadeMusicCoroutine(AudioClip newClip)
    {
        // Fade out current music
        if (musicSource.isPlaying)
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < musicFadeDuration)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeDuration);
                yield return null;
            }
        }

        // Switch to new music
        musicSource.clip = newClip;
        musicSource.Play();

        // Fade in new music
        float targetVolume = musicVolume;
        float fadeInElapsed = 0f;

        while (fadeInElapsed < musicFadeDuration)
        {
            fadeInElapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0f, targetVolume, fadeInElapsed / musicFadeDuration);
            yield return null;
        }

        musicSource.volume = targetVolume;
        musicFadeCoroutine = null;
    }

    private IEnumerator FadeOutCoroutine()
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < musicFadeDuration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeDuration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = musicVolume;
        musicFadeCoroutine = null;
    }

    #endregion

    #region Editor Only

#if UNITY_EDITOR
    private void OnEnable()
    {
        // Setup sound effects list
        string[] soundNames = Enum.GetNames(typeof(SoundType));
        Array.Resize(ref soundList, soundNames.Length);
        for (int i = 0; i < soundList.Length; i++)
        {
            soundList[i].name = soundNames[i];
        }

        // Setup music list
        string[] musicNames = Enum.GetNames(typeof(MusicType));
        Array.Resize(ref musicList, musicNames.Length);
        for (int i = 0; i < musicList.Length; i++)
        {
            musicList[i].name = musicNames[i];
        }
    }
#endif

    #endregion
}

[Serializable]
public struct SoundList
{
    public AudioClip[] Sounds => sounds;

    // Helpers that return safe defaults when fields are left at 0 (newly added enum entries)
    public float EffectiveVolume   => volume   > 0f ? volume   : 1f;
    public float EffectivePitchMin => pitchMin > 0f ? pitchMin : 1f;
    public float EffectivePitchMax => pitchMax > 0f ? pitchMax : 1f;

    [SerializeField] public string name;
    [SerializeField] private AudioClip[] sounds;

    [Range(0f, 1f)]
    [SerializeField] public float volume;       // 0 = use default (1.0)

    [Range(0.5f, 2f)]
    [SerializeField] public float pitchMin;     // 0 = use default (1.0)

    [Range(0.5f, 2f)]
    [SerializeField] public float pitchMax;     // 0 = use default (1.0)

    [SerializeField] public bool loop;          // informational — used by StartLoop callers
}

[Serializable]
public struct MusicList
{
    public AudioClip Music { get => music; }
    [SerializeField] public string name;
    [SerializeField] private AudioClip music;
}
