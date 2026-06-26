using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text musicPercent;
    [SerializeField] private TMP_Text sfxPercent;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    private Resolution[] _resolutions;
    private bool _initializing;

    private void OnEnable()
    {
        _initializing = true;
        BuildResolutions();
        musicSlider.value = GameSettings.MusicVolume;
        sfxSlider.value = GameSettings.SFXVolume;
        fullscreenToggle.isOn = GameSettings.Fullscreen;
        UpdateMusicLabel(musicSlider.value);
        UpdateSFXLabel(sfxSlider.value);
        _initializing = false;
    }

    private void Start()
    {
        musicSlider.onValueChanged.AddListener(OnMusicChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
    }

    private void BuildResolutions()
    {
        var raw = Screen.resolutions;
        var seen = new HashSet<long>();
        var unique = new List<Resolution>();

        for (int i = raw.Length - 1; i >= 0; i--)
        {
            long key = ((long)raw[i].width << 32) | (uint)raw[i].height;
            if (seen.Add(key)) unique.Add(raw[i]);
        }
        unique.Reverse();
        _resolutions = unique.ToArray();

        resolutionDropdown.ClearOptions();
        var options = new List<string>();
        int savedIdx = GameSettings.ResolutionIndex;
        int defaultIdx = 0;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            options.Add($"{_resolutions[i].width} x {_resolutions[i].height}");
            // No saved choice yet -> default the selection to 1920x1080 if available.
            if (_resolutions[i].width == GameSettings.DefaultWidth
                && _resolutions[i].height == GameSettings.DefaultHeight)
                defaultIdx = i;
        }

        int selectIdx = (savedIdx >= 0 && savedIdx < _resolutions.Length) ? savedIdx : defaultIdx;

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = selectIdx;
        resolutionDropdown.RefreshShownValue();
    }

    private void OnMusicChanged(float v)
    {
        UpdateMusicLabel(v);
        GameSettings.MusicVolume = v;
        SoundManager.SetMusicVolume(v);
    }

    private void OnSFXChanged(float v)
    {
        UpdateSFXLabel(v);
        GameSettings.SFXVolume = v;
        SoundManager.SetSFXVolume(v);
    }

    private void OnResolutionChanged(int idx)
    {
        if (_initializing || _resolutions == null || idx >= _resolutions.Length) return;
        GameSettings.ResolutionIndex = idx;
        var r = _resolutions[idx];
        Screen.SetResolution(r.width, r.height, GameSettings.Fullscreen);
    }

    private void OnFullscreenChanged(bool isOn)
    {
        if (_initializing) return;
        GameSettings.Fullscreen = isOn;
        Screen.fullScreen = isOn;
    }

    private void UpdateMusicLabel(float v) => musicPercent.text = Mathf.RoundToInt(v * 100) + "%";
    private void UpdateSFXLabel(float v) => sfxPercent.text = Mathf.RoundToInt(v * 100) + "%";
}
