using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI.Movement
{
    /// <summary>
    /// Car-dashboard style speedometer using a radial Image arc and a rotating needle (SRP).
    ///
    /// Inspector setup:
    ///   gaugeArc   — Image (Image Type: Filled, Fill Method: Radial360, Fill Origin: Bottom)
    ///   needle     — RectTransform of the needle sprite, pivoted at its base
    ///   minAngle   — rotation at 0 km/h  (e.g.  135 degrees)
    ///   maxAngle   — rotation at max km/h (e.g. -135 degrees)
    ///   speedMarks — (optional) TextMeshProUGUI labels for tick marks
    /// </summary>
    public class SpeedDashboardDisplay : MonoBehaviour, ISpeedDisplay
    {
        [Header("Gauge Arc")]
        [SerializeField] private Image gaugeArc;
        [SerializeField] private Gradient arcColorGradient;

        [Header("Needle")]
        [SerializeField] private RectTransform needle;
        [SerializeField] private float needleMinAngle = 135f;
        [SerializeField] private float needleMaxAngle = -135f;
        [SerializeField] private float needleSmoothSpeed = 8f;

        [Header("Speed Labels")]
        [SerializeField] private TextMeshProUGUI currentSpeedLabel;
        [SerializeField] private string speedFormat = "{0:F0}";
        [SerializeField] private string unit = "km/h";

        [Header("Visibility")]
        [SerializeField] private GameObject dashboardRoot;

        private float _targetAngle;
        private float _currentAngle;

        private void Awake()
        {
            if (dashboardRoot == null)
                dashboardRoot = gameObject;

            _currentAngle = needleMinAngle;
            _targetAngle = needleMinAngle;
        }

        public void Refresh(ISpeedDataProvider provider)
        {
            UpdateArc(provider.NormalizedSpeed);
            UpdateNeedle(provider.NormalizedSpeed);
            UpdateSpeedLabel(provider.CurrentSpeedKmh);
        }

        public void SetVisible(bool visible)
        {
            if (dashboardRoot != null)
                dashboardRoot.SetActive(visible);
        }

        private void UpdateArc(float normalizedSpeed)
        {
            if (gaugeArc == null) return;

            gaugeArc.fillAmount = normalizedSpeed;

            if (arcColorGradient != null)
                gaugeArc.color = arcColorGradient.Evaluate(normalizedSpeed);
        }

        private void UpdateNeedle(float normalizedSpeed)
        {
            if (needle == null) return;

            _targetAngle = Mathf.LerpUnclamped(needleMinAngle, needleMaxAngle, normalizedSpeed);
            _currentAngle = Mathf.LerpAngle(_currentAngle, _targetAngle, Time.deltaTime * needleSmoothSpeed);
            needle.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);
        }

        private void UpdateSpeedLabel(float speedKmh)
        {
            if (currentSpeedLabel == null) return;
            currentSpeedLabel.text = $"{string.Format(speedFormat, speedKmh)} {unit}";
        }
    }
}
