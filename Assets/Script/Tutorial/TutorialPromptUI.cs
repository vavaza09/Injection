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
        }

        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private RectTransform glyphRow;
        [SerializeField] private InputDeviceTracker deviceTracker;
        [SerializeField] private List<GlyphBinding> bindings = new List<GlyphBinding>();
        [SerializeField] private Color descriptionColor = Color.white;
        [SerializeField] private Color successColor = new Color(0.4f, 1f, 0.5f);
        [SerializeField] private float glyphSize = 64f;

        private string _description;
        private PromptEntry[] _keys;
        private bool _showingSuccess;
        private readonly List<GameObject> _chips = new List<GameObject>();

        private void Awake()
        {
            if (label == null || glyphRow == null) BuildDefaultUI();
            if (deviceTracker == null) deviceTracker = FindAnyObjectByType<InputDeviceTracker>();
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
            _showingSuccess = false;
            _description = description;
            _keys = actionKeys;
            Render();
        }

        public void ShowSuccess(string message)
        {
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
                var binding = Find(entry.actionKey);
                Sprite sprite = binding == null ? null : (gamepad ? binding.gamepadSprite : binding.keyboardSprite);
                if (sprite != null)
                {
                    CreateGlyphChip(sprite);
                }
                else
                {
                    string label = binding != null ? (gamepad ? binding.gamepadLabel : binding.keyboardLabel) : null;
                    if (string.IsNullOrEmpty(label)) label = entry.actionKey;
                    CreateTextChip(label);
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

        private void CreateGlyphChip(Sprite sprite)
        {
            var go = new GameObject("Glyph", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(glyphRow, false);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = glyphSize;
            le.preferredHeight = glyphSize;

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
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = descriptionColor;

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
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = descriptionColor;

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

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0f);
            panelRt.anchorMax = new Vector2(0.5f, 0f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.anchoredPosition = new Vector2(0f, 60f);
            panelRt.sizeDelta = new Vector2(1000f, 200f);
            var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.LowerCenter;
            vlg.spacing = 12f;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = false;

            var labelGo = new GameObject("PromptLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(panelGo.transform, false);
            label = labelGo.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 32;
            label.color = descriptionColor;

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
