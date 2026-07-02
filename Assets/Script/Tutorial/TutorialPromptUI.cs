using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tutorial
{
    /// <summary>
    /// Shows the current step's description plus button glyphs for the relevant actions. Glyphs are
    /// resolved per the last-used input device (keyboard vs gamepad) via <see cref="InputDeviceTracker"/>
    /// and live-switch when the player swaps device. Each action key maps to a sprite (Kenney input
    /// prompts) through <see cref="bindings"/>; if no sprite is assigned it falls back to a text chip.
    /// </summary>
    public class TutorialPromptUI : MonoBehaviour
    {
        [Serializable]
        public class GlyphBinding
        {
            public string actionKey;            // e.g. "Move", "Jump", "Aim", "Dash", "Grab", "Skill1", "Skill2"
            public string keyboardLabel = "?";  // text fallback when no keyboard sprite
            public string gamepadLabel = "?";   // text fallback when no gamepad sprite
            public Sprite keyboardSprite;       // Kenney keyboard/mouse glyph
            public Sprite gamepadSprite;        // Kenney gamepad glyph
            [Tooltip("Override glyph size for this binding only. 0 = use global glyphSize.")]
            public float sizeOverride = 0f;
        }

        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private RectTransform glyphRow;
        [SerializeField] private InputDeviceTracker deviceTracker;
        [SerializeField] private List<GlyphBinding> bindings = new List<GlyphBinding>();
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Color descriptionColor = Color.white;
        [SerializeField] private Color successColor = new Color(0.4f, 1f, 0.5f);
        [SerializeField] private float fontSize = 32f;
        [SerializeField] private float glyphSize = 64f;

        [Header("Position")]
        [SerializeField] private Vector2 panelAnchor   = new Vector2(0.5f, 0f);
        [SerializeField] private Vector2 panelPosition = new Vector2(0f, 60f);

        [Header("Background")]
        [SerializeField] private Color   bgColor  = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private float   bgHeight = 130f;

        private RectTransform _panelRect;
        private Image _bgImage;
        private string _description;
        private PromptEntry[] _keys;
        private bool _showingSuccess;
        private readonly List<GameObject> _chips = new List<GameObject>();
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            if (label == null || glyphRow == null) BuildDefaultUI();
            if (deviceTracker == null) deviceTracker = FindAnyObjectByType<InputDeviceTracker>();
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }

        private void Update()
        {
            var current = new Vector2Int(Screen.width, Screen.height);
            if (current != _lastScreenSize)
            {
                _lastScreenSize = current;
                if (!_showingSuccess && _keys != null) Render();
            }
        }

        private void OnEnable()
        {
            if (deviceTracker != null) deviceTracker.DeviceChanged += OnDeviceChanged;
        }

        private void OnDisable()
        {
            if (deviceTracker != null) deviceTracker.DeviceChanged -= OnDeviceChanged;
        }

        public void Show(string description, PromptEntry[] actionKeys)
        {
            if (_bgImage != null) _bgImage.gameObject.SetActive(true);
            _showingSuccess = false;
            _description = description;
            _keys = actionKeys;
            Render();
        }

        public void ShowSuccess(string message)
        {
            if (_bgImage != null) _bgImage.gameObject.SetActive(true);
            _showingSuccess = true;
            ClearChips();
            if (label != null)
            {
                label.color = successColor;
                label.text = message;
            }
        }

        public void Hide()
        {
            if (_bgImage != null) _bgImage.gameObject.SetActive(false);
            ClearChips();
            if (label != null) label.text = string.Empty;
        }

        private void OnDeviceChanged(InputDeviceKind kind)
        {
            if (!_showingSuccess) Render();
        }

        private void Render()
        {
            if (label != null)
            {
                label.color = descriptionColor;
                label.text = _description;
            }

            ClearChips();
            if (_keys == null || glyphRow == null) return;

            bool gamepad = deviceTracker != null && deviceTracker.Current == InputDeviceKind.Gamepad;
            for (int i = 0; i < _keys.Length; i++)
            {
                var entry = _keys[i];

                if (string.IsNullOrEmpty(entry.actionKey))
                {
                    if (!string.IsNullOrEmpty(entry.text))
                        CreateLabelChip(entry.text);
                    else
                        CreateSpacerChip(entry.spacerWidth);
                }
                else
                {
                    var binding = Find(entry.actionKey);
                    Sprite sprite = binding == null ? null : (gamepad ? binding.gamepadSprite : binding.keyboardSprite);
                    if (sprite != null)
                    {
                        float size = (binding != null && binding.sizeOverride > 0f) ? binding.sizeOverride : glyphSize;
                        CreateGlyphChip(sprite, size);
                    }
                    else
                    {
                        string chipLabel = binding != null ? (gamepad ? binding.gamepadLabel : binding.keyboardLabel) : null;
                        if (string.IsNullOrEmpty(chipLabel)) chipLabel = entry.actionKey;
                        CreateTextChip(chipLabel);
                    }
                }

                if (entry.separatorAfter != SeparatorAfter.None)
                    CreateSeparatorChip(entry.separatorAfter);
            }
        }

        private GlyphBinding Find(string key)
        {
            for (int i = 0; i < bindings.Count; i++)
                if (bindings[i].actionKey == key) return bindings[i];
            return null;
        }

        private void CreateGlyphChip(Sprite sprite, float size)
        {
            var go = new GameObject("Glyph", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(glyphRow, false);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;

            _chips.Add(go);
        }

        private void CreateSeparatorChip(SeparatorAfter kind)
        {
            string symbol = kind switch
            {
                SeparatorAfter.Plus => "+",
                SeparatorAfter.Then => "→",
                SeparatorAfter.Or   => "/",
                _                   => ""
            };
            if (string.IsNullOrEmpty(symbol)) return;

            var go = new GameObject("Separator", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(glyphRow, false);

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = symbol;
            tmp.fontSize = fontSize * 0.875f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = descriptionColor;
            if (font != null) tmp.font = font;

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = glyphSize;
            le.preferredWidth = 24f;

            _chips.Add(go);
        }

        private void CreateTextChip(string text)
        {
            var go = new GameObject("GlyphText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(glyphRow, false);

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = "[" + text + "]";
            tmp.fontSize = fontSize * 0.875f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = descriptionColor;
            if (font != null) tmp.font = font;

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = glyphSize;

            _chips.Add(go);
        }

        private void CreateSpacerChip(float width)
        {
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(glyphRow, false);

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = width > 0f ? width : 24f;
            le.minHeight = glyphSize;

            _chips.Add(go);
        }

        private void CreateLabelChip(string text)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(glyphRow, false);

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize * 0.875f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = descriptionColor;
            if (font != null) tmp.font = font;

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = glyphSize;

            _chips.Add(go);
        }

        private void ClearChips()
        {
            for (int i = 0; i < _chips.Count; i++)
                if (_chips[i] != null) Destroy(_chips[i]);
            _chips.Clear();
        }

        private void BuildDefaultUI()
        {
            var canvasGo = new GameObject("TutorialPromptCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Full-width background strip — Panel lives inside it so position always matches.
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, panelAnchor.y);
            bgRect.anchorMax = new Vector2(1f, panelAnchor.y);
            bgRect.pivot     = new Vector2(0.5f, panelAnchor.y);
            bgRect.offsetMin = new Vector2(0f, panelPosition.y);
            bgRect.offsetMax = new Vector2(0f, panelPosition.y + bgHeight);
            _bgImage = bgGo.GetComponent<Image>();
            _bgImage.color = bgColor;
            _bgImage.raycastTarget = false;
            bgGo.AddComponent<RectMask2D>();
            bgGo.SetActive(false);

            // Panel is a child of BG, stretches to fill it — guarantees vertical centering.
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panelGo.transform.SetParent(bgGo.transform, false);
            _panelRect = panelGo.GetComponent<RectTransform>();
            _panelRect.anchorMin = Vector2.zero;
            _panelRect.anchorMax = Vector2.one;
            _panelRect.offsetMin = Vector2.zero;
            _panelRect.offsetMax = Vector2.zero;
            var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 12f;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = false;

            var labelGo = new GameObject("PromptLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(panelGo.transform, false);
            label = labelGo.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = fontSize;
            label.color = descriptionColor;
            if (font != null) label.font = font;

            var rowGo = new GameObject("GlyphRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(panelGo.transform, false);
            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 12f;
            hlg.childControlHeight = true;
            hlg.childControlWidth = true;
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth = false;
            glyphRow = rowGo.GetComponent<RectTransform>();
        }
    }
}
