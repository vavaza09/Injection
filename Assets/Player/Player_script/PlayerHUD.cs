using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("Charge Bar")]
    public Slider chargeBar;
    public Image fillImage; // Drag the "Fill" image inside the Slider here

    [Header("Colors")]
    public Color chargingColor = Color.white;
    public Color readyColor = Color.green;

    void Start()
    {
        // Ensure bar is hidden on start
        ToggleChargeBar(false);
    }

    public void ToggleChargeBar(bool isActive)
    {
        if (chargeBar) chargeBar.gameObject.SetActive(isActive);
    }

    public void UpdateChargeBar(float percentage, bool isReady)
    {
        if (!chargeBar) return;

        chargeBar.value = percentage;

        if (fillImage)
        {
            fillImage.color = isReady ? readyColor : chargingColor;
        }
    }

    // Fixes the UI flipping when the player flips
    public void FlipUI()
    {
        if (chargeBar)
        {
            Vector3 uiScale = chargeBar.transform.localScale;
            uiScale.x *= -1;
            chargeBar.transform.localScale = uiScale;
        }
    }
}