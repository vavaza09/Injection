using TMPro;
using UnityEngine;

namespace Game.UI.Movement
{
    /// <summary>
    /// Displays current speed as a numeric label with a "km/h" unit suffix (SRP).
    /// Attach to a GameObject that has a TextMeshProUGUI component.
    /// </summary>
    public class SpeedTextDisplay : MonoBehaviour, ISpeedDisplay
    {
        [SerializeField] private TextMeshProUGUI speedLabel;
        [SerializeField] private string format = "{0:F0}";
        [SerializeField] private string unit = "km/h";

        private void Awake()
        {
            if (speedLabel == null)
                speedLabel = GetComponent<TextMeshProUGUI>();
        }

        public void Refresh(ISpeedDataProvider provider)
        {
            if (speedLabel == null) return;
            speedLabel.text = $"{string.Format(format, provider.CurrentSpeedKmh)} {unit}";
        }

        public void SetVisible(bool visible)
        {
            if (speedLabel != null)
                speedLabel.gameObject.SetActive(visible);
        }
    }
}
