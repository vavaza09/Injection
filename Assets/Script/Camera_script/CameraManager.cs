using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager instance;

    [Header("References")]
    [SerializeField] private CinemachineCamera[] _allVirtualCameras;
    [SerializeField] private Rigidbody2D _playerRb;

    [Header("Room Lock")]
    [SerializeField] private Transform cameraTarget;
    private Transform _cachedFollowTarget;
    private Transform _cachedLookAtTarget;
    private bool _isRoomLocked;

    [Header("Zoom Time")]
    [SerializeField] private float _zoomInTime = 0.35f;
    [SerializeField] private float _zoomOutTime = 0.45f;

    private float _defaultOrthoSize;
    private Coroutine _zoomRoutine;

    [Header("Fall Detection")]
    [SerializeField] private float _fallVelocityThreshold = -5f;   // start considering fall (increased threshold)
    [SerializeField] private float _commitDelay = 0.1f;            // slightly longer delay for smoother feel

    [Header("Vertical Offset")]
    [SerializeField] private float _normalOffsetY = 1f;            // look slightly above player normally
    [SerializeField] private float _fallOffsetY = -1.5f;           // subtle look down when falling (reduced from -3)

    [Header("Damping (YDamping)")]
    [SerializeField] private float _normalYDamping = 1f;           // smooth when not falling
    [SerializeField] private float _fallYDamping = 0.5f;           // moderate response when falling

    [Header("Transition Times")]
    [SerializeField] private float _downTime = 0.2f;               // slightly slower transition down
    [SerializeField] private float _upTime = 0.5f;                 // slower recovery for comfort


    private CinemachineCamera _currentCam;
    private CinemachinePositionComposer _composer;

    private Coroutine _lerpRoutine;
    private bool _isFallingCommitted;
    private float _fallTimer;

    private enum State { Normal, Falling }
    private State _state = State.Normal;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ResolveCurrentCamera();
        ApplyInstant(_normalOffsetY, _normalYDamping);

        if (_currentCam != null)
        {
            _defaultOrthoSize = _currentCam.Lens.OrthographicSize;
        }


    }

    private void Update()
    {
        if (_playerRb == null || _composer == null) return;

        float vy = _playerRb.linearVelocity.y;

        // Commit logic (prevents tiny step-downs triggering)
        bool fallingCandidate = vy < _fallVelocityThreshold;

        if (fallingCandidate)
        {
            _fallTimer += Time.deltaTime;
            if (_fallTimer >= _commitDelay)
                _isFallingCommitted = true;
        }
        else
        {
            _fallTimer = 0f;
            _isFallingCommitted = false;
        }

        State desired = _isFallingCommitted ? State.Falling : State.Normal;

        if (desired != _state)
        {
            _state = desired;

            if (_state == State.Falling)
                StartLerp(_fallOffsetY, _fallYDamping, _downTime);
            else
                StartLerp(_normalOffsetY, _normalYDamping, _upTime);
        }
    }

    private void ResolveCurrentCamera()
    {
        if (_allVirtualCameras == null || _allVirtualCameras.Length == 0)
        {
            Debug.LogWarning("CameraManager: No virtual cameras assigned!");
            return;
        }

        // Pick the enabled one
        for (int i = 0; i < _allVirtualCameras.Length; i++)
        {
            if (_allVirtualCameras[i] != null && _allVirtualCameras[i].isActiveAndEnabled)
            {
                _currentCam = _allVirtualCameras[i];
                break;
            }
        }

        if (_currentCam == null) _currentCam = _allVirtualCameras[0];

        // Get CinemachinePositionComposer (new API - replaces FramingTransposer)
        _composer = _currentCam.GetComponent<CinemachinePositionComposer>();

        if (_composer == null)
        {
            Debug.LogError("CameraManager: CinemachinePositionComposer not found on current vcam. Make sure your Cinemachine Camera has Position Composer component.");
        }
    }

    private void StartLerp(float targetOffsetY, float targetYDamping, float time)
    {
        if (_lerpRoutine != null) StopCoroutine(_lerpRoutine);
        _lerpRoutine = StartCoroutine(LerpFraming(targetOffsetY, targetYDamping, time));
    }

    private IEnumerator LerpFraming(float targetOffsetY, float targetYDamping, float time)
    {
        if (_composer == null) yield break;

        Vector3 startOffset = _composer.TargetOffset;
        Vector3 startDamping = _composer.Damping;

        float t = 0f;
        time = Mathf.Max(0.001f, time);

        while (t < time)
        {
            t += Time.deltaTime;
            float alpha = t / time;

            // Lerp the Y offset (keep X and Z the same)
            _composer.TargetOffset = new Vector3(
                startOffset.x,
                Mathf.Lerp(startOffset.y, targetOffsetY, alpha),
                startOffset.z
            );

            // Update Y damping only (keep X and Z the same)
            _composer.Damping = new Vector3(
                startDamping.x,
                Mathf.Lerp(startDamping.y, targetYDamping, alpha),
                startDamping.z
            );

            yield return null;
        }

        // Ensure final values are exact
        _composer.TargetOffset = new Vector3(_composer.TargetOffset.x, targetOffsetY, _composer.TargetOffset.z);
        _composer.Damping = new Vector3(_composer.Damping.x, targetYDamping, _composer.Damping.z);

        _lerpRoutine = null;
    }

    private void ApplyInstant(float offsetY, float yDamping)
    {
        if (_composer == null) return;

        _composer.TargetOffset = new Vector3(_composer.TargetOffset.x, offsetY, _composer.TargetOffset.z);
        _composer.Damping = new Vector3(_composer.Damping.x, yDamping, _composer.Damping.z);
    }

    // Public API for room bounds (if needed)
    public void EnterRoom(Bounds roomBounds)
    {
        // If using CinemachineConfiner2D, you would update it here
        Debug.Log($"Entered room with bounds: {roomBounds}");
    }

    // Public API for camera shake
    public void Shake(float intensity, float duration = 0.3f)
    {
        if (_currentCam != null)
        {
            var impulse = _currentCam.GetComponent<CinemachineImpulseSource>();
            if (impulse != null)
            {
                impulse.GenerateImpulse(intensity);
            }
        }
    }

    public void EnterRoomLock(Transform roomAnchor)
    {
        if (_currentCam == null || roomAnchor == null || _isRoomLocked)
        {
            return;
        }

        // Cache current follow targets
        _cachedFollowTarget = _currentCam.Follow;
        _cachedLookAtTarget = _currentCam.LookAt;

        // Lock camera to room
        _currentCam.Follow = roomAnchor;
        _currentCam.LookAt = roomAnchor;

        _isRoomLocked = true;
    }
    public void ExitRoomLock()
    {
        if (_currentCam == null || !_isRoomLocked)
        {
            return;
        }

        // Restore follow targets
        _currentCam.Follow = cameraTarget;
        _currentCam.LookAt = cameraTarget;

        _isRoomLocked = false;
    }


    private void CacheDefaultOrtho()
    {
        if (_currentCam != null)
            _defaultOrthoSize = _currentCam.Lens.OrthographicSize;
    }

    public void SetRoomOrthoSize(float newSize)
    {
        if (_currentCam == null) return;
        StartZoom(newSize, _zoomInTime);
    }

    public void ResetRoomOrthoSize()
    {
        if (_currentCam == null) return;
        StartZoom(_defaultOrthoSize, _zoomOutTime);
    }

    private void StartZoom(float targetSize, float duration)
    {
        if (_zoomRoutine != null)
        {
            StopCoroutine(_zoomRoutine);
        }
        _zoomRoutine = StartCoroutine(ZoomCoroutine(targetSize, duration));
    }

    private IEnumerator ZoomCoroutine(float targetSize, float duration)
    {
        if (_currentCam == null) yield break;
        float startSize = _currentCam.Lens.OrthographicSize;
        float t = 0f;
        duration = Mathf.Max(0.001f, duration);
        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = t / duration;
            _currentCam.Lens.OrthographicSize = Mathf.Lerp(startSize, targetSize, alpha);
            yield return null;
        }
        _currentCam.Lens.OrthographicSize = targetSize;
        _zoomRoutine = null;
    }

}
