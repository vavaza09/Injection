using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public class WavTrimmerWindow : EditorWindow
{
    // ── clip state ──────────────────────────────────────────────────────────
    private AudioClip _clip;
    private string    _assetPath;
    private float     _duration;
    private float[]   _waveform;   // mono-mixed, downsampled for display
    private Texture2D _waveformTex;

    // ── trim params ─────────────────────────────────────────────────────────
    private float _startTime;
    private float _endTime;

    // ── output options ──────────────────────────────────────────────────────
    private bool   _overwrite;
    private string _suffix = "_trim";

    // ── status ───────────────────────────────────────────────────────────────
    private string      _status;
    private MessageType _statusType;

    // ── drag state ───────────────────────────────────────────────────────────
    private enum DragHandle { None, Start, End, Region }
    private DragHandle _drag = DragHandle.None;

    // ── AudioUtil reflection ─────────────────────────────────────────────────
    private static MethodInfo _playClip;
    private static MethodInfo _stopAll;
    private static bool       _audioUtilResolved;

    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/WAV Trimmer")]
    public static void ShowWindow() => GetWindow<WavTrimmerWindow>("WAV Trimmer");

    // ════════════════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        DrawHeader();
        DrawClipPicker();
        if (_clip == null) return;
        DrawInfo();
        DrawWaveformArea();
        DrawTimingFields();
        DrawOutputOptions();
        DrawButtons();
        DrawStatus();
    }

    // ── sections ─────────────────────────────────────────────────────────────
    private void DrawHeader()
    {
        GUILayout.Label("WAV Trimmer", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
    }

    private void DrawClipPicker()
    {
        var next = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", _clip, typeof(AudioClip), false);
        if (next != _clip) OnClipAssigned(next);
    }

    private void DrawInfo()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            $"Duration: {_duration:F4}s    " +
            $"Sample rate: {_clip.frequency} Hz    " +
            $"Channels: {_clip.channels}    " +
            $"Samples: {_clip.samples:N0}",
            MessageType.None);
    }

    private void DrawWaveformArea()
    {
        EditorGUILayout.Space(6);
        var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
            GUILayout.ExpandWidth(true), GUILayout.Height(80));
        DrawWaveform(rect);
        HandleWaveformMouse(rect);
    }

    private void DrawTimingFields()
    {
        EditorGUILayout.Space(6);

        // start
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Start", GUILayout.Width(36));
        float s = EditorGUILayout.Slider(_startTime, 0f, _duration);
        float sField = EditorGUILayout.FloatField(_startTime, GUILayout.Width(70));
        EditorGUILayout.LabelField("s", GUILayout.Width(12));
        EditorGUILayout.EndHorizontal();
        _startTime = Mathf.Clamp(Mathf.Approximately(s, _startTime) ? sField : s, 0f, _endTime);

        // end
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("End", GUILayout.Width(36));
        float e = EditorGUILayout.Slider(_endTime, 0f, _duration);
        float eField = EditorGUILayout.FloatField(_endTime, GUILayout.Width(70));
        EditorGUILayout.LabelField("s", GUILayout.Width(12));
        EditorGUILayout.EndHorizontal();
        _endTime = Mathf.Clamp(Mathf.Approximately(e, _endTime) ? eField : e, _startTime, _duration);

        var sel = _endTime - _startTime;
        EditorGUILayout.LabelField($"Selected: {sel:F4}s", EditorStyles.centeredGreyMiniLabel);
    }

    private void DrawOutputOptions()
    {
        EditorGUILayout.Space(6);
        _overwrite = EditorGUILayout.Toggle("Overwrite Original", _overwrite);
        if (!_overwrite)
            _suffix = EditorGUILayout.TextField("Output Suffix", _suffix);
    }

    private void DrawButtons()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("▶ Preview")) PlayPreview();
        if (GUILayout.Button("■ Stop"))   StopPreview();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        bool canTrim = (_endTime - _startTime) > 0.001f;
        using (new EditorGUI.DisabledScope(!canTrim))
        {
            if (GUILayout.Button("Trim & Save", GUILayout.Height(32)))
                TrimAndSave();
        }
    }

    private void DrawStatus()
    {
        if (string.IsNullOrEmpty(_status)) return;
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(_status, _statusType);
    }

    // ── waveform ─────────────────────────────────────────────────────────────
    private void DrawWaveform(Rect rect)
    {
        // background
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));

        if (_waveformTex != null)
            GUI.DrawTexture(rect, _waveformTex, ScaleMode.StretchToFill);

        if (_duration <= 0f) return;

        // dimmed out-of-range
        float sx = rect.x + (_startTime / _duration) * rect.width;
        float ex = rect.x + (_endTime   / _duration) * rect.width;
        var dimColor = new Color(0f, 0f, 0f, 0.55f);
        EditorGUI.DrawRect(new Rect(rect.x,  rect.y, sx - rect.x,     rect.height), dimColor);
        EditorGUI.DrawRect(new Rect(ex,       rect.y, rect.xMax - ex,  rect.height), dimColor);

        // handle lines
        const float handleW = 2f;
        EditorGUI.DrawRect(new Rect(sx - handleW * 0.5f, rect.y, handleW, rect.height), Color.yellow);
        EditorGUI.DrawRect(new Rect(ex - handleW * 0.5f, rect.y, handleW, rect.height), Color.cyan);

        // time labels
        DrawTimeLabel(rect, _startTime, sx, Color.yellow, TextAnchor.UpperLeft);
        DrawTimeLabel(rect, _endTime,   ex, Color.cyan,   TextAnchor.UpperRight);
    }

    private static void DrawTimeLabel(Rect waveRect, float time, float xPx, Color color, TextAnchor anchor)
    {
        var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = color }, alignment = anchor };
        string label = $"{time:F3}s";
        var sz = style.CalcSize(new GUIContent(label));
        float lx = anchor == TextAnchor.UpperLeft ? xPx + 2 : xPx - sz.x - 2;
        lx = Mathf.Clamp(lx, waveRect.x, waveRect.xMax - sz.x);
        GUI.Label(new Rect(lx, waveRect.y + 2, sz.x, sz.y), label, style);
    }

    private void HandleWaveformMouse(Rect rect)
    {
        if (_duration <= 0f) return;
        var ev = Event.current;

        float sx = rect.x + (_startTime / _duration) * rect.width;
        float ex = rect.x + (_endTime   / _duration) * rect.width;
        const float hitRadius = 7f;

        switch (ev.type)
        {
            case EventType.MouseDown when rect.Contains(ev.mousePosition):
                float mx = ev.mousePosition.x;
                if (Mathf.Abs(mx - sx) < hitRadius)       _drag = DragHandle.Start;
                else if (Mathf.Abs(mx - ex) < hitRadius)  _drag = DragHandle.End;
                else                                       _drag = DragHandle.Region;
                ev.Use();
                break;

            case EventType.MouseDrag when _drag != DragHandle.None:
                float t = Mathf.Clamp01((ev.mousePosition.x - rect.x) / rect.width) * _duration;
                if (_drag == DragHandle.Start)
                    _startTime = Mathf.Clamp(t, 0f, _endTime);
                else if (_drag == DragHandle.End)
                    _endTime = Mathf.Clamp(t, _startTime, _duration);
                else
                {
                    // single click in region → set nearest handle
                    float ds = Mathf.Abs(ev.mousePosition.x - sx);
                    float de = Mathf.Abs(ev.mousePosition.x - ex);
                    if (ds < de) _startTime = Mathf.Clamp(t, 0f, _endTime);
                    else         _endTime   = Mathf.Clamp(t, _startTime, _duration);
                }
                ev.Use();
                Repaint();
                break;

            case EventType.MouseUp:
                _drag = DragHandle.None;
                break;
        }

        // cursor hint
        if (Mathf.Abs(ev.mousePosition.x - sx) < hitRadius && rect.Contains(ev.mousePosition))
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
        else if (Mathf.Abs(ev.mousePosition.x - ex) < hitRadius && rect.Contains(ev.mousePosition))
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
    }

    // ── clip assignment ───────────────────────────────────────────────────────
    private void OnClipAssigned(AudioClip clip)
    {
        _clip      = clip;
        _status    = null;
        _waveform  = null;

        if (_waveformTex != null) { DestroyImmediate(_waveformTex); _waveformTex = null; }

        if (_clip == null) return;

        _assetPath = AssetDatabase.GetAssetPath(_clip);
        _duration  = _clip.length;
        _startTime = 0f;
        _endTime   = _duration;

        BuildWaveformTexture();
    }

    private void BuildWaveformTexture()
    {
        const int W = 512, H = 80;
        int ch = _clip.channels;
        float[] all = new float[_clip.samples * ch];
        if (!_clip.GetData(all, 0)) return;

        // downsample to W buckets, mono-mix
        _waveform = new float[W];
        float samplesPerPx = (float)_clip.samples / W;
        for (int x = 0; x < W; x++)
        {
            int start = (int)(x * samplesPerPx) * ch;
            int end   = Mathf.Min((int)((x + 1) * samplesPerPx) * ch, all.Length);
            float peak = 0f;
            for (int i = start; i < end; i++) peak = Mathf.Max(peak, Mathf.Abs(all[i]));
            _waveform[x] = peak;
        }

        _waveformTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var pixels = new Color[W * H];

        var bg      = new Color(0.12f, 0.12f, 0.12f, 0f); // transparent (bg already drawn)
        var waveCol = new Color(0.3f, 0.85f, 0.4f);

        for (int x = 0; x < W; x++)
        {
            int barH = Mathf.RoundToInt(_waveform[x] * H * 0.9f);
            int mid  = H / 2;
            for (int y = 0; y < H; y++)
                pixels[y * W + x] = (y >= mid - barH / 2 && y <= mid + barH / 2) ? waveCol : bg;
        }

        _waveformTex.SetPixels(pixels);
        _waveformTex.Apply();
    }

    // ── trim & save ──────────────────────────────────────────────────────────
    private void TrimAndSave()
    {
        try
        {
            int sampleRate = _clip.frequency;
            int channels   = _clip.channels;

            int startSample = Mathf.FloorToInt(_startTime * sampleRate);
            int endSample   = Mathf.CeilToInt (_endTime   * sampleRate);
            endSample = Mathf.Min(endSample, _clip.samples);
            int count = endSample - startSample;

            float[] all = new float[_clip.samples * channels];
            if (!_clip.GetData(all, 0))
            {
                SetStatus("Cannot read audio data — clip may be a streaming/compressed type.", MessageType.Error);
                return;
            }

            float[] trimmed = new float[count * channels];
            Array.Copy(all, startSample * channels, trimmed, 0, count * channels);

            string absIn = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", _assetPath));

            string absOut, assetOut;
            if (_overwrite)
            {
                absOut   = absIn;
                assetOut = _assetPath;
            }
            else
            {
                string dir  = Path.GetDirectoryName(absIn)!;
                string name = Path.GetFileNameWithoutExtension(absIn) + _suffix + ".wav";
                absOut   = Path.Combine(dir, name);
                assetOut = Path.Combine(
                    Path.GetDirectoryName(_assetPath)!, name).Replace('\\', '/');
            }

            WriteWav(absOut, trimmed, sampleRate, channels);
            AssetDatabase.ImportAsset(assetOut, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            float outDur = count / (float)sampleRate;
            SetStatus($"Saved: {assetOut}  ({outDur:F4}s)", MessageType.Info);
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", MessageType.Error);
        }
    }

    private static void WriteWav(string path, float[] samples, int rate, int channels)
    {
        int byteCount = samples.Length * 2;
        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + byteCount);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));

        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);              // PCM
        bw.Write((short)channels);
        bw.Write(rate);
        bw.Write(rate * channels * 2);
        bw.Write((short)(channels * 2));
        bw.Write((short)16);

        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(byteCount);
        foreach (float s in samples)
            bw.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
    }

    // ── audio preview via reflection (internal UnityEditor.AudioUtil) ────────
    private void PlayPreview()
    {
        if (_clip == null) return;
        ResolveAudioUtil();
        try
        {
            _stopAll?.Invoke(null, null);
            _playClip?.Invoke(null, new object[] { _clip, 0, false });
        }
        catch { /* AudioUtil signature mismatch — silently skip */ }
    }

    private void StopPreview()
    {
        ResolveAudioUtil();
        try { _stopAll?.Invoke(null, null); }
        catch { }
    }

    private static void ResolveAudioUtil()
    {
        if (_audioUtilResolved) return;
        _audioUtilResolved = true;
        var t = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
        if (t == null) return;

        // Unity 2020+: PlayPreviewClip(AudioClip, int, bool)
        _playClip = t.GetMethod("PlayPreviewClip",
            BindingFlags.Static | BindingFlags.Public, null,
            new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);

        _stopAll = t.GetMethod("StopAllPreviewClips",
            BindingFlags.Static | BindingFlags.Public);
    }

    // ── helpers ───────────────────────────────────────────────────────────────
    private void SetStatus(string msg, MessageType type)
    {
        _status     = msg;
        _statusType = type;
        Repaint();
    }

    private void OnDisable()
    {
        if (_waveformTex != null) DestroyImmediate(_waveformTex);
    }
}
