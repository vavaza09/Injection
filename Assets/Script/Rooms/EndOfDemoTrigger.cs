using UnityEngine;

// One-shot trigger: when the player enters, shows the end-of-demo screen.
// Place at whatever point marks the end of playable content.
[RequireComponent(typeof(Collider2D))]
public class EndOfDemoTrigger : MonoBehaviour
{
    [SerializeField] private EndOfDemoController controller;

    private bool _triggered;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;
        if (controller == null)
        {
            Debug.LogWarning("[EndOfDemoTrigger] controller is not assigned.", this);
            return;
        }

        _triggered = true;
        controller.Show();
    }
}
