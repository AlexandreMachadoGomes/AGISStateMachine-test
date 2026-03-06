/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Character.Abilities.Starters
{
    using Opsive.Shared.Input;
    using UnityEngine;

    /// <summary>
    /// The AbilityStarter allows a custom object to decide when the ability should start.
    /// </summary>
    [System.Serializable]
    [UnityEngine.Scripting.Preserve]
    public abstract class AbilityStarter
    {
        protected GameObject m_GameObject;
        protected Ability m_Ability;

        /// <summary>
        /// Initializes the starter to the specified ability.
        /// </summary>
        /// <param name="character">The character GameObject that the starter is attached to.</param>
        /// <param name="ability">The ability that owns the starter.</param>
        public virtual void Initialize(GameObject character, Ability ability) { m_GameObject = character; m_Ability = ability; }

        /// <summary>
        /// Can the starter start the ability?
        /// </summary>
        /// <param name="playerInput">A reference to the input component.</param>
        /// <returns>True if the starter can start the ability.</returns>
        public abstract bool CanInputStartAbility(IPlayerInput playerInput);

        /// <summary>
        /// The ability has started.
        /// </summary>
        public virtual void AbilityStarted() { }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        public virtual void AbilityStopped() { }

        /// <summary>
        /// The object has been destroyed.
        /// </summary>
        public virtual void OnDestroy() { }
    }
}