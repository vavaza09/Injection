using System.Collections.Generic;
using UnityEngine;

public class DashFocusMarkerSystem : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private GameObject focusMarkerPrefab;
    [SerializeField] private float detectRadius = 8f;

    private bool _wasAiming;
    private readonly Dictionary<Transform, GameObject> _activeMarkers = new Dictionary<Transform, GameObject>();

    private void Update()
    {
        if (player == null || focusMarkerPrefab == null) return;

        bool isAiming = player.IsAimingDash;

        if (isAiming && !_wasAiming)
            ShowMarkers();
        else if (!isAiming && _wasAiming)
            HideMarkers();
        else if (isAiming)
            RefreshBossMarkers();

        _wasAiming = isAiming;
    }

    private void ShowMarkers()
    {
        foreach (EnemyWeakPoint wp in FindObjectsOfType<EnemyWeakPoint>())
        {
            if (_activeMarkers.ContainsKey(wp.transform)) continue;
            if (Vector2.Distance(transform.position, wp.transform.position) > detectRadius) continue;

            _activeMarkers[wp.transform] = Instantiate(focusMarkerPrefab, wp.transform.position, Quaternion.identity, wp.transform);
        }

        foreach (BossWeakPoint bwp in FindObjectsOfType<BossWeakPoint>())
        {
            if (!bwp.IsVisible || bwp.IsDestroyed) continue;
            if (_activeMarkers.ContainsKey(bwp.transform)) continue;
            if (Vector2.Distance(transform.position, bwp.transform.position) > detectRadius) continue;

            _activeMarkers[bwp.transform] = Instantiate(focusMarkerPrefab, bwp.transform.position, Quaternion.identity, bwp.transform);
        }
    }

    // Boss weak points toggle visibility at runtime — add/remove markers as they open/close.
    private void RefreshBossMarkers()
    {
        foreach (BossWeakPoint bwp in FindObjectsOfType<BossWeakPoint>())
        {
            bool shouldShow = bwp.IsVisible && !bwp.IsDestroyed
                && Vector2.Distance(transform.position, bwp.transform.position) <= detectRadius;

            bool hasMarker = _activeMarkers.ContainsKey(bwp.transform);

            if (shouldShow && !hasMarker)
            {
                _activeMarkers[bwp.transform] = Instantiate(focusMarkerPrefab, bwp.transform.position, Quaternion.identity, bwp.transform);
            }
            else if (!shouldShow && hasMarker)
            {
                if (_activeMarkers[bwp.transform] != null)
                    Destroy(_activeMarkers[bwp.transform]);
                _activeMarkers.Remove(bwp.transform);
            }
        }
    }

    private void HideMarkers()
    {
        foreach (GameObject marker in _activeMarkers.Values)
        {
            if (marker != null) Destroy(marker);
        }
        _activeMarkers.Clear();
    }

    private void OnDisable()
    {
        HideMarkers();
    }
}
