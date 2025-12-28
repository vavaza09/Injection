using UnityEngine;

public class CameraFollowObject : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _playerTransform;

    private void Update()
    {
        if (_playerTransform == null) return;
        transform.position = _playerTransform.position;
    }
}
