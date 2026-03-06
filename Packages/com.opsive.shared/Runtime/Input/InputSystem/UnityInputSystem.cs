#if ENABLE_INPUT_SYSTEM
/// ---------------------------------------------
/// Opsive Shared
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.Shared.Input.InputSystem
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.Controls;
    using UnityEngine.Scripting.APIUpdating;

    /// <summary>
    /// Responds to input using the Unity Input System.
    /// </summary>
    [MovedFrom("Opsive.Shared.Input")]
    public class UnityInputSystem : Opsive.Shared.Input.PlayerInput
    {
        /// <summary>
        /// Specifies if any input type should be forced.
        /// </summary>
        public enum ForceInputType { None, Standalone, OnScreen }

        [Tooltip("Specifies if any input type should be forced.")]
        [SerializeField] protected ForceInputType m_ForceInput;
        [Tooltip("Should the cursor be disabled?")]
        [SerializeField] protected bool m_DisableCursor = true;
        [Tooltip("Should the cursor be enabled when the escape key is pressed?")]
        [SerializeField] protected bool m_EnableCursorWithEscape = true;
#if UNITY_EDITOR || !(UNITY_IPHONE || UNITY_ANDROID || UNITY_WP_8_1 || UNITY_BLACKBERRY)
        [Tooltip("If the cursor is enabled with escape should the look vector be prevented from updating?")]
        [SerializeField] protected bool m_PreventLookVectorChanges = true;
#endif
        [Tooltip("Should the PlayerInput component be disabled when the UnityInputSystem component is disabled?")]
        [SerializeField] protected bool m_DisablePlayerInput = true;

        private PlayerInput m_PlayerInput;
        private Dictionary<InputActionMap, Dictionary<string, InputAction>> m_InputActionByName = new Dictionary<InputActionMap, Dictionary<string, InputAction>>();
        private Dictionary<string, float> m_CachedAxisValues = new Dictionary<string, float>();

        private bool m_UseOnScreenControls;
        protected override bool CanCheckForController => false;

        public ForceInputType ForceInput {
            get { return m_ForceInput; }
            set {
                if (m_ForceInput == value) { return; }

                m_ForceInput = value;
                UpdateOnScreenControls();
            }
        }
        public bool DisableCursor {
            get { return m_DisableCursor; }
            set {

                m_DisableCursor = value;
                if (m_DisableCursor && Cursor.visible) {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                } else if (!m_DisableCursor && !Cursor.visible) {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }
        public bool EnableCursorWithEscape { get { return m_EnableCursorWithEscape; } set { m_EnableCursorWithEscape = value; } }
#if UNITY_EDITOR || !(UNITY_IPHONE || UNITY_ANDROID || UNITY_WP_8_1 || UNITY_BLACKBERRY)
        public bool PreventLookMovementWithEscape { get { return m_PreventLookVectorChanges; } set { m_PreventLookVectorChanges = value; } }
#endif

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_PlayerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            m_PlayerInput.enabled = true;
        }

        /// <summary>
        /// Starts the component.
        /// </summary>
        protected override void Start()
        {
            base.Start();

            UpdateOnScreenControls();
        }

        /// <summary>
        /// Updates if on screen controls are used.
        /// </summary>
        private void UpdateOnScreenControls()
        {
            m_UseOnScreenControls = m_ForceInput == ForceInputType.OnScreen;
#if !UNITY_EDITOR && (UNITY_IPHONE || UNITY_ANDROID || UNITY_WP_8_1 || UNITY_BLACKBERRY)
            if (m_ForceInput != ForceInputType.Standalone) {
                m_UseOnScreenControls = true;
            }
#endif
            if (m_UseOnScreenControls && Gamepad.current != null) {
                m_PlayerInput.SwitchCurrentControlScheme("Gamepad", Gamepad.current);
            }
            Events.EventHandler.ExecuteEvent(gameObject, "OnUnityInputSystemTypeChanged", m_UseOnScreenControls);
        }

        /// <summary>
        /// The component has been enabled.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (m_DisableCursor) {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            m_PlayerInput.enabled = true;

            // All players must have access to the same keyboard and mouse.
            if (Keyboard.current != null) {
                m_PlayerInput.SwitchCurrentControlScheme(Keyboard.current, Mouse.current);
            }
        }

#if UNITY_EDITOR || !(UNITY_IPHONE || UNITY_ANDROID || UNITY_WP_8_1 || UNITY_BLACKBERRY)
        /// <summary>
        /// Update the joystick and cursor state values.
        /// </summary>
        private void LateUpdate()
        {
            // Enable the cursor if the escape key is pressed. Disable the cursor if it is visbile but should be disabled upon press.
            if (m_EnableCursorWithEscape && Keyboard.current.escapeKey.wasPressedThisFrame) {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (m_PreventLookVectorChanges) {
                    OnApplicationFocus(false);
                }
            } else if ((Cursor.visible || m_Focus == false) && m_DisableCursor && !IsPointerOverUI() && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)) {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (m_PreventLookVectorChanges) {
                    OnApplicationFocus(true);
                }
            }
#if UNITY_EDITOR
            // The cursor should be visible when the game is paused.
            if (!Cursor.visible && Time.deltaTime == 0) {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
#endif
        }
#endif

        /// <summary>
        /// Is a controller connected?
        /// </summary>
        /// <returns>True if a controller is connected.</returns>
        public override bool IsControllerConnected() { return true; }

        /// <summary>
        /// Internal method which returns true if the button is being pressed.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <returns>True of the button is being pressed.</returns>
        protected override bool GetButtonInternal(string name)
        {
            var action = GetActionByName(name);
            if (action != null) {
                if (action.activeControl is ButtonControl button && button.isPressed) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Internal method which returns true if the button was pressed this frame.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <returns>True if the button is pressed this frame.</returns>
        protected override bool GetButtonDownInternal(string name)
        {
            var action = GetActionByName(name);
            if (action != null) {
                if (action.activeControl is ButtonControl button && button.wasPressedThisFrame) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Internal method which returnstrue if the button is up.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <returns>True if the button is up.</returns>
        protected override bool GetButtonUpInternal(string name)
        {
            var action = GetActionByName(name);
            if (action != null) {
                for (int i = 0; i < action.controls.Count; i++) {
                    if (action.controls[i] is ButtonControl button && button.wasReleasedThisFrame) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Internal method which returns the value of the axis with the specified name.
        /// </summary>
        /// <param name="name">The name of the axis.</param>
        /// <returns>The value of the axis.</returns>
        protected override float GetAxisInternal(string name)
        {
            var action = GetActionByName(name);
            if (action != null) {
                var currentValue = action.ReadValue<float>();

                if (m_UseOnScreenControls) {
                    // The on-screen stick component does not keep the action active when the user holds the stick without movement.
                    if (action.phase == InputActionPhase.Performed) {
                        m_CachedAxisValues[name] = currentValue;
                        return currentValue;
                    } else { // No active input.
                        if (action.phase == InputActionPhase.Canceled || !IsUserInteracting()) { // The stick was released.
                            m_CachedAxisValues.Remove(name);
                            return 0.0f;
                        }

                        // Check for a cached value. The stick is being held in position.
                        if (m_CachedAxisValues.TryGetValue(name, out var cachedValue)) {
                            return cachedValue;
                        }
                    }
                }

                return currentValue;
            }
            return 0.0f;
        }

        /// <summary>
        /// Internal method which returns the value of the raw axis with the specified name.
        /// </summary>
        /// <param name="name">The name of the axis.</param>
        /// <returns>The value of the raw axis.</returns>
        protected override float GetAxisRawInternal(string name)
        {
            return GetAxisInternal(name);
        }

        /// <summary>
        /// Checks if the user is actively interacting with the screen (mouse button down or touching).
        /// </summary>
        /// <returns>True if the user is interacting.</returns>
        private bool IsUserInteracting()
        {
            // Check mouse input.
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) {
                return true;
            }

            // Check touch input.
            if (Touchscreen.current != null) {
                for (int i = 0; i < Touchscreen.current.touches.Count; i++) {
                    var touch = Touchscreen.current.touches[i];
                    if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began ||
                        touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved ||
                        touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Stationary) {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the position of the mouse.
        /// </summary>
        /// <returns>The mouse position.</returns>
        public override Vector2 GetMousePosition() { return Mouse.current.position.ReadValue(); }

        /// <summary>
        /// Enables or disables gameplay input. An example of when it will not be enabled is when there is a fullscreen UI over the main camera.
        /// </summary>
        /// <param name="enable">True if the input is enabled.</param>
        protected override void EnableGameplayInput(bool enable)
        {
            base.EnableGameplayInput(enable);

            if (enable && m_DisableCursor) {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>
        /// Does the game have focus?
        /// </summary>
        /// <param name="hasFocus">True if the game has focus.</param>
        protected override void OnApplicationFocus(bool hasFocus)
        {
            base.OnApplicationFocus(hasFocus);

            if (enabled && hasFocus && m_DisableCursor) {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>
        /// Returns the InputAction with the specified name.
        /// </summary>
        /// <param name="name">The name of the InputAction.</param>
        /// <returns>The InputAction with the specified name.</returns>
        private InputAction GetActionByName(string name)
        {
            if (m_PlayerInput.currentActionMap == null) {
                return null;
            }

            if (!m_InputActionByName.TryGetValue(m_PlayerInput.currentActionMap, out var inputActionByName)) {
                inputActionByName = new Dictionary<string, InputAction>();
                m_InputActionByName.Add(m_PlayerInput.currentActionMap, inputActionByName);
            }
            if (!inputActionByName.TryGetValue(name, out var inputAction)) {
                inputAction = m_PlayerInput.currentActionMap?.FindAction(name);
                inputActionByName.Add(name, inputAction);
            }
            return inputAction;
        }

        /// <summary>
        /// The component has been disabled.
        /// </summary>
        public virtual void OnDisable()
        {
            if (m_DisablePlayerInput) {
                m_PlayerInput.enabled = false;
            }
        }

        /// <summary>
        /// Resets the component.
        /// </summary>
        public void Reset()
        {
            var unityPlayerInput = GetComponent<PlayerInput>();
            if (unityPlayerInput == null) {
                gameObject.AddComponent<PlayerInput>();
            }
            var unityInput = GetComponent<InputManager.UnityInput>();
            if (unityInput != null) {
                DestroyImmediate(unityInput, true);
            }
        }
    }
}
#endif