using System.Collections.Generic;
using Game.Components.Movement;
using Game.Components.Skills;
using Game.UI.Health;
using Game.UI.Movement;
using Game.UI.Skills;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private HealthSlotDisplay healthSlots;
        [SerializeField] private float healHoldDuration = 2f;

        [Header("Speed")]
        [SerializeField] private SpeedTextDisplay     speedText;
        [SerializeField] private SpeedDashboardDisplay speedDashboard;

        [Header("Skills")]
        [SerializeField] private SkillReadyDisplay empDisplay;
        [SerializeField] private SkillReadyDisplay trueDamageDisplay;

        private Player                  _player;
        private ISkillReadinessProvider _skills;
        private MovementSpeedProvider   _speedProvider;
        private readonly List<ISpeedDisplay> _speedDisplays = new List<ISpeedDisplay>();

        private bool  _invincible;
        private float _healHideAt;

        [Inject]
        public void Construct(Player player, ISkillReadinessProvider skills)
        {
            _player = player;
            _skills = skills;
        }

        private void Start()
        {
            if (_player == null) return;

            var mc = _player.GetComponent<MovementComponent>();
            if (mc != null)
            {
                _speedProvider = new MovementSpeedProvider(mc);
                if (speedText      != null) _speedDisplays.Add(speedText);
                if (speedDashboard != null) _speedDisplays.Add(speedDashboard);
            }

            _player.Damaged             += OnDamaged;
            _player.Healed              += OnHealed;
            _player.Died                += OnHealthChanged;
            _player.InvincibilityStarted += OnInvincStart;
            _player.InvincibilityEnded   += OnInvincEnd;

            _healHideAt = float.MinValue;
            RefreshHealth(null);
        }

        private void OnDestroy()
        {
            if (_player == null) return;
            _player.Damaged             -= OnDamaged;
            _player.Healed              -= OnHealed;
            _player.Died                -= OnHealthChanged;
            _player.InvincibilityStarted -= OnInvincStart;
            _player.InvincibilityEnded   -= OnInvincEnd;
        }

        private void Update()
        {
            if (_speedProvider != null)
            {
                _speedProvider.Tick();
                foreach (var d in _speedDisplays)
                    d.Refresh(_speedProvider);
            }

            if (_skills != null)
            {
                empDisplay?.Refresh(_skills.Get(SkillId.Emp));
                trueDamageDisplay?.Refresh(_skills.Get(SkillId.TrueDamage));
            }

            bool wantVisible = _invincible || Time.time < _healHideAt;
            healthSlots?.Show(wantVisible);
        }

        private void OnInvincStart() => _invincible = true;
        private void OnInvincEnd()   => _invincible = false;

        private void OnDamaged(character _)     => RefreshHealth(_);
        private void OnHealthChanged(character _) => RefreshHealth(_);

        private void OnHealed(character _)
        {
            RefreshHealth(_);
            _healHideAt = Time.time + healHoldDuration;
        }

        private void RefreshHealth(character _)
        {
            if (_player == null || healthSlots == null) return;
            healthSlots.Refresh(_player.GetRemainingHits(), _player.GetMaxHits());
        }
    }
}
