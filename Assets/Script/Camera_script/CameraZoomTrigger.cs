using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Enter zooms the camera to zoomSize; exit restores whatever ortho size it had before
/// entering. Put on a trigger Collider2D. Works with either the persistent CameraController
/// (main game) or Cinemachine (practice/test scenes) — whichever is present.
///
/// Cinemachine path resolves the currently-live vcam via CinemachineBrain each time (not a
/// cached reference) — this scene swaps between multiple CinemachineCameras by priority
/// (see CameraZoneTrigger), so grabbing "a" vcam once in Start() can silently zoom a camera
/// that isn't the one on screen.
/// </summary>
public class CameraZoomTrigger : MonoBehaviour
{
    [SerializeField] private float zoomSize = 6f;
    [SerializeField] private float zoomDuration = 0.5f;

    private float _defaultSize;
    private CinemachineCamera _activeVcam;
    private Coroutine _vcamZoomRoutine;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[CameraZoomTrigger] OnTriggerEnter2D by '{other.name}' tag='{other.tag}'");
        if (!other.CompareTag("Player")) return;
        CacheDefaultSize();
        Debug.Log($"[CameraZoomTrigger] Enter zone -> zoomSize={zoomSize}, defaultSize={_defaultSize}, CameraController={(CameraController.instance != null)}, activeVcam={(_activeVcam != null ? _activeVcam.name : "null")}");
        Zoom(zoomSize);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        Debug.Log($"[CameraZoomTrigger] Exit zone -> restoring {_defaultSize}");
        Zoom(_defaultSize);
    }

    private void CacheDefaultSize()
    {
        if (CameraController.instance != null)
        {
            _defaultSize = CameraController.instance.Cam.orthographicSize;
            return;
        }

        _activeVcam = FindActiveVcam();
        if (_activeVcam != null) _defaultSize = _activeVcam.Lens.OrthographicSize;
    }

    private void Zoom(float targetSize)
    {
        if (CameraController.instance != null)
        {
            CameraController.instance.SetZoom(targetSize, zoomDuration);
            return;
        }

        if (_activeVcam == null) _activeVcam = FindActiveVcam();
        if (_activeVcam == null) return;

        if (_vcamZoomRoutine != null) StopCoroutine(_vcamZoomRoutine);
        _vcamZoomRoutine = StartCoroutine(VcamZoomRoutine(_activeVcam, targetSize));
    }

    private static CinemachineCamera FindActiveVcam()
    {
        var brain = CinemachineBrain.GetActiveBrain(0);
        return brain != null ? brain.ActiveVirtualCamera as CinemachineCamera : null;
    }

    private IEnumerator VcamZoomRoutine(CinemachineCamera cam, float targetSize)
    {
        float start = cam.Lens.OrthographicSize;
        float dur = Mathf.Max(0.001f, zoomDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            var lens = cam.Lens;
            lens.OrthographicSize = Mathf.Lerp(start, targetSize, t / dur);
            cam.Lens = lens;
            yield return null;
        }
        var final = cam.Lens;
        final.OrthographicSize = targetSize;
        cam.Lens = final;
        _vcamZoomRoutine = null;
    }
}
