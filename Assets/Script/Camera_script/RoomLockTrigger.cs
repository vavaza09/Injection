using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using Game.Rooms;

public class RoomLockTrigger : MonoBehaviour
{
    [System.Serializable]
    public struct WallEntry
    {
        [Tooltip("Wall prefab with a solid (non-trigger) Collider2D.")]
        public GameObject wallPrefab;
        [Tooltip("Where to instantiate the wall — place this empty at the sealed position.")]
        public Transform marker;
    }

    [Header("Trigger")]
    [Tooltip("Untick when another system (e.g. a boss intro) should call Lock() itself instead of this collider firing it.")]
    [SerializeField] private bool lockOnPlayerEnter = true;

    [Header("Camera")]
    [Tooltip("The room's virtual camera. Auto-found if left empty.")]
    [SerializeField] private CinemachineCamera roomVcam;
    [Tooltip("Empty GameObject placed where the camera should center when the room locks.")]
    [SerializeField] private Transform roomAnchor;
    [SerializeField] private float _roomOrthoSize = 10f;
    [SerializeField] private float zoomDuration = 0.4f;
    [Tooltip("Seconds for the camera to glide to the room anchor center on lock.")]
    [SerializeField] private float panDuration = 2.0f;

    [Header("Enemies")]
    [Tooltip("All enemies that must be destroyed to unlock the room.")]
    [SerializeField] private GameObject[] targetEnemies;

    [Header("Walls")]
    [SerializeField] private WallEntry[] walls;

    [Header("Impact VFX")]
    [Tooltip("StompImpactVFX prefab from Assets/Enemy/Boss/StompImpactVFX.prefab")]
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private float impactVfxRadius = 2f;

    [Header("Camera Shake")]
    [SerializeField] private float shakeIntensity = 0.6f;
    [SerializeField] private float shakeDuration = 0.3f;

    private readonly List<GameObject> _spawnedWalls = new List<GameObject>();
    private bool _locked;
    private bool _cameraReleased;
    private IRoomLoader _roomLoader;
    private Player _player;

    // Cached camera state for restore
    private Transform _cachedFollow;
    private Transform _cachedLookAt;
    private float _cachedOrthoSize;
    private Vector3 _cachedComposerOffset;

    private Coroutine _zoomRoutine;
    private Coroutine _panRoutine;
    private Transform _panProxy;
    private CinemachinePositionComposer _composer;

    private void Start()
    {
        if (roomVcam == null)
            roomVcam = FindObjectOfType<CinemachineCamera>();

        if (roomVcam != null)
            _composer = roomVcam.GetComponent<CinemachinePositionComposer>();

        _roomLoader = FindFirstObjectByType<RoomManager>();

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        _player = playerGo != null ? playerGo.GetComponent<Player>() : null;
        if (_player != null) _player.Died += OnPlayerDied;
    }

    private void OnDestroy()
    {
        if (_panProxy != null) Destroy(_panProxy.gameObject);
        if (_player != null) _player.Died -= OnPlayerDied;
    }

    // Deliberately NOT wired to BossBase.Defeated: that event fires the instant HP hits 0,
    // which for a boss with a BossDeathSequence is ~10 seconds before its death animation
    // actually finishes and the boss GameObject is destroyed. Releasing the camera that early
    // snapped it back to the player mid-explosion, while the gates (opened by WatchEnemyRoutine
    // below, which correctly waits for real destruction) stayed sealed for the rest of the
    // sequence. WatchEnemyRoutine is the single source of truth for "boss is actually gone" —
    // both the camera and the gates release together, once, at the right time.

    // Player death doesn't defeat the boss, so WatchEnemyRoutine never fires — without this the
    // camera stays locked on the room anchor through the death/respawn beat instead of
    // following the player. Restores directly (not via ReleaseCamera/_cameraReleased) so the
    // room stays logically locked and a later real boss-defeat still restores correctly.
    private void OnPlayerDied(character _)
    {
        if (!_locked) return;
        RestoreCamera();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!lockOnPlayerEnter) return;
        if (_locked) return;
        if (_roomLoader != null && _roomLoader.IsTransitioning) return;
        if (!other.CompareTag("Player")) return;

        Lock();
    }

    // Public so an external sequencer (e.g. a boss intro) can seal the room on its own cue
    // instead of relying on this collider — set lockOnPlayerEnter false on those triggers.
    public void Lock()
    {
        if (_locked) return;
        _locked = true;

        LockCamera();
        SpawnWalls();
        StartCoroutine(WatchEnemyRoutine());
    }

    private void LockCamera()
    {
        if (roomVcam == null) return;

        _cachedFollow = roomVcam.Follow;
        _cachedLookAt = roomVcam.LookAt;
        _cachedOrthoSize = roomVcam.Lens.OrthographicSize;

        if (_composer != null)
            _cachedComposerOffset = _composer.TargetOffset;

        CameraManager.instance?.SuspendOffsetControl();
        // CameraManager does not exist in any practice/ room scene, so the call above is a silent
        // no-op there. Broadcast to the foresight extensions directly: a room lock owns the framing
        // outright, and a broadcast reaches whichever vcam is actually live (roomVcam can resolve to
        // the wrong one in the scenes that have more than one).
        CameraForesightExtension.SuspendAll();

        if (roomAnchor != null)
        {
            if (_panProxy == null)
                _panProxy = new GameObject("RoomLockPanProxy").transform;

            // Start the proxy at the current camera world position
            Vector3 startPos = roomVcam.transform.position;
            _panProxy.position = startPos;

            roomVcam.Follow = _panProxy;
            roomVcam.LookAt = _panProxy;

            if (_panRoutine != null) StopCoroutine(_panRoutine);
            Vector3 target = roomAnchor.position;
            target.z = startPos.z;
            _panRoutine = StartCoroutine(PanRoutine(target));
        }

        StartZoom(_roomOrthoSize);
    }

    private IEnumerator PanRoutine(Vector3 targetPos)
    {
        Vector3 startPos = _panProxy.position;
        Vector3 startOffset = _composer != null ? _composer.TargetOffset : Vector3.zero;
        float t = 0f;
        float dur = Mathf.Max(0.001f, panDuration);

        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            _panProxy.position = Vector3.Lerp(startPos, targetPos, k);
            if (_composer != null)
                _composer.TargetOffset = Vector3.Lerp(startOffset, Vector3.zero, k);
            yield return null;
        }

        _panProxy.position = targetPos;
        if (_composer != null)
            _composer.TargetOffset = Vector3.zero;

        _panRoutine = null;
    }

    private void RestoreCamera()
    {
        if (roomVcam == null) return;

        if (_panRoutine != null) { StopCoroutine(_panRoutine); _panRoutine = null; }

        roomVcam.Follow = _cachedFollow;
        roomVcam.LookAt = _cachedLookAt;

        if (_composer != null)
            _composer.TargetOffset = _cachedComposerOffset;

        CameraManager.instance?.ResumeOffsetControl();
        // Idempotent by design: RestoreCamera can genuinely run twice (OnPlayerDied, then later
        // OpenRoom -> ReleaseCamera), so this must not be counter-based.
        CameraForesightExtension.ResumeAll();

        StartZoom(_cachedOrthoSize);
    }

    private void StartZoom(float targetSize)
    {
        if (_zoomRoutine != null) StopCoroutine(_zoomRoutine);
        _zoomRoutine = StartCoroutine(ZoomRoutine(targetSize));
    }

    private IEnumerator ZoomRoutine(float targetSize)
    {
        float start = roomVcam.Lens.OrthographicSize;
        float t = 0f;
        float dur = Mathf.Max(0.001f, zoomDuration);
        while (t < dur)
        {
            t += Time.deltaTime;
            var lens = roomVcam.Lens;
            lens.OrthographicSize = Mathf.Lerp(start, targetSize, t / dur);
            roomVcam.Lens = lens;
            yield return null;
        }
        var final = roomVcam.Lens;
        final.OrthographicSize = targetSize;
        roomVcam.Lens = final;
        _zoomRoutine = null;
    }

    private void SpawnWalls()
    {
        foreach (var entry in walls)
        {
            if (entry.wallPrefab == null || entry.marker == null) continue;

            var wall = Instantiate(entry.wallPrefab, entry.marker.position, entry.marker.rotation);
            _spawnedWalls.Add(wall);

            if (impactVfxPrefab != null)
            {
                var go = Instantiate(impactVfxPrefab, entry.marker.position, Quaternion.identity);
                go.GetComponent<StompImpactVFX>()?.Initialize(impactVfxRadius);
            }
        }

        // CameraShake (Cinemachine Impulse) is scene-independent and is what every other
        // boss/player shake in the project already uses — CameraManager itself isn't present
        // in Room_Boss (or most room scenes), so routing through it was a silent no-op there.
        if (_spawnedWalls.Count > 0)
            CameraShake.Shake(shakeIntensity, shakeDuration);
    }

    private IEnumerator WatchEnemyRoutine()
    {
        bool anyAlive = true;
        while (anyAlive)
        {
            anyAlive = false;
            foreach (var e in targetEnemies)
            {
                if (e != null) { anyAlive = true; break; }
            }
            yield return null;
        }

        OpenRoom();
    }

    private void ReleaseCamera()
    {
        if (_cameraReleased) return;
        _cameraReleased = true;
        RestoreCamera();
    }

    public void OpenRoom()
    {
        ReleaseCamera();

        foreach (var wall in _spawnedWalls)
        {
            if (wall != null) Destroy(wall);
        }
        _spawnedWalls.Clear();
    }

}
