using UnityEngine;
using System;
using System.Collections;

public enum SoundType
{
    SWORD,
    DASH,
    JUMP,
    FOOTSTEP,
    HURT,
    MAGIC,
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

    private static SoundManager instance;
    private AudioSource sfxSource;
    private AudioSource musicSource;
    private Coroutine musicFadeCoroutine;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Get or create audio sources
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

        // Configure music source
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;
    }

    #region Sound Effects

    public static void PlaySound(SoundType sound, float volume = 1)
    {
        if (instance == null || instance.sfxSource == null) return;

        AudioClip[] clips = instance.soundList[(int)sound].Sounds;
        if (clips == null || clips.Length == 0) return;

        AudioClip randomClip = clips[UnityEngine.Random.Range(0, clips.Length)];
        instance.sfxSource.PlayOneShot(randomClip, volume);
    }

    public static void SetSFXVolume(float volume)
    {
        if (instance != null && instance.sfxSource != null)
        {
            instance.sfxSource.volume = Mathf.Clamp01(volume);
        }
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
    public AudioClip[] Sounds { get => sounds; }
    [SerializeField] public string name;
    [SerializeField] private AudioClip[] sounds;
}

[Serializable]
public struct MusicList
{
    public AudioClip Music { get => music; }
    [SerializeField] public string name;
    [SerializeField] private AudioClip music;
}
