using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VContainer;
using Core.Logging;
using Game.Persistence;
using Game.Rooms.Objectives;

namespace Game.Rooms
{
    public sealed class RoomObjectiveManager : MonoBehaviour
    {
        [SerializeField] private UnityEvent onAllObjectivesComplete;

        private SaveService _saveService;
        private Core.Logging.ILogger _logger;

        private readonly List<RoomObjective> _objectives = new List<RoomObjective>();
        private readonly Dictionary<string, KillObjective> _killObjectives = new Dictionary<string, KillObjective>();
        private int _completedCount;

        [Inject]
        public void Construct(SaveService saveService, LoggerFactory loggerFactory)
        {
            _saveService = saveService;
            _logger = loggerFactory?.CreateLogger("RoomObjectiveManager");
        }

        private void Awake()
        {
            // Pre-index KillObjectives before any Start() runs so RoomSpawner can call
            // RegisterObjectiveEnemy in its own Start() regardless of script execution order.
            var objectives = FindObjectsByType<RoomObjective>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var obj in objectives)
            {
                _objectives.Add(obj);
                if (obj is KillObjective ko && !string.IsNullOrEmpty(ko.ObjectiveId))
                    _killObjectives[ko.ObjectiveId] = ko;
            }
        }

        private void Start()
        {
            foreach (var obj in _objectives)
                obj.Bind(this);
            _logger?.Log($"[RoomObjectiveManager] Bound {_objectives.Count} objective(s).");
        }

        public void RegisterObjectiveEnemy(string objectiveId, Enemy enemy)
        {
            if (string.IsNullOrEmpty(objectiveId) || enemy == null) return;
            if (_killObjectives.TryGetValue(objectiveId, out var ko))
                ko.AddEnemy(enemy);
            else
                _logger?.LogWarning($"[RoomObjectiveManager] No KillObjective for id '{objectiveId}'.");
        }

        public void SealSpawning()
        {
            foreach (var ko in _killObjectives.Values)
                ko.Seal();
        }

        // True if the objective enemy group (by objectiveId) has been persistently completed.
        // Used by RoomSpawner to skip spawning enemies that are already done.
        public bool IsObjectiveComplete(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId)) return false;
            if (_killObjectives.TryGetValue(objectiveId, out var ko))
                return IsPersistentlyComplete(ko.SaveId);
            return false;
        }

        public bool IsPersistentlyComplete(string saveId)
        {
            if (string.IsNullOrEmpty(saveId)) return false;
            return _saveService != null && _saveService.IsObjectiveComplete(saveId);
        }

        public void PersistComplete(string saveId)
        {
            _saveService?.MarkObjectiveComplete(saveId);
        }

        public void NotifyCompleted(RoomObjective objective)
        {
            _completedCount++;
            _logger?.Log($"[RoomObjectiveManager] Objective completed ({_completedCount}/{_objectives.Count}).");
            if (_completedCount >= _objectives.Count)
                onAllObjectivesComplete?.Invoke();
        }
    }
}
