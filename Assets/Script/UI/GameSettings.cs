using UnityEngine;

public static class GameSettings
{
    const string KEY_MUSIC = "MusicVolume";
    const string KEY_SFX = "SFXVolume";
    const string KEY_FULLSCREEN = "Fullscreen";
    const string KEY_RESOLUTION = "ResolutionIndex";

    // Default display resolution used on first run (before the player picks one).
    public const int DefaultWidth = 1920;
    public const int DefaultHeight = 1080;

    // Whether the game owns the display resolution / fullscreen state on this platform.
    // On WebGL the browser owns it: the canvas is sized by the WebGL template's CSS
    // (Assets/WebGLTemplates/BlueSteam), and fullscreen is the browser's own F11.
    // Calling Screen.SetResolution / Screen.fullScreen there fights the template —
    // it pins the canvas to a fixed pixel size (the "not full height" borders) and
    // goes through the HTML5 Fullscreen API, which resizes the canvas + backbuffer
    // mid-session and is the source of the fullscreen stutter.
    public static bool SupportsDisplaySettings
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            return true;
#endif
        }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(KEY_MUSIC, 0.5f);
        set { PlayerPrefs.SetFloat(KEY_MUSIC, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    public static float SFXVolume
    {
        get => PlayerPrefs.GetFloat(KEY_SFX, 1f);
        set { PlayerPrefs.SetFloat(KEY_SFX, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    // Default ON for desktop builds, OFF where the platform owns fullscreen (WebGL).
    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(KEY_FULLSCREEN, SupportsDisplaySettings ? 1 : 0) == 1;
        set { PlayerPrefs.SetInt(KEY_FULLSCREEN, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static int ResolutionIndex
    {
        get => PlayerPrefs.GetInt(KEY_RESOLUTION, -1);
        set { PlayerPrefs.SetInt(KEY_RESOLUTION, value); PlayerPrefs.Save(); }
    }

    public static void ApplyVolumes()
    {
        SoundManager.SetMusicVolume(MusicVolume);
        SoundManager.SetSFXVolume(SFXVolume);
    }

    public static void ApplyAll(Resolution[] resolutions)
    {
        ApplyVolumes();
        ApplyResolution(resolutions);
    }

    // Apply the resolution at startup. Uses the player's saved choice if any,
    // otherwise falls back to the default (1920x1080).
    // No-op on WebGL — see SupportsDisplaySettings.
    public static void ApplyResolution(Resolution[] resolutions)
    {
        if (!SupportsDisplaySettings)
        {
            return;
        }

        int idx = ResolutionIndex;
        if (idx >= 0 && idx < resolutions.Length)
        {
            var r = resolutions[idx];
            Screen.SetResolution(r.width, r.height, Fullscreen);
        }
        else
        {
            Screen.SetResolution(DefaultWidth, DefaultHeight, Fullscreen);
        }
    }

    // Convenience overload for callers that don't already hold the list.
    public static void ApplyResolution() => ApplyResolution(Screen.resolutions);
}
