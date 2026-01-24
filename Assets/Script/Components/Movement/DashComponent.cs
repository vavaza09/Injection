using System;
using UnityEngine;

public class DashComponent : MonoBehaviour
{
    [Header("Dash Setting")]
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashCooldown = 2f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private LayerMask obstacleLayer;

    private float lastDashTime = -Mathf.Infinity;
    private bool isDashing = false;
    private Vector2 dashTarget;

    public event Action OnDashStart;
    public event Action OnDashEnd;

    public bool CanDash()
    {
        return ! isDashing && Time.time >= lastDashTime + dashCooldown;
    }

    public bool IsDashing => isDashing;

    public void SetDashTarget(Vector2 target)
    {
        dashTarget = target;
    }

    public Vector2 GetDashTarget() => dashTarget;

    public void StartDash()
    {
        if(! CanDash()) return;

        isDashing = true;
        lastDashTime = Time.time;
        OnDashStart?.Invoke();
    }

    public void EndDash()
    {
        isDashing = false;
        OnDashEnd?.Invoke();
    }

    public float GetCooldownProgress()
    {
        return Mathf.Clamp01((Time.time - lastDashTime) / dashCooldown);
    }

}
