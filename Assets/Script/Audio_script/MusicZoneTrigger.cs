using UnityEngine;
using Game.Rooms;

// Starts the current scene's mapped music when the player enters this trigger collider.
// Use in scenes whose SoundManager entry is set to MusicStartMode.OnPlayerTrigger.
// Place on a GameObject with a Collider2D set as a trigger.
[RequireComponent(typeof(Collider2D))]
public class MusicZoneTrigger : MonoBehaviour
{
    [Tooltip("Only fire once. Untick to re-trigger every time the player enters.")]
    [SerializeField] private bool once = true;

    private bool _fired;
    private IRoomLoader _roomLoader;

    private void Start()
    {
        _roomLoader = FindFirstObjectByType<RoomManager>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_roomLoader != null && _roomLoader.IsTransitioning) return;
        if (_fired && once) return;
        // Player lives on the Cape layer (10) — match by component, not tag/layer.
        if (other.GetComponentInParent<Player>() == null) return;

        SoundManager.PlayCurrentSceneMusic();
        _fired = true;
    }
}
