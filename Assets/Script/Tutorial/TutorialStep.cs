using System;
using UnityEngine;
using Game.Characters.Player;
using Game.Components.Skills;
using Game.Tutorial.Navigator;

namespace Game.Tutorial
{
    public enum SeparatorAfter { None, Plus, Then, Or }

    [Serializable]
    public class PromptEntry
    {
        [Tooltip("Action key resolved by TutorialPromptUI bindings (e.g. \"Jump\", \"Dash\"). Leave empty to show 'text' as a plain label instead.")]
        public string actionKey;
        [Tooltip("Plain text shown as a label chip when actionKey is empty (e.g. \"then\", \"and\", \"or hold\"). Leave both actionKey and text empty for a blank spacer.")]
        public string text;
        [Tooltip("Width in pixels of the blank spacer inserted when both actionKey and text are empty.")]
        public float spacerWidth = 24f;
        [Tooltip("Separator shown after this chip. None = last chip in the row.")]
        public SeparatorAfter separatorAfter = SeparatorAfter.None;
    }

    /// <summary>Shared services handed to every step when it begins.</summary>
    public class TutorialContext
    {
        public Player Player;
        public PlayerInputHandler Input;
        public IPlayerSkillEvents SkillEvents;
        public IEnergyStore EnergyStore;
    }

    /// <summary>
    /// Base class for one tutorial step. Place a concrete step as a component in the tutorial
    /// scene and wire its references in the inspector. The <see cref="TutorialManager"/> begins
    /// steps in order, unlocking <see cref="AbilityToUnlock"/> as each one starts, and advances
    /// when <see cref="Completed"/> fires.
    /// </summary>
    public abstract class TutorialStep : MonoBehaviour
    {
        [SerializeField, TextArea] protected string description;
        [Tooltip("Action keys shown as button glyphs in the prompt (resolved by TutorialPromptUI).")]
        [SerializeField] protected PromptEntry[] promptKeys;
        [Tooltip("Ability key to unlock when this step begins (see TutorialAbilities). Leave empty for steps that gate nothing, e.g. walk/jump.")]
        [SerializeField] protected string abilityToUnlock;

        [Header("Navigator")]
        [Tooltip("Recorded clip to play on the shadow ghost for this step. Leave empty for steps with no demonstration.")]
        [SerializeField] private NavigatorClip navigatorClip;
        [Tooltip("World-space anchor for the clip's frame-0 position. Usually a child Transform placed at the step's start point.")]
        [SerializeField] private Transform navigatorAnchor;

        [Header("Barriers")]
        [Tooltip("GameObjects that block the player from advancing past this step. They start active in the scene and are disabled when the step completes or is skipped.")]
        [SerializeField] private GameObject[] barriers;

        public string Description => description;
        public PromptEntry[] PromptKeys => promptKeys;
        public string AbilityToUnlock => abilityToUnlock;
        public NavigatorClip NavigatorClip => navigatorClip;
        public Transform NavigatorAnchor => navigatorAnchor;

        /// <summary>Raised once the step's objective is met.</summary>
        public event Action Completed;

        protected TutorialContext Context { get; private set; }
        protected bool IsActive { get; private set; }

        public void Begin(TutorialContext ctx)
        {
            Context = ctx;
            IsActive = true;
            OnBegin();
        }

        /// <summary>Called by the manager when a step is force-skipped (dev hotkey).</summary>
        public void Cleanup()
        {
            if (!IsActive) return;
            IsActive = false;
            OnCleanup();
            DeactivateBarriers();
        }

        /// <summary>Subclasses call this when the objective is satisfied.</summary>
        protected void Complete()
        {
            if (!IsActive) return;
            IsActive = false;
            OnCleanup();
            DeactivateBarriers();
            Completed?.Invoke();
        }

        private void DeactivateBarriers()
        {
            if (barriers == null) return;
            foreach (var b in barriers)
                if (b != null) b.SetActive(false);
        }

        protected virtual void OnBegin() { }
        protected virtual void OnCleanup() { }
    }
}
