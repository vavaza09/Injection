using UnityEngine;
using UnityEngine.Events;
using Game.Rooms;

namespace Game.Rooms.Objectives
{
    public abstract class RoomObjective : MonoBehaviour
    {
        [SerializeField] private string saveId;
        [SerializeField] private UnityEvent onCompleted;

        private RoomObjectiveManager _manager;
        private bool _done;

        public string SaveId => saveId;

        public void Bind(RoomObjectiveManager manager)
        {
            _manager = manager;

            if (!string.IsNullOrEmpty(saveId) && manager.IsPersistentlyComplete(saveId))
            {
                _done = true;
                onCompleted?.Invoke();
                manager.NotifyCompleted(this);
                return;
            }

            OnBind();
        }

        // Subclasses override to start tracking their condition.
        protected abstract void OnBind();

        protected void Complete()
        {
            if (_done) return;
            _done = true;

            onCompleted?.Invoke();
            _manager?.NotifyCompleted(this);

            if (!string.IsNullOrEmpty(saveId))
                _manager?.PersistComplete(saveId);
        }
    }
}
