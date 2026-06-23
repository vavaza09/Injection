using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Attach to each weakpoint child GameObject on the Boss (needs a Collider2D + Light2D).
// BossWeakPointManager drives Show/Hide/TryDestroy — don't call them from elsewhere.
public class BossWeakPoint : MonoBehaviour
{
    [SerializeField] private Light2D       pointLight;
    [SerializeField] private Collider2D    hitCollider;
    [SerializeField] private Color         openColor   = Color.blue;
    [SerializeField] private Color         closedColor = Color.yellow;

    public bool IsDestroyed { get; private set; }
    public event Action OnDestroyed;

    private enum WPState { Hidden, Closed, Open, Destroyed }
    private WPState _state;

    private void Awake()
    {
        if (pointLight   == null) pointLight   = GetComponentInChildren<Light2D>();
        if (hitCollider  == null) hitCollider  = GetComponent<Collider2D>();
        SetLightOff();
        if (hitCollider != null) hitCollider.enabled = false;
    }

    // Show the weakpoint. attackable=true → blue (player can hit), false → yellow (display only).
    public void Show(bool attackable)
    {
        if (IsDestroyed) return;
        _state = attackable ? WPState.Open : WPState.Closed;
        if (pointLight != null)
        {
            pointLight.color   = attackable ? openColor : closedColor;
            pointLight.enabled = true;
        }
        if (hitCollider != null) hitCollider.enabled = attackable;
    }

    // Hide the weakpoint between reveal windows (light off, collider off).
    public void Hide()
    {
        if (IsDestroyed) return;
        _state = WPState.Hidden;
        SetLightOff();
        if (hitCollider != null) hitCollider.enabled = false;
    }

    // Called by PlayerDashImpact when the player hits this collider.
    // Returns true if the weakpoint was Open and is now destroyed; false otherwise.
    public bool TryDestroy()
    {
        if (_state != WPState.Open) return false;
        _state      = WPState.Destroyed;
        IsDestroyed = true;
        SetLightOff();
        if (hitCollider != null) hitCollider.enabled = false;
        OnDestroyed?.Invoke();
        return true;
    }

    private void SetLightOff()
    {
        if (pointLight != null) pointLight.enabled = false;
    }
}
