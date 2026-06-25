using Game.Components.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.UI.Skills
{
    public class EnergyHUD : MonoBehaviour
    {
        [Header("Pip images (optional — auto-label used if empty)")]
        [SerializeField] private Image[] pips;

        [Header("Energy counter text (two-digit, e.g. 00 01 02)")]
        [SerializeField] private TextMeshProUGUI counterText;

        [Header("Colors")]
        [SerializeField] private Color filledColor = new Color(0.4f, 0.8f, 1f, 1f);
        [SerializeField] private Color emptyColor  = new Color(0.2f, 0.2f, 0.2f, 0.5f);

        private IEnergyStore    _store;
        private TextMeshProUGUI _fallbackLabel;

        [Inject]
        public void Construct(IEnergyStore energyStore)
        {
            _store = energyStore;
        }

        private void Start()
        {
            if (_store == null) return;

            if (pips == null || pips.Length == 0)
                EnsureFallbackLabel();

            _store.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_store != null)
                _store.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (_store == null) return;

            if (pips != null && pips.Length > 0)
            {
                for (int i = 0; i < pips.Length; i++)
                {
                    if (pips[i] == null) continue;
                    pips[i].color = i < _store.Current ? filledColor : emptyColor;
                }
            }
            else if (_fallbackLabel != null)
            {
                _fallbackLabel.text = $"Energy: {_store.Current}/{_store.Max}";
            }

            if (counterText != null)
                counterText.text = _store.Current.ToString("D2");
        }

        private void EnsureFallbackLabel()
        {
            if (_fallbackLabel != null) return;

            var go = new GameObject("EnergyText", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin        = new Vector2(0f, 1f);
            rect.anchorMax        = new Vector2(0f, 1f);
            rect.pivot            = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -90f);
            rect.sizeDelta        = new Vector2(200f, 30f);

            _fallbackLabel            = go.AddComponent<TextMeshProUGUI>();
            _fallbackLabel.fontSize   = 24f;
            _fallbackLabel.alignment  = TextAlignmentOptions.TopLeft;
            _fallbackLabel.color      = new Color(0.4f, 0.8f, 1f, 1f);
        }
    }
}
