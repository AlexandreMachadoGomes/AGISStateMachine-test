#if ENABLE_INPUT_SYSTEM
/// ---------------------------------------------
/// Opsive Shared
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.Shared.Input.InputSystem
{
    using Opsive.Shared.Events;
    using UnityEngine;

    public class OnScreenControls : MonoBehaviour
    {
        [Tooltip("The character used by the on screen input. Can be null.")]
        [SerializeField] protected GameObject m_Character;

        public GameObject Character { get { return m_Character; } set { OnAttachCharacter(value); } }

        private GameObject m_CameraGameObject;

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected virtual void Awake()
        {
            if (m_Character == null) {
                var foundCamera = Shared.Camera.CameraUtility.FindCamera(null);
                if (foundCamera != null) {
                    m_CameraGameObject = foundCamera.gameObject;
                    EventHandler.RegisterEvent<GameObject>(m_CameraGameObject, "OnCameraAttachCharacter", OnAttachCharacter);
                }
                gameObject.SetActive(false); // Wait for a character in order to activate.
            } else {
                var character = m_Character;
                m_Character = null; // Set the character to null so the assignment will occur.
                OnAttachCharacter(character);
            }
        }

        /// <summary>
        /// Attaches the component to the specified character.
        /// </summary>
        /// <param name="character">The handler to attach the camera to.</param>
        private void OnAttachCharacter(GameObject character)
        {
            if (character == m_Character) {
                return;
            }

            if (m_Character != null) {
                var playerInput = m_Character.GetComponent<IPlayerInput>();
                if (playerInput is PlayerInputProxy) {
                    playerInput = (playerInput as PlayerInputProxy).PlayerInput;
                }
                var unityInputSystem = playerInput as UnityInputSystem;
                if (unityInputSystem == null) {
                    gameObject.SetActive(false);
                    return;
                }

                EventHandler.UnregisterEvent<bool>(unityInputSystem.gameObject, "OnUnityInputSystemTypeChanged", OnInputTypeChange);
            }

            m_Character = character;

            var activateGameObject = false;
            if (character != null) {
                var playerInput = m_Character.GetComponent<IPlayerInput>();
                if (playerInput is PlayerInputProxy) {
                    playerInput = (playerInput as PlayerInputProxy).PlayerInput;
                }
                var unityInputSystem = playerInput as UnityInputSystem;
                if (unityInputSystem == null) {
                    Debug.LogError($"Error: The character {m_Character.name} has no UnityInputSystem component.");
                    gameObject.SetActive(false);
                    return;
                }

                // If the on screen controls weren't registered then the on screen input type isn't selected.
                EventHandler.RegisterEvent<bool>(unityInputSystem.gameObject, "OnUnityInputSystemTypeChanged", OnInputTypeChange);
            }

            gameObject.SetActive(activateGameObject);
        }

        /// <summary>
        /// Callback when the UnityInputSystem.ForceInput value has changed.
        /// </summary>
        /// <param name="useOnScreenControls">Are the on screen controls now being used?</param>
        private void OnInputTypeChange(bool useOnScreenControls)
        {
            gameObject.SetActive(useOnScreenControls);
        }

        /// <summary>
        /// The GameObject has been destroyed.
        /// </summary>
        private void OnDestroy()
        {
            if (m_CameraGameObject == null) {
                return;
            }

            EventHandler.UnregisterEvent<GameObject>(m_CameraGameObject, "OnCameraAttachCharacter", OnAttachCharacter);
        }
    }
}
#endif