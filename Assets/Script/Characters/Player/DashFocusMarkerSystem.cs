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

            var refCol = wp.WeakPointColliders.Count > 0 ? wp.WeakPointColliders[0] : wp.GetComponent<Collider2D>();
            Vector3 spawnPos = refCol != null ? new Vector3(refCol.bounds.center.x, refCol.bounds.center.y, wp.transform.position.z) : wp.transform.position;
            var marker = Instantiate(focusMarkerPrefab, spawnPos, Quaternion.identity, wp.transform);
            _activeMarkers[wp.transform] = marker;
            ScaleMarkerToWeakpoint(marker, refCol, 0.8f);
        }

        foreach (BossWeakPoint bwp in FindObjectsOfType<BossWeakPoint>())
        {
            if (!bwp.IsVisible || bwp.IsDestroyed) continue;
            if (_activeMarkers.ContainsKey(bwp.transform)) continue;
            if (Vector2.Distance(transform.position, bwp.transform.position) > detectRadius) continue;

            var refCol = bwp.GetComponent<Collider2D>();
            Vector3 spawnPos = refCol != null ? new Vector3(refCol.bounds.center.x, refCol.bounds.center.y, bwp.transform.position.z) : bwp.transform.position;
            var marker = Instantiate(focusMarkerPrefab, spawnPos, Quaternion.identity, bwp.transform);
            _activeMarkers[bwp.transform] = marker;
            ScaleMarkerToWeakpoint(marker, refCol, 1.5f);
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
                var refCol = bwp.GetComponent<Collider2D>();
                Vector3 spawnPos = refCol != null ? new Vector3(refCol.bounds.center.x, refCol.bounds.center.y, bwp.transform.position.z) : bwp.transform.position;
                var marker = Instantiate(focusMarkerPrefab, spawnPos, Quaternion.identity, bwp.transform);
                _activeMarkers[bwp.transform] = marker;
                ScaleMarkerToWeakpoint(marker, refCol, 1.5f);
            }
            else if (!shouldShow && hasMarker)
            {
                if (_activeMarkers[bwp.transform] != null)
                    Destroy(_activeMarkers[bwp.transform]);
                _activeMarkers.Remove(bwp.transform);
            }
        }
    }

    private void ScaleMarkerToWeakpoint(GameObject marker, Collider2D referenceCollider, float multiplier)
    {
        if (referenceCollider == null) return;
        var sr = marker.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        float spriteWorldSize = sr.sprite.bounds.size.x;
        float weakpointWorldDiameter = referenceCollider.bounds.size.x;
        float parentLossyScale = marker.transform.parent != null ? marker.transform.parent.lossyScale.x : 1f;
        float localScale = (weakpointWorldDiameter * multiplier) / (spriteWorldSize * parentLossyScale);
        marker.transform.localScale = Vector3.one * localScale;
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
