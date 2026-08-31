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
    private RoomCatalog _catalog;
    private IEnergyStore _energy;

    [Inject]
    public void Construct(Player player, SaveService saveService, IRoomLoader roomLoader,
        RoomCatalog catalog, IEnergyStore energy)
    {
        _player = player;
        _saveService = saveService;
        _roomLoader = roomLoader;
        _catalog = catalog;
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

        // A room transition may still be mid-flight (e.g. died right after a portal, or while
        // the arrival fade-in was still playing). LoadRoom silently no-ops while
        // IsTransitioning and never calls onArrived, which would leave the player stuck dead
        // forever — always wait it out first.
        if (_roomLoader != null)
            yield return new WaitUntil(() => !_roomLoader.IsTransitioning);

        // Dying in the boss room always reloads it fresh (fully re-instantiates the scene) instead
        // of just repositioning the player in place — boss health, weakpoints, spawned junk/gas
        // clouds, caged enemies, room-lock walls, everything resets like the fight never happened.
        // Takes priority over the generic checkpoint respawn below, which only repositions the
        // player within whatever scene is already loaded.
        if (_roomLoader != null && _roomLoader.CurrentRoomId == "boss")
        {
            _roomLoader.LoadRoom("boss", "Room_Boss", onArrived: () =>
            {
                _player?.Respawn(respawnInvincDuration);
            });
            yield break;
        }

        var data = _saveService?.Load();

        // A checkpoint takes priority over a plain room reload — it reloads its own room too,
        // so enemies come back fresh either way, but it also restores checkpoint energy.
        if (data != null && !string.IsNullOrEmpty(data.checkpoint?.roomId))
        {
            int energy = data.checkpoint.energy;
            _roomLoader.LoadRoom(data.checkpoint.roomId, data.checkpoint.spawnPointId, onArrived: () =>
            {
                _player?.Respawn(respawnInvincDuration);
                _energy?.SetCurrent(energy);
            });
            yield break;
        }

        // No checkpoint reached yet (the case for every shipping room right now): reload the
        // room the player died in so RoomSpawner re-spawns every enemy fresh from its markers.
        RoomDefinition def = null;
        string roomId = _roomLoader?.CurrentRoomId;
        bool canReload = _roomLoader != null
                         && _catalog != null
                         && _catalog.TryGet(roomId, out def)
                         && !def.skipRoomReloadOnDeath;

        if (canReload)
        {
            // RoomAutosaveOnEntry writes lastRoom every time this room was actually entered
            // via a portal; fall back to the project's spawnPointId == sceneName convention
            // (e.g. dev pressed Play directly inside the room scene).
            bool haveEntry = data?.lastRoom != null
                             && data.lastRoom.roomId == roomId
                             && !string.IsNullOrEmpty(data.lastRoom.spawnPointId);
            string spawnId = haveEntry ? data.lastRoom.spawnPointId : def.sceneName;

            _roomLoader.LoadRoom(roomId, spawnId, onArrived: () =>
            {
                _player?.Respawn(respawnInvincDuration);
            });
            yield break;
        }

        // Room opted out of reload (tutorial) or couldn't be resolved (CurrentRoomId is null
        // when a dev presses Play directly inside a room scene, skipping Bootstrap) — never
        // leave the player stuck dead, fall back to repositioning in place.
        yield return RespawnInPlace(data, roomId);
    }

    private IEnumerator RespawnInPlace(SaveData data, string roomId)
    {
        if (ScreenFader.Instance != null)
        {
            bool faded = false;
            ScreenFader.Instance.FadeOut(() => faded = true);
            yield return new WaitUntil(() => faded);
        }

        var spawnPoints = Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (spawnPoints.Length > 0 && _player != null)
        {
            // FindObjectsSortMode.None means [0] is arbitrary whenever a room has more than
            // one SpawnPoint (e.g. 0_TutorialLevel has 4) — prefer the id the player actually
            // last entered through, when known.
            Vector3 target = spawnPoints[0].Position;
            string wanted = (data?.lastRoom != null && data.lastRoom.roomId == roomId)
                ? data.lastRoom.spawnPointId
                : null;

            if (!string.IsNullOrEmpty(wanted))
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    if (spawnPoints[i].Id == wanted)
                    {
                        target = spawnPoints[i].Position;
                        break;
                    }
                }
            }

            _player.transform.position = target;
        }

        _player?.Respawn(respawnInvincDuration);
        ScreenFader.Instance?.FadeIn();
    }
}
