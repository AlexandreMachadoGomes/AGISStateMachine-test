/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Objects
{
    using Opsive.UltimateCharacterController.Character;
    using Opsive.UltimateCharacterController.Items.Actions;
    using Opsive.UltimateCharacterController.Items;
    using UnityEngine;

    /// <summary>
    /// Interface for an object that shows when the weapon is fired.
    /// </summary>
    public interface IMuzzleFlash
    {
        /// <summary>
        /// A weapon has been fired and the muzzle flash needs to show.
        /// </summary>
        /// <param name="characterItem">The item that the muzzle flash is attached to.</param>
        /// <param name="itemActionID">The ID which corresponds to the ItemAction that spawned the muzzle flash.</param>
        /// <param name="perspectiveLocation">The muzzle flash location where it should be placed if the perspective changes.</param>
        /// <param name="pooled">Is the muzzle flash pooled?</param>
        /// <param name="characterLocomotion">The character that the muzzle flash is attached to.</param>
        public void Show(CharacterItem characterItem, int itemActionID, IPerspectiveProperty<Transform> perspectiveLocation, bool pooled, UltimateCharacterLocomotion characterLocomotion);
    }
}