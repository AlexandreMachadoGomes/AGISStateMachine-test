/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.UltimateCharacterController.Demo.UnityStandardAssets.Vehicles.Car
{
    using UnityEngine;
#if ENABLE_INPUT_SYSTEM
    using UnityEngine.InputSystem;
#endif

    [RequireComponent(typeof (CarController))]
    public class CarUserControl : MonoBehaviour
    {
        private CarController m_Car; // the car controller we want to use

        private void Awake()
        {
            m_Car = GetComponent<CarController>();
        }

        private void FixedUpdate()
        {
#if ENABLE_INPUT_SYSTEM
            float h = GetAxis(true);
            float v = GetAxis(false);
            float handbrake = Keyboard.current?.spaceKey.isPressed == true ? 1f : 0f;
#else
            // pass the input to the car!
            float h = UnityEngine.Input.GetAxis("Horizontal");
            float v = UnityEngine.Input.GetAxis("Vertical");
            float handbrake = UnityEngine.Input.GetAxis("Jump");
#endif
            m_Car.Move(h, v, v, handbrake);
        }

#if ENABLE_INPUT_SYSTEM
        public static float GetAxis(bool horizontal)
        {
            if (horizontal) {
                return (Keyboard.current?.dKey.isPressed == true ? 1f : 0f) +
                       (Keyboard.current?.aKey.isPressed == true ? -1f : 0f);
            }
            return (Keyboard.current?.wKey.isPressed == true ? 1f : 0f) +
                   (Keyboard.current?.sKey.isPressed == true ? -1f : 0f);
        }
#endif
    }
}
