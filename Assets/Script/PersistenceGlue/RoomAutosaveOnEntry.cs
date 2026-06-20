using UnityEngine;
using VContainer;
using Game.Persistence;
using Game.Rooms;

/// <summary>
/// Session-scoped. Writes only the <c>lastRoom</c> anchor whenever a room is entered, so
/// "Continue" resumes where the player last was. Deliberately does NOT touch the
/// checkpoint anchor — save points remain the death-respawn authority.
/// </summary>
public class RoomAutosaveOnEntry : MonoBehaviour
{
    private SaveService _saveService;
    private IRoomLoader _roomLoader;
    private bool _subscribed;

    [Inject]
    public void Construct(SaveService saveService, IRoomLoader roomLoader)
    {
        _saveService = saveService;
        _roomLoader = roomLoader;
    }

    // Subscribe in Start (after [Inject] has run during the scope build).
    private void Start()
    {
        if (_roomLoader != null)
        {
            _roomLoader.RoomEntered += OnRoomEntered;
            _subscribed = true;
        }
    }

    private void OnDestroy()
    {
        if (_subscribed && _roomLoader != null)
            _roomLoader.RoomEntered -= OnRoomEntered;
    }

    private void OnRoomEntered(string roomId, string spawnPointId)
    {
        if (_saveService == null) return;

        var data = _saveService.Load() ?? new SaveData();
        data.lastRoom.roomId = roomId;
        data.lastRoom.spawnPointId = spawnPointId;
        _saveService.Save(data);
    }
}
