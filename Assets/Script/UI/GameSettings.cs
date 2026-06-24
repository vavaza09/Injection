using UnityEngine;

public static class GameSettings
{
    const string KEY_MUSIC = "MusicVolume";
    const string KEY_SFX = "SFXVolume";
    const string KEY_FULLSCREEN = "Fullscreen";
    const string KEY_RESOLUTION = "ResolutionIndex";

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

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(KEY_FULLSCREEN, 1) == 1;
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
        Screen.fullScreen = Fullscreen;

        int idx = ResolutionIndex;
        if (idx >= 0 && idx < resolutions.Length)
        {
            var r = resolutions[idx];
            Screen.SetResolution(r.width, r.height, Fullscreen);
        }
    }
}
