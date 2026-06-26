
using UnityEngine;

/// <summary>
/// Coordinator for the boss hammer impact shockwave effect.
/// Wire OnHammerImpact() to BossHammerAttack.OnImpact in the Inspector.
/// </summary>
public class HammerShockwave : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ShockWaveManager script on the SHOCKWAVE sprite (child of Main Camera).")]
    [SerializeField] private ShockWaveManager shockWaveManager;

    [Tooltip("World-space point used as the shockwave origin — assign the 'Hammer collider' " +
             "transform or the hammer IK target.")]
    [SerializeField] private Transform hammerImpactPoint;

    [Header("Player Launch")]
    [Tooltip("Impulse force magnitude applied to the player.")]
    [SerializeField] private float launchForce  = 120f;

    [Tooltip("Vertical bias for the knockback. Higher = more upward, less horizontal.")]
    [SerializeField] private float upwardBias   = 8f;

    [Tooltip("Any player within this world-unit radius of the impact gets launched.")]
    [SerializeField] private float launchRadius = 8f;

    [Header("Camera Shake")]
    [SerializeField] private float shakeIntensity = 0.4f;
    [SerializeField] private float shakeDuration  = 0.35f;

    private Player _player;

    private void Start()
    {
        // Player is tagged "Player" but is on layer 10 "Cape" — find by tag only.
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) _player = playerGO.GetComponent<Player>();

        if (shockWaveManager == null)
            Debug.LogWarning("[HammerShockwave] ShockWaveManager reference is not set.");
        if (hammerImpactPoint == null)
            Debug.LogWarning("[HammerShockwave] hammerImpactPoint is not set — falling back to Boss position.");
    }

    /// <summary>
    /// Called by BossHammerAttack.OnImpact (UnityEvent) at the moment the hammer hits the ground.
    /// </summary>
    public void OnHammerImpact()
    {
        Vector3 impactPos = hammerImpactPoint != null
            ? hammerImpactPoint.position
            : transform.position;

        // 1. Screen-space ripple
        shockWaveManager?.Play(impactPos);

        // 2. Vertical player launch — anyone inside the radius, grounded or airborne
        if (_player != null)
        {
            float dist = Vector2.Distance(_player.transform.position, impactPos);
            if (dist <= launchRadius)
                _player.ApplyKnockback(impactPos, launchForce, upwardBias);
        }

        // 3. Camera shake
        CameraShake.Shake(shakeIntensity, shakeDuration);
    }
}
