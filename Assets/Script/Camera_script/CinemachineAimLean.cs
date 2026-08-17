using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Aim-lean for practice/room scenes that use bare Cinemachine (no CameraController or
/// CameraManager wrapper). Some scenes have multiple room CinemachineCameras (one per
/// confined sub-area) — this tracks whichever one is currently live and applies the lean
/// to its CinemachinePositionComposer, same way CameraManager.ResolveCurrentCamera picks
/// the active vcam. Mirrors CameraController.SetAim/CancelAim so Player.cs can drive
/// whichever camera system exists in the loaded scene. Place on any GameObject in the scene.
/// </summary>
public class CinemachineAimLean : MonoBehaviour
{
    public static CinemachineAimLean instance { get; private set; }

    [Header("Aim Lean")]
    [Tooltip("Max world-unit offset the camera leans toward the cursor while aiming.")]
    [SerializeField] private float aimMaxOffset = 3f;
    [Tooltip("Fraction of cursor world-distance the camera leads toward while aiming (0-1).")]
    [SerializeField] private float aimStrength = 0.6f;
    [Tooltip("SmoothDamp time for the aim lean easing in.")]
    [SerializeField] private float aimSmoothTime = 1.2f;
    [Tooltip("SmoothDamp time for the aim lean returning to center (dash / aim release). Keep short.")]
    [SerializeField] private float aimReturnSmoothTime = 0.25f;

    private CinemachineCamera[] _allVirtualCameras;
    private CinemachineCamera _currentCam;
    private CinemachinePositionComposer _composer;
    private Vector3 _baseOffset;

    private Vector2 _aimTarget;
    private Vector2 _aimCurrent;
    private Vector2 _aimVelocity;
    private bool _aimSuppressed;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        ResolveLiveCamera();
        if (_composer == null) return;

        float aimTime = _aimTarget == Vector2.zero ? aimReturnSmoothTime : aimSmoothTime;
        _aimCurrent = Vector2.SmoothDamp(_aimCurrent, _aimTarget, ref _aimVelocity,
            aimTime, float.MaxValue, Time.unscaledDeltaTime);

        _composer.TargetOffset = _baseOffset + new Vector3(_aimCurrent.x, _aimCurrent.y, 0f);
    }

    // Re-picks the live vcam every frame (cheap: small fixed array, changes rarely) so scenes
    // with multiple room-confined CinemachineCameras still get the lean on whichever is active.
    private void ResolveLiveCamera()
    {
        if (_allVirtualCameras == null)
            _allVirtualCameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);

        CinemachineCamera live = null;
        foreach (var cam in _allVirtualCameras)
        {
            if (cam != null && cam.isActiveAndEnabled && cam.IsLive) { live = cam; break; }
        }
        if (live == null)
        {
            foreach (var cam in _allVirtualCameras)
            {
                if (cam != null && cam.isActiveAndEnabled) { live = cam; break; }
            }
        }

        if (live == _currentCam) return;

        _currentCam = live;
        _composer = live != null ? live.GetComponent<CinemachinePositionComposer>() : null;
        _baseOffset = _composer != null ? _composer.TargetOffset : Vector3.zero;
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
}
