using UnityEngine;
using VContainer;
using Core.Logging;
using Game.Persistence;
using Game.Rooms;
using Game.Components.Skills;

/// <summary>
/// Runs once from the Bootstrap scene. Decides the first room to load:
/// continue from the saved <c>lastRoom</c> (restoring checkpoint energy), or — with no
/// save — the catalog's default room. Owns the boot path only; per-room placement is
/// <see cref="RoomManager"/>'s job (this replaces the old SaveApplier boot role).
/// </summary>
public class SaveBootstrapper : MonoBehaviour
{
    private SaveService _saveService;
    private IRoomLoader _roomLoader;
    private RoomCatalog _catalog;
    private IEnergyStore _energy;
    private Core.Logging.ILogger _logger;

    [Inject]
    public void Construct(SaveService saveService, IRoomLoader roomLoader, RoomCatalog catalog,
        IEnergyStore energy, LoggerFactory loggerFactory)
    {
        _saveService = saveService;
        _roomLoader = roomLoader;
        _catalog = catalog;
        _energy = energy;
        _logger = loggerFactory?.CreateLogger("SaveBootstrapper");
    }

    private void Start()
    {
        var data = _saveService?.Load();

        if (data != null && !string.IsNullOrEmpty(data.lastRoom?.roomId))
        {
            int energy = data.checkpoint?.energy ?? 0;
            _logger?.Log($"[SaveBootstrapper] Continue -> room '{data.lastRoom.roomId}'.");
            _roomLoader.LoadRoom(data.lastRoom.roomId, data.lastRoom.spawnPointId,
                onArrived: () => _energy?.SetCurrent(energy));
            return;
        }

        if (_catalog != null && !string.IsNullOrEmpty(_catalog.DefaultRoomId))
        {
            _logger?.Log($"[SaveBootstrapper] Fresh start -> room '{_catalog.DefaultRoomId}'.");
            _roomLoader.LoadRoom(_catalog.DefaultRoomId, _catalog.DefaultSpawnPointId);
        }
        else
        {
            _logger?.LogError("[SaveBootstrapper] No RoomCatalog/default room — cannot boot.");
            ScreenFader.Instance?.FadeIn();
        }
    }
}
