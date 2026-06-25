using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

// Attach to each weakpoint child GameObject on the Boss (needs a Collider2D + Light2D + SpriteRenderer).
// BossWeakPointManager drives Show/Hide/TryDestroy — don't call them from elsewhere.
public class BossWeakPoint : MonoBehaviour
{
    [SerializeField] private Light2D       pointLight;
    [SerializeField] private Collider2D    hitCollider;
    [SerializeField] private SpriteRenderer disc;
    [SerializeField] private Color         openColor   = Color.blue;
    [SerializeField] private Color         closedColor = Color.yellow;

    [Header("Platform Events")]
    [SerializeField] private UnityEvent onBecameAttackable;
    [SerializeField] private UnityEvent onWindowClosed;

    public bool IsDestroyed { get; private set; }
    public bool IsVisible => _state == WPState.Open;
    public event Action OnDestroyed;

    private enum WPState { Hidden, Closed, Open, Destroyed }
    private WPState _state;
    private Color   _defaultDiscColor;
    private bool    _wasAttackable;

    private void Awake()
    {
        if (pointLight  == null) pointLight  = GetComponentInChildren<Light2D>();
        if (hitCollider == null) hitCollider = GetComponent<Collider2D>();
        if (disc        == null) disc        = GetComponentInChildren<SpriteRenderer>();

        if (disc != null) _defaultDiscColor = disc.color;

        SetLightOff();
        if (disc        != null) disc.enabled        = false;
        if (hitCollider != null) hitCollider.enabled = false;
    }

    // Show the weakpoint. attackable=true → blue (player can hit), false → stay hidden.
    public void Show(bool attackable)
    {
        if (IsDestroyed) return;
        _state = attackable ? WPState.Open : WPState.Closed;

        if (attackable)
        {
            if (pointLight != null)
            {
                pointLight.color   = openColor;
                pointLight.enabled = true;
            }
            if (disc != null) { disc.enabled = true; SetDisc(openColor); }
            if (hitCollider != null) hitCollider.enabled = true;

            if (!_wasAttackable)
            {
                _wasAttackable = true;
                onBecameAttackable?.Invoke();
            }
        }
        else
        {
            SetLightOff();
            if (disc != null) disc.enabled = false;
            if (hitCollider != null) hitCollider.enabled = false;
        }
    }

    // Hide the weakpoint between reveal windows (light off, collider off).
    public void Hide()
    {
        if (IsDestroyed) return;
        _state = WPState.Hidden;
        SetLightOff();
        if (disc != null) disc.enabled = false;
        if (hitCollider != null) hitCollider.enabled = false;

        if (_wasAttackable)
        {
            _wasAttackable = false;
            onWindowClosed?.Invoke();
        }
    }

    // Called by PlayerDashImpact when the player hits this collider.
    // Returns true if the weakpoint was Open and is now destroyed; false otherwise.
    public bool TryDestroy()
    {
        if (_state != WPState.Open) return false;
        _state      = WPState.Destroyed;
        IsDestroyed = true;
        SetLightOff();
        if (disc != null) disc.enabled = false;
        if (hitCollider != null) hitCollider.enabled = false;

        if (_wasAttackable)
        {
            _wasAttackable = false;
            onWindowClosed?.Invoke();
        }

        OnDestroyed?.Invoke();
        return true;
    }

    private void SetLightOff()
    {
        if (pointLight != null) pointLight.enabled = false;
    }

    private void SetDisc(Color c)
    {
        if (disc == null) return;
        disc.color = new Color(c.r, c.g, c.b, _defaultDiscColor.a);
    }
}
