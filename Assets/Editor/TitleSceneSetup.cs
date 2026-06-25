using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Run via Tools > Setup Title Scene to wire all interactivity into TitleScene.unity.
/// Safe to re-run; existing GameObjects are reused if found by name.
/// </summary>
public static class TitleSceneSetup
{
    const string SCENE_PATH = "Assets/Scenes/TitleScene.unity";
    const string FONT_GUID = "6357d2da8981e924cbc8f0263069aaef";    // LAMCHO SDF
    const string MUSIC_GUID = "c893a20a9312a3f4db7842533154030f";   // Yhwach Theme
    const string UI_SFX_GUID = "85bfc1de72be6d04690cae46fbfcd832";  // HitStop placeholder for UI sounds
    static readonly Color CREAM = new Color(0.972549f, 0.9098039f, 0.77254903f, 1f);

    [MenuItem("Tools/Setup Title Scene")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        // ── Locate the existing Title canvas root ──────────────────────────
        var titleCanvas = GameObject.Find("Title");
        if (titleCanvas == null) { Debug.LogError("[TitleSetup] Could not find 'Title' canvas root."); return; }

        var canvas = titleCanvas.GetComponent<Canvas>();

        // ── EventSystem ────────────────────────────────────────────────────
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // ── SoundManager GameObject ────────────────────────────────────────
        var soundMgrGO = GameObject.Find("SoundManager");
        if (soundMgrGO == null)
        {
            soundMgrGO = new GameObject("SoundManager");
            soundMgrGO.AddComponent<AudioSource>();
            soundMgrGO.AddComponent<AudioSource>();
        }
        if (soundMgrGO.GetComponent<SoundManager>() == null)
            soundMgrGO.AddComponent<SoundManager>();

        // Assign audio clips to SoundManager via SerializedObject
        AssignSoundManagerClips(soundMgrGO);

        // ── Menu Manager ───────────────────────────────────────────────────
        var menuMgrGO = GameObject.Find("MenuManager");
        if (menuMgrGO == null) menuMgrGO = new GameObject("MenuManager");
        var controller = menuMgrGO.GetComponent<MainMenuController>()
                         ?? menuMgrGO.AddComponent<MainMenuController>();

        // ── Wire the three main buttons ────────────────────────────────────
        var options = titleCanvas.transform.Find("Options");
        if (options == null) { Debug.LogError("[TitleSetup] 'Options' not found under Title."); return; }

        WireMenuButton(options, "Start", controller, "OnStart");
        WireMenuButton(options, "Quit", controller, "OnQuit");

        // Setting button — find or activate
        var settingTf = options.Find("Setting");
        if (settingTf != null) settingTf.gameObject.SetActive(true);
        WireMenuButton(options, "Setting", controller, "OpenSettings");

        // ── Settings Panel ────────────────────────────────────────────────
        var settingsPanel = BuildOrFindSettingsPanel(titleCanvas.transform, controller);

        // Wire controller → settingsPanel reference via serialized field
        var so = new SerializedObject(controller);
        var spProp = so.FindProperty("settingsPanel");
        if (spProp != null) { spProp.objectReferenceValue = settingsPanel; so.ApplyModifiedProperties(); }

        // ── Save ──────────────────────────────────────────────────────────
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TitleSetup] TitleScene setup complete. Compile, then open TitleScene and verify.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SoundManager clip assignment
    // ─────────────────────────────────────────────────────────────────────────

    static void AssignSoundManagerClips(GameObject soundMgrGO)
    {
        var sm = soundMgrGO.GetComponent<SoundManager>();
        if (sm == null) return;

        var musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(MUSIC_GUID));
        var uiClip    = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(UI_SFX_GUID));

        var so = new SerializedObject(sm);

        // musicList[MENU = 0]
        var musicList = so.FindProperty("musicList");
        if (musicList != null && musicList.arraySize > 0)
        {
            var menuEntry = musicList.GetArrayElementAtIndex(0);
            var musicProp = menuEntry.FindPropertyRelative("music");
            if (musicProp != null && musicClip != null) musicProp.objectReferenceValue = musicClip;
        }

        // soundList[UI_HOVER] and soundList[UI_CLICK] are the last two entries
        var soundList = so.FindProperty("soundList");
        if (soundList != null && uiClip != null)
        {
            int count = soundList.arraySize;
            // UI_HOVER = count-2, UI_CLICK = count-1
            for (int i = Mathf.Max(0, count - 2); i < count; i++)
            {
                var entry = soundList.GetArrayElementAtIndex(i);
                var sounds = entry.FindPropertyRelative("sounds");
                if (sounds != null)
                {
                    sounds.arraySize = 1;
                    sounds.GetArrayElementAtIndex(0).objectReferenceValue = uiClip;
                }
                var vol = entry.FindPropertyRelative("volume");
                if (vol != null) vol.floatValue = 0.6f;
                var pitchMin = entry.FindPropertyRelative("pitchMin");
                if (pitchMin != null) pitchMin.floatValue = 1f;
                var pitchMax = entry.FindPropertyRelative("pitchMax");
                if (pitchMax != null) pitchMax.floatValue = 1f;
            }
        }

        so.ApplyModifiedProperties();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button wiring
    // ─────────────────────────────────────────────────────────────────────────

    static void WireMenuButton(Transform options, string goName, MainMenuController controller, string methodName)
    {
        var tf = options.Find(goName);
        if (tf == null) { Debug.LogWarning($"[TitleSetup] Button '{goName}' not found under Options."); return; }
        var go = tf.gameObject;

        // Ensure Image exists as raycast target (already present but make sure)
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0); // transparent hitbox
        img.raycastTarget = true;

        // Add Button
        var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None; // visual handled by our hover script

        // Wire onClick
        var so = new SerializedObject(btn);
        var onClick = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        onClick.ClearArray();
        onClick.InsertArrayElementAtIndex(0);
        var call = onClick.GetArrayElementAtIndex(0);
        call.FindPropertyRelative("m_Target").objectReferenceValue = controller;
        call.FindPropertyRelative("m_MethodName").stringValue = methodName;
        call.FindPropertyRelative("m_Mode").intValue = 1; // void, no args
        call.FindPropertyRelative("m_CallState").intValue = 2; // RuntimeOnly
        so.ApplyModifiedProperties();

        // Add MenuButtonHover
        var hover = go.GetComponent<MenuButtonHover>() ?? go.AddComponent<MenuButtonHover>();
        var hoverSO = new SerializedObject(hover);
        hoverSO.FindProperty("target").objectReferenceValue = go.GetComponent<RectTransform>();

        // Collect reveal targets: Glow (already active but hidden by alpha), LPoint, RPoint
        var revealTargets = new List<GameObject>();
        foreach (Transform child in tf)
        {
            string n = child.name;
            if (n == "Glow" || n == "LPoint" || n == "RPoint")
            {
                // Start hidden so hover reveals them
                child.gameObject.SetActive(false);
                revealTargets.Add(child.gameObject);
            }
        }
        var revealProp = hoverSO.FindProperty("revealOnHover");
        revealProp.arraySize = revealTargets.Count;
        for (int i = 0; i < revealTargets.Count; i++)
            revealProp.GetArrayElementAtIndex(i).objectReferenceValue = revealTargets[i];
        hoverSO.ApplyModifiedProperties();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Settings Panel construction
    // ─────────────────────────────────────────────────────────────────────────

    static GameObject BuildOrFindSettingsPanel(Transform canvasRoot, MainMenuController controller)
    {
        var existing = canvasRoot.Find("SettingsPanel");
        if (existing != null) return existing.gameObject;

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FONT_GUID));

        // Root panel — full-screen dark overlay
        var panel = new GameObject("SettingsPanel");
        panel.transform.SetParent(canvasRoot, false);
        panel.SetActive(false);

        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        panel.AddComponent<CanvasRenderer>();

        // ── Add SettingsMenu script ──
        var settingsMenu = panel.AddComponent<SettingsMenu>();

        // Content container (centered box)
        var box = MakeRect("Box", panel.transform, new Vector2(600, 480), Vector2.zero);
        var boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = new Color(0.1f, 0.09f, 0.08f, 0.95f);

        // Title label
        var titleLabel = MakeTMPLabel("SETTINGS", box.transform, font, 28, CREAM, new Vector2(0, 180), new Vector2(500, 50));

        float rowY = 100f;
        float rowStep = 80f;

        // ── Music row ──
        var (musicSlider, musicPct) = MakeSliderRow("Music", box.transform, font, rowY);
        rowY -= rowStep;

        // ── SFX row ──
        var (sfxSlider, sfxPct) = MakeSliderRow("SFX", box.transform, font, rowY);
        rowY -= rowStep;

        // ── Resolution row ──
        MakeTMPLabel("Resolution", box.transform, font, 18, CREAM, new Vector2(-120, rowY), new Vector2(160, 36));
        var resDropdown = MakeDropdown(box.transform, new Vector2(100, rowY));
        rowY -= rowStep;

        // ── Fullscreen row ──
        MakeTMPLabel("Fullscreen", box.transform, font, 18, CREAM, new Vector2(-120, rowY), new Vector2(160, 36));
        var fsToggle = MakeToggle(box.transform, new Vector2(100, rowY));

        // ── Back button ──
        var backBtn = MakeButton("Back", box.transform, font, new Vector2(0, -180), new Vector2(160, 48));
        var backBtnComp = backBtn.GetComponent<Button>();
        var backSO = new SerializedObject(backBtnComp);
        var backClicks = backSO.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        backClicks.ClearArray();
        backClicks.InsertArrayElementAtIndex(0);
        var backCall = backClicks.GetArrayElementAtIndex(0);
        backCall.FindPropertyRelative("m_Target").objectReferenceValue = controller;
        backCall.FindPropertyRelative("m_MethodName").stringValue = "CloseSettings";
        backCall.FindPropertyRelative("m_Mode").intValue = 1;
        backCall.FindPropertyRelative("m_CallState").intValue = 2;
        backSO.ApplyModifiedProperties();

        // Wire SettingsMenu serialized refs
        var smSO = new SerializedObject(settingsMenu);
        smSO.FindProperty("musicSlider").objectReferenceValue = musicSlider;
        smSO.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
        smSO.FindProperty("musicPercent").objectReferenceValue = musicPct;
        smSO.FindProperty("sfxPercent").objectReferenceValue = sfxPct;
        smSO.FindProperty("resolutionDropdown").objectReferenceValue = resDropdown;
        smSO.FindProperty("fullscreenToggle").objectReferenceValue = fsToggle;
        smSO.ApplyModifiedProperties();

        return panel;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI helpers
    // ─────────────────────────────────────────────────────────────────────────

    static RectTransform MakeRect(string name, Transform parent, Vector2 size, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return rt;
    }

    static TMP_Text MakeTMPLabel(string text, Transform parent, TMP_FontAsset font, float size, Color color, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(text + "_Label");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = pos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        return tmp;
    }

    static (Slider slider, TMP_Text pctLabel) MakeSliderRow(string label, Transform parent, TMP_FontAsset font, float y)
    {
        MakeTMPLabel(label, parent, font, 18, CREAM, new Vector2(-160, y), new Vector2(120, 36));

        // Slider background
        var sliderGO = new GameObject(label + "_Slider");
        sliderGO.transform.SetParent(parent, false);
        var sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.sizeDelta = new Vector2(220, 20);
        sliderRT.anchoredPosition = new Vector2(40, y);

        var slider = sliderGO.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.5f;

        // Background image
        var bg = new GameObject("Background");
        bg.transform.SetParent(sliderGO.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.18f, 0.16f, 1f);

        // Fill area
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGO.transform, false);
        var faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0, 0.25f); faRT.anchorMax = new Vector2(1, 0.75f);
        faRT.offsetMin = new Vector2(5, 0); faRT.offsetMax = new Vector2(-15, 0);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillRT = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = CREAM;

        // Handle slide area
        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderGO.transform, false);
        var haRT = handleArea.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.offsetMin = new Vector2(10, 0); haRT.offsetMax = new Vector2(-10, 0);

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var handleRT = handle.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(20, 20);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;

        // Percent label
        var pct = MakeTMPLabel("50%", parent, font, 16, CREAM, new Vector2(175, y), new Vector2(60, 36));
        pct.alignment = TextAlignmentOptions.MidlineRight;

        return (slider, pct);
    }

    static TMP_Dropdown MakeDropdown(Transform parent, Vector2 pos)
    {
        var go = new GameObject("ResolutionDropdown");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(240, 36);
        rt.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.18f, 0.16f, 1f);

        var dd = go.AddComponent<TMP_Dropdown>();

        // Label child
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(10, 2); labelRT.offsetMax = new Vector2(-30, -2);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.color = CREAM;
        labelTMP.fontSize = 16;
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        dd.captionText = labelTMP;

        return dd;
    }

    static Toggle MakeToggle(Transform parent, Vector2 pos)
    {
        var go = new GameObject("FullscreenToggle");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(36, 36);
        rt.anchoredPosition = pos;

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.18f, 0.16f, 1f);

        var toggle = go.AddComponent<Toggle>();
        toggle.targetGraphic = bg;

        // Checkmark
        var check = new GameObject("Checkmark");
        check.transform.SetParent(go.transform, false);
        var checkRT = check.AddComponent<RectTransform>();
        checkRT.anchorMin = new Vector2(0.1f, 0.1f); checkRT.anchorMax = new Vector2(0.9f, 0.9f);
        checkRT.offsetMin = Vector2.zero; checkRT.offsetMax = Vector2.zero;
        var checkImg = check.AddComponent<Image>();
        checkImg.color = CREAM;

        toggle.graphic = checkImg;
        return toggle;
    }

    static GameObject MakeButton(string label, Transform parent, TMP_FontAsset font, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(label + "_Button");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.13f, 0.12f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero; labelRT.offsetMax = Vector2.zero;
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        if (font != null) tmp.font = font;
        tmp.fontSize = 20;
        tmp.color = CREAM;
        tmp.alignment = TextAlignmentOptions.Center;

        return go;
    }
}
