using UnityEngine;

public class RoomLockTrigger : MonoBehaviour
{
    [SerializeField] private Transform roomAnchor;
    [SerializeField] private float _roomOrthoSize = 10f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (CameraManager.instance == null) return;
        CameraManager.instance.EnterRoomLock(roomAnchor);
        CameraManager.instance.SetRoomOrthoSize(_roomOrthoSize);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (CameraManager.instance == null) return;
        CameraManager.instance.ExitRoomLock();
        CameraManager.instance.ResetRoomOrthoSize();
    }
}