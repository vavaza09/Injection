using System.Globalization;
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
        get => GetFloat(KEY_MUSIC, 0.5f);
        set => SetFloat(KEY_MUSIC, Mathf.Clamp01(value));
    }

    public static float SFXVolume
    {
        get => GetFloat(KEY_SFX, 1f);
        set => SetFloat(KEY_SFX, Mathf.Clamp01(value));
    }

    // Default ON for desktop builds, OFF where the platform owns fullscreen (WebGL).
    public static bool Fullscreen
    {
        get => GetInt(KEY_FULLSCREEN, SupportsDisplaySettings ? 1 : 0) == 1;
        set => SetInt(KEY_FULLSCREEN, value ? 1 : 0);
    }

    public static int ResolutionIndex
    {
        get => GetInt(KEY_RESOLUTION, -1);
        set => SetInt(KEY_RESOLUTION, value);
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

    // WebGL keeps settings in localStorage for the same reason as the save (BrowserSaveStorage):
    // PlayerPrefs live in URL-keyed IndexedDB there and are lost on every itch.io re-upload.
    private static float GetFloat(string key, float defaultValue)
    {
        if (!BrowserStorage.IsAvailable)
        {
            return PlayerPrefs.GetFloat(key, defaultValue);
        }
        return float.TryParse(BrowserStorage.Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : defaultValue;
    }

    private static void SetFloat(string key, float value)
    {
        if (!BrowserStorage.IsAvailable)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            return;
        }
        BrowserStorage.Set(key, value.ToString(CultureInfo.InvariantCulture));
    }

    private static int GetInt(string key, int defaultValue)
    {
        if (!BrowserStorage.IsAvailable)
        {
            return PlayerPrefs.GetInt(key, defaultValue);
        }
        return int.TryParse(BrowserStorage.Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : defaultValue;
    }

    private static void SetInt(string key, int value)
    {
        if (!BrowserStorage.IsAvailable)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            return;
        }
        BrowserStorage.Set(key, value.ToString(CultureInfo.InvariantCulture));
    }
}
