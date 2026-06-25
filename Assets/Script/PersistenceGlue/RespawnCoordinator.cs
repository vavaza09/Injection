using System.Collections;
using UnityEngine;
using VContainer;
using Game.Persistence;
using Game.Rooms;
using Game.Components.Skills;
using System.Linq;

/// <summary>
/// Session-scoped death handler. The player now persists across scenes, so respawn no
/// longer recreates it via a scene reload — instead we load the checkpoint room through
/// <see cref="IRoomLoader"/> and revive the existing player on arrival.
/// </summary>
public class RespawnCoordinator : MonoBehaviour
{
    [SerializeField] private float deathDisplayDuration = 1f;
    [SerializeField] private float respawnInvincDuration = 2f;

    private Player _player;
    private SaveService _saveService;
    private IRoomLoader _roomLoader;
    private IEnergyStore _energy;

    [Inject]
    public void Construct(Player player, SaveService saveService, IRoomLoader roomLoader, IEnergyStore energy)
    {
        _player = player;
        _saveService = saveService;
        _roomLoader = roomLoader;
        _energy = energy;
    }

    private void Start()
    {
        if (_player != null)
            _player.Died += OnPlayerDied;
    }

    private void OnDestroy()
    {
        if (_player != null)
            _player.Died -= OnPlayerDied;
    }

    private void OnPlayerDied(character _)
    {
        StartCoroutine(RespawnSequence());
    }

    private IEnumerator RespawnSequence()
    {
        yield return new WaitForSecondsRealtime(deathDisplayDuration);
        // Time is already restored by Player.OnStateEnter(Dead) -> ISlowMotionController.ResetImmediate().

        var data = _saveService?.Load();

        // No checkpoint reached yet: place at the current scene's default spawn point.
        if (data == null || string.IsNullOrEmpty(data.checkpoint?.roomId))
        {
            if (ScreenFader.Instance != null)
            {
                bool faded = false;
                ScreenFader.Instance.FadeOut(() => faded = true);
                yield return new WaitUntil(() => faded);
            }

            var spawnPoints = Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (spawnPoints.Length > 0 && _player != null)
                _player.transform.position = spawnPoints[0].Position;
            _player?.Respawn(respawnInvincDuration);
            ScreenFader.Instance?.FadeIn();
            yield break;
        }

        int energy = data.checkpoint.energy;
        _roomLoader.LoadRoom(data.checkpoint.roomId, data.checkpoint.spawnPointId, onArrived: () =>
        {
            _player?.Respawn(respawnInvincDuration);
            _energy?.SetCurrent(energy);
        });
    }
}
