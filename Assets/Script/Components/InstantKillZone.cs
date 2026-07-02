using UnityEngine;

public class InstantKillZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            other.GetComponent<character>()?.Die();
            return;
        }

        other.GetComponent<Enemy>()?.Die();
    }
}
