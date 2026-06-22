using System;
using UnityEngine;
using Game.Characters.Player;
using Game.Components.Skills;

namespace Game.Tutorial
{
    public enum SeparatorAfter { None, Plus, Then, Or }

    [Serializable]
    public class PromptEntry
    {
        [Tooltip("Action key resolved by TutorialPromptUI bindings (e.g. \"Jump\", \"Dash\").")]
        public string actionKey;
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

        public string Description => description;
        public PromptEntry[] PromptKeys => promptKeys;
        public string AbilityToUnlock => abilityToUnlock;

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
        }

        /// <summary>Subclasses call this when the objective is satisfied.</summary>
        protected void Complete()
        {
            if (!IsActive) return;
            IsActive = false;
            OnCleanup();
            Completed?.Invoke();
        }

        protected virtual void OnBegin() { }
        protected virtual void OnCleanup() { }
    }
}
