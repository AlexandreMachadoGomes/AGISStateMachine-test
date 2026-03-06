/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Networking.Character
{
    using UnityEngine;

    /// <summary>
    /// Acts as a bridge between the character controller and the underlying networking implementation.
    /// </summary>
    public interface INetworkCharacter
    {
        /// <summary>
        /// Pushes the target Rigidbody in the specified direction.
        /// </summary>
        /// <param name="targetRigidbody">The Rigidbody to push.</param>
        /// <param name="force">The amount of force to apply.</param>
        /// <param name="point">The point at which to apply the push force.</param>
        void PushRigidbody(Rigidbody targetRigidbody, Vector3 force, Vector3 point);

        /// <summary>
        /// Sets the rotation of the character.
        /// </summary>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        void SetRotation(Quaternion rotation, bool snapAnimator);

        /// <summary>
        /// Sets the position of the character.
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        void SetPosition(Vector3 position, bool snapAnimator);

        /// <summary>
        /// Resets the rotation and position to their default values.
        /// </summary>
        void ResetRotationPosition();

        /// <summary>
        /// Sets the position and rotation of the character.
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        /// <param name="stopAllAbilities">Should all abilities be stopped?</param>
        void SetPositionAndRotation(Vector3 position, Quaternion rotation, bool snapAnimator, bool stopAllAbilities);

        /// <summary>
        /// Changes the character model.
        /// </summary>
        /// <param name="modelIndex">The index of the model within the ModelManager.</param>
        void ChangeModels(int modelIndex);

        /// <summary>
        /// Activates or deactivates the character.
        /// </summary>
        /// <param name="active">Is the character active?</param>
        /// <param name="uiEvent">Should the OnShowUI event be executed?</param>
        void SetActive(bool active, bool uiEvent);

        /// <summary>
        /// Executes a bool event.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="value">The bool value.</param>
        void ExecuteBoolEvent(string eventName, bool value);
    }
}