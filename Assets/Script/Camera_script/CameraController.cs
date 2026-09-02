using System.Collections;
using UnityEngine;

/// <summary>
/// Hollow Knight-inspired 2D camera. Attach to the Main Camera.
/// Disable CinemachineBrain + CinemachineCamera before using.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController instance { get; private set; }

    [Header("Target")]
    [Tooltip("Transform the camera follows — usually the player root.")]
    [SerializeField] private Transform target;

    [Header("Deadzone")]
    [Tooltip("Half-width of the deadzone window (world units). Player moves this far horizontally before camera tracks.")]
    [SerializeField] private float deadzoneHalfW = 1.2f;
    [Tooltip("Half-height of the deadzone window (world units). Player moves this far vertically before camera tracks.")]
    [SerializeField] private float deadzoneHalfH = 0.7f;

    [Header("Look-Ahead")]
    [Tooltip("Max world-unit offset the camera leads ahead in the player's velocity direction.")]
    [SerializeField] private float lookAheadDistance = 2.0f;
    [Tooltip("Seconds for look-ahead to interpolate to the target direction. Higher = lazier lead.")]
    [SerializeField] private float lookAheadSmoothTime = 0.35f;

    [Header("Follow Speed")]
    [Tooltip("SmoothDamp time for horizontal tracking. Higher = softer/slower follow.")]
    [SerializeField] private float smoothTimeX = 0.14f;
    [Tooltip("SmoothDamp time for vertical tracking.")]
    [SerializeField] private float smoothTimeY = 0.18f;

    [Header("Room Bounds")]
    [Tooltip("When enabled, clamps the camera so edges never exceed the room boundary.")]
    [SerializeField] private bool useBounds;
    [Tooltip("The room's world-space bounding box.")]
    [SerializeField] private Bounds roomBounds = new Bounds(Vector3.zero, new Vector3(30f, 20f, 0f));

    [Header("Screen Shake")]
    [Tooltip("Multiplier applied to the intensity passed into Shake(). Tune here without touching call-sites.")]
    [SerializeField] private float shakeScale = 1f;

    [Header("Aim Lean")]
    [Tooltip("Max world-unit offset the camera leans toward the cursor while aiming.")]
    [SerializeField] private float aimMaxOffset = 6f;
    [Tooltip("Fraction of cursor world-distance the camera leads toward while aiming (0-1).")]
    [SerializeField] private float aimStrength = 0.85f;
    [Tooltip("SmoothDamp time for the aim lean easing in.")]
    [SerializeField] private float aimSmoothTime = 0.2f;
    [Tooltip("SmoothDamp time for the aim lean returning to center (dash / aim release). Keep short.")]
    [SerializeField] private float aimReturnSmoothTime = 0.25f;

    private Camera _cam;
    public Camera Cam => _cam;
    private Rigidbody2D _targetRb;
    private float _cameraZ;

    private Vector3 _smoothVelocity;
    private Vector2 _deadzoneAnchor;

    private Vector2 _lookAheadCurrent;
    private Vector2 _lookAheadVelocity;

    private Vector2 _aimTarget;
    private Vector2 _aimCurrent;
    private Vector2 _aimVelocity;
    private bool _aimSuppressed;

    private Coroutine _shakeRoutine;
    private Coroutine _zoomRoutine;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        _cam = GetComponent<Camera>();
        _cameraZ = transform.position.z;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Start()
    {
        if (target != null) SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null) return;
        PushDeadzone();
        SmoothToGoal();
    }

    // Slide the deadzone window so the target stays inside it.
    private void PushDeadzone()
    {
        Vector2 p = target.position;
        float dx = p.x - _deadzoneAnchor.x;
        float dy = p.y - _deadzoneAnchor.y;

        if (Mathf.Abs(dx) > deadzoneHalfW) _deadzoneAnchor.x += dx - Mathf.Sign(dx) * deadzoneHalfW;
        if (Mathf.Abs(dy) > deadzoneHalfH) _deadzoneAnchor.y += dy - Mathf.Sign(dy) * deadzoneHalfH;
    }

    private void SmoothToGoal()
    {
        // Look-ahead: shift toward velocity direction
        Vector2 vel = _targetRb != null ? _targetRb.linearVelocity : Vector2.zero;
        Vector2 targetAhead = vel.sqrMagnitude > 0.1f ? vel.normalized * lookAheadDistance : Vector2.zero;

        _lookAheadCurrent = Vector2.SmoothDamp(
            _lookAheadCurrent, targetAhead, ref _lookAheadVelocity,
            lookAheadSmoothTime, float.MaxValue, Time.unscaledDeltaTime);

        float goalX = _deadzoneAnchor.x + _lookAheadCurrent.x;
        float goalY = _deadzoneAnchor.y + _lookAheadCurrent.y;

        float aimTime = _aimTarget == Vector2.zero ? aimReturnSmoothTime : aimSmoothTime;
        _aimCurrent = Vector2.SmoothDamp(_aimCurrent, _aimTarget, ref _aimVelocity,
            aimTime, float.MaxValue, Time.unscaledDeltaTime);
        goalX += _aimCurrent.x;
        goalY += _aimCurrent.y;

        if (useBounds) ClampGoal(ref goalX, ref goalY);

        float x = Mathf.SmoothDamp(transform.position.x, goalX,
            ref _smoothVelocity.x, smoothTimeX, float.MaxValue, Time.unscaledDeltaTime);
        float y = Mathf.SmoothDamp(transform.position.y, goalY,
            ref _smoothVelocity.y, smoothTimeY, float.MaxValue, Time.unscaledDeltaTime);

        transform.position = new Vector3(x, y, _cameraZ);
    }

    private void ClampGoal(ref float x, ref float y)
    {
        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;
        x = Mathf.Clamp(x, roomBounds.min.x + halfW, roomBounds.max.x - halfW);
        y = Mathf.Clamp(y, roomBounds.min.y + halfH, roomBounds.max.y - halfH);
    }

    // ── Screen Shake ──────────────────────────────────────────────────────────
    // Applied via WaitForEndOfFrame so it sits on top of the settled camera
    // position and is never accidentally clamped by room bounds.

    public void Shake(float intensity, float duration = 0.3f)
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine(intensity * shakeScale, duration));
    }

    private IEnumerator ShakeRoutine(float amplitude, float duration)
    {
        var eof = new WaitForEndOfFrame();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float strength = amplitude * (1f - Mathf.Clamp01(elapsed / duration));
            transform.position += new Vector3(
                Random.Range(-1f, 1f) * strength,
                Random.Range(-1f, 1f) * strength,
                0f);
            yield return eof;
            elapsed += Time.unscaledDeltaTime;
        }
        _shakeRoutine = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null) SnapToTarget();
    }

    public void SetBounds(Bounds bounds) { roomBounds = bounds; useBounds = true; }
    public void ClearBounds() => useBounds = false;

    public void SetZoom(float targetOrthoSize, float duration)
    {
        if (_zoomRoutine != null) StopCoroutine(_zoomRoutine);
        _zoomRoutine = StartCoroutine(ZoomRoutine(targetOrthoSize, duration));
    }

    private IEnumerator ZoomRoutine(float targetSize, float duration)
    {
        float start = _cam.orthographicSize;
        float dur = Mathf.Max(0.001f, duration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _cam.orthographicSize = Mathf.Lerp(start, targetSize, t / dur);
            yield return null;
        }
        _cam.orthographicSize = targetSize;
        _zoomRoutine = null;
    }

    public void SetAim(bool aiming, Vector2 worldOffset)
    {
        if (!aiming) _aimSuppressed = false; // release resets the latch
        _aimTarget = aiming && !_aimSuppressed
            ? Vector2.ClampMagnitude(worldOffset * aimStrength, aimMaxOffset)
            : Vector2.zero;
    }

    // Dash recenters the camera; the lean stays off until aim is released and re-pressed.
    public void CancelAim()
    {
        _aimSuppressed = true;
        _aimTarget = Vector2.zero;
    }

    private void SnapToTarget()
    {
        _targetRb = target.GetComponent<Rigidbody2D>();
        _deadzoneAnchor = target.position;
        _lookAheadCurrent = Vector2.zero;
        _smoothVelocity = Vector3.zero;
        _aimCurrent = Vector2.zero;
        _aimVelocity = Vector2.zero;
        _aimTarget = Vector2.zero;
        transform.position = new Vector3(target.position.x, target.position.y, _cameraZ);
    }

    private void OnDrawGizmosSelected()
    {
        // Green box = deadzone window
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.5f);
        Gizmos.DrawWireCube(new Vector3(_deadzoneAnchor.x, _deadzoneAnchor.y, 0f),
            new Vector3(deadzoneHalfW * 2f, deadzoneHalfH * 2f, 0f));

        if (useBounds)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
            Gizmos.DrawWireCube(roomBounds.center, roomBounds.size);
        }
    }
}
