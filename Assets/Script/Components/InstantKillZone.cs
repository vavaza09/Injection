using UnityEngine;

public class InstantKillZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var player = other.GetComponent<character>();
        player?.Die();
    }
}
