using UnityEngine;

public class HitFlash : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;

    public void Flash()
    {
        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        HitFlashFX.Spawn(pos);
    }
}
