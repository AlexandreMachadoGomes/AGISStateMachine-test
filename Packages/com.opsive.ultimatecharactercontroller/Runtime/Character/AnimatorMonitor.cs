/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Character
{
    using Opsive.Shared.Events;
    using Opsive.Shared.Game;
    using Opsive.Shared.Utility;
    using Opsive.UltimateCharacterController.Inventory;
    using Opsive.UltimateCharacterController.Utility;
    using UnityEngine;
    using System.Collections.Generic;

    /// <summary>
    /// The AnimatorMonitor acts as a bridge for the parameters on the Animator component.
    /// If an Animator component is not attached to the character (such as for first person view) then the updates will be forwarded to the item's Animator.
    /// </summary>
    public class AnimatorMonitor : AnimationMonitorBase
    {
        [Tooltip("The runtime speed of the Animator.")]
        [SerializeField] protected float m_AnimatorSpeed = 1;
        [Tooltip("The location that the Animator should update in.")]
        [SerializeField] protected AnimatorUpdateMode m_UpdateMode = AnimatorUpdateMode.Normal;
        [Tooltip("The damping time for the Horizontal Movement parameter. The higher the value the slower the parameter value changes.")]
        [SerializeField] protected float m_HorizontalMovementDampingTime = 0.1f;
        [Tooltip("The damping time for the Forward Movement parameter. The higher the value the slower the parameter value changes.")]
        [SerializeField] protected float m_ForwardMovementDampingTime = 0.1f;
        [Tooltip("The damping time for the Pitch parameter. The higher the value the slower the parameter value changes.")]
        [SerializeField] protected float m_PitchDampingTime = 0.1f;
        [Tooltip("The damping time for the Yaw parameter. The higher the value the slower the parameter value changes.")]
        [SerializeField] protected float m_YawDampingTime = 0.1f;
        [Tooltip("Specifies how much to multiply the yaw parameter by when turning in place.")]
        [SerializeField] protected float m_YawMultiplier = 7;
        [Tooltip("Specifies the value of the Speed Parameter when the character is moving.")]
        [SerializeField] protected float m_MovingSpeedParameterValue = 1;

        public float AnimatorSpeed { get { return m_AnimatorSpeed; } set { m_AnimatorSpeed = value; if (m_Animator != null) { m_Animator.speed = m_AnimatorSpeed; } } }
        public AnimatorUpdateMode UpdateMode { get { return m_UpdateMode; } set { m_UpdateMode = value; if (m_Animator != null) { m_Animator.updateMode = m_UpdateMode; } } }
        public float HorizontalMovementDampingTime { get { return m_HorizontalMovementDampingTime; } set { m_HorizontalMovementDampingTime = value; } }
        public float ForwardMovementDampingTime { get { return m_ForwardMovementDampingTime; } set { m_ForwardMovementDampingTime = value; } }
        public float PitchDampingTime { get { return m_PitchDampingTime; } set { m_PitchDampingTime = value; } }
        public float YawDampingTime { get { return m_YawDampingTime; } set { m_YawDampingTime = value; } }

        private static int s_HorizontalMovementHash = Animator.StringToHash("HorizontalMovement");
        private static int s_ForwardMovementHash = Animator.StringToHash("ForwardMovement");
        private static int s_PitchHash = Animator.StringToHash("Pitch");
        private static int s_YawHash = Animator.StringToHash("Yaw");
        private static int s_SpeedHash = Animator.StringToHash("Speed");
        private static int s_HeightHash = Animator.StringToHash("Height");
        private static int s_MovingHash = Animator.StringToHash("Moving");
        private static int s_AimingHash = Animator.StringToHash("Aiming");
        private static int s_MovementSetIDHash = Animator.StringToHash("MovementSetID");
        private static int s_AbilityIndexHash = Animator.StringToHash("AbilityIndex");
        private static int s_AbilityChangeHash = Animator.StringToHash("AbilityChange");
        private static int s_AbilityIntDataHash = Animator.StringToHash("AbilityIntData");
        private static int s_AbilityFloatDataHash = Animator.StringToHash("AbilityFloatData");
        private static int[] s_ItemSlotIDHash;
        private static int[] s_ItemSlotStateIndexHash;
        private static int[] s_ItemSlotStateIndexChangeHash;
        private static int[] s_ItemSlotSubstateIndexHash;

        private CharacterIK m_CharacterIK;

        private HashSet<int> m_ItemParameterExists;
        private bool m_SpeedParameterOverride;

        [NonSerialized] public override bool SpeedParameterOverride { get { return m_SpeedParameterOverride; } set { m_SpeedParameterOverride = value; } }

        /// <summary>
        /// Internal method which initializes the default values.
        /// </summary>
        protected override void AwakeInternal()
        {
            base.AwakeInternal();

            m_CharacterIK = gameObject.GetComponent<CharacterIK>();

#if UNITY_EDITOR
            // If the animator doesn't have the required parameters then it's not a valid animator.
            if (m_Animator != null) {
                if (!HasParameter(s_HorizontalMovementHash) || !HasParameter(s_ForwardMovementHash) || !HasParameter(s_AbilityChangeHash)) {
                    Debug.LogError($"Error: The animator {m_Animator.name} is not designed to work with the Ultimate Character Controller. " +
                                   "Ensure the animator has all of the required parameters.");
                    return;
                }
            }
#endif

            if (m_Animator != null) {
                EventHandler.RegisterEvent<float>(m_GameObject, "OnCharacterChangeTimeScale", OnChangeTimeScale);

                m_Animator.speed = m_AnimatorSpeed;
                m_Animator.updateMode = m_UpdateMode;
            }
        }

        /// <summary>
        /// Does the animator have the specified parameter?
        /// </summary>
        /// <param name="parameterHash">The hash of the parameter.</param>
        /// <returns>True if the animator has the specified parameter.</returns>
        private bool HasParameter(int parameterHash)
        {
            for (int i = 0; i < m_Animator.parameterCount; ++i) {
                if (m_Animator.parameters[i].nameHash == parameterHash) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Initializes the item parameters.
        /// </summary>
        /// <returns>True if the item parameters were initialized.</returns>
        public override bool InitializeItemParameters()
        {
            if (!base.InitializeItemParameters()) {
                return false;
            }

            m_ItemParameterExists = new HashSet<int>();
            if (m_Animator == null) {
                return true;
            }

            var inventory = m_GameObject.GetComponent<InventoryBase>();
            var slotCount = inventory.SlotCount;
            if (s_ItemSlotIDHash == null || s_ItemSlotIDHash.Length < slotCount) {
                s_ItemSlotIDHash = new int[slotCount];
                s_ItemSlotStateIndexHash = new int[slotCount];
                s_ItemSlotStateIndexChangeHash = new int[slotCount];
                s_ItemSlotSubstateIndexHash = new int[slotCount];
            }

            for (int i = 0; i < slotCount; ++i) {
                // Animators do not need to contain every slot index.
                var slotIDHash = Animator.StringToHash(string.Format("Slot{0}ItemID", i));
                if (!HasParameter(slotIDHash)) {
                    continue;
                }
                m_ItemParameterExists.Add(i);

                if (s_ItemSlotIDHash[i] == 0) {
                    s_ItemSlotIDHash[i] = slotIDHash;
                    s_ItemSlotStateIndexHash[i] = Animator.StringToHash(string.Format("Slot{0}ItemStateIndex", i));
                    s_ItemSlotStateIndexChangeHash[i] = Animator.StringToHash(string.Format("Slot{0}ItemStateIndexChange", i));
                    s_ItemSlotSubstateIndexHash[i] = Animator.StringToHash(string.Format("Slot{0}ItemSubstateIndex", i));
                }
            }

            return true;
        }

        /// <summary>
        /// Prepares the Animator parameters for start.
        /// </summary>
        protected override void Start()
        {
            base.Start();

            if (m_Animator != null) {
                var characterLocomotion = m_GameObject.GetCachedComponent<UltimateCharacterLocomotion>();
                if (characterLocomotion != null) {
                    OnChangeTimeScale(characterLocomotion.TimeScale);
                }
            }
        }

        /// <summary>
        /// Snaps the animator to the default values.
        /// </summary>
        private void SnapAnimations()
        {
            SnapAnimations(true);
        }

        /// <summary>
        /// Snaps the animator to the default values.
        /// </summary>
        /// <param name="executeEvent">Should the animator snapped event be executed?</param>
        protected override void SnapAnimations(bool executeEvent)
        {
            // A first person view may not use an Animator.
            if (m_Animator != null) {
                // The values should be reset enabled so the animator will snap to the correct animation.
                m_Animator.SetFloat(s_HorizontalMovementHash, m_HorizontalMovement, 0, 0);
                m_Animator.SetFloat(s_ForwardMovementHash, m_ForwardMovement, 0, 0);
                m_Animator.SetFloat(s_PitchHash, m_Pitch, 0, 0);
                m_Animator.SetFloat(s_YawHash, m_Yaw, 0, 0);
                m_Animator.SetFloat(s_SpeedHash, m_Speed, 0, 0);
                m_Animator.SetFloat(s_HeightHash, m_Height, 0, 0);
                m_Animator.SetBool(s_MovingHash, m_Moving);
                m_Animator.SetBool(s_AimingHash, m_Aiming);
                m_Animator.SetInteger(s_MovementSetIDHash, m_MovementSetID);
                m_Animator.SetInteger(s_AbilityIndexHash, m_AbilityIndex);
                m_Animator.SetTrigger(s_AbilityChangeHash);
                m_Animator.SetInteger(s_AbilityIntDataHash, m_AbilityIntData);
                m_Animator.SetFloat(s_AbilityFloatDataHash, m_AbilityFloatData, 0, 0);

                if (m_HasItemParameters) {
                    UpdateItemIDParameters();
                    for (int i = 0; i < m_EquippedItems.Length; ++i) {
                        if (!m_ItemParameterExists.Contains(i)) {
                            continue;
                        }
                        m_Animator.SetInteger(s_ItemSlotIDHash[i], m_ItemSlotID[i]);
                        m_Animator.SetTrigger(s_ItemSlotStateIndexChangeHash[i]);
                        m_Animator.SetInteger(s_ItemSlotStateIndexHash[i], m_ItemSlotStateIndex[i]);
                        m_Animator.SetInteger(s_ItemSlotSubstateIndexHash[i], m_ItemSlotSubstateIndex[i]);
                    }
                }

                if (executeEvent) {
                    EventHandler.ExecuteEvent(m_GameObject, "OnAnimatorWillSnap");
                }

                // Keep the IK component disabled until the animator is snapped. This will prevent the OnAnimatorIK callback from occurring.
                var ikEnabled = false;
                if (m_CharacterIK != null) {
                    ikEnabled = m_CharacterIK.enabled;
                    m_CharacterIK.enabled = false;
                }

                // Root motion should not move the character when snapping.
                var position = m_Transform.position;
                var rotation = m_Transform.rotation;

                // Update 0 will force the changes.
                if (m_Animator.isActiveAndEnabled) {
                    m_Animator.Update(0);
                }
#if UNITY_EDITOR
                var count = 0;
#endif
                // Keep updating the Animator until it is no longer in a transition. This will snap the animator to the correct state immediately.
                while (IsInTrasition()) {
#if UNITY_EDITOR
                    count++;
                    if (count > TimeUtility.TargetFramerate * 2) {
                        Debug.LogError("Error: The animator is not leaving a transition. Ensure your Animator Controller does not have any infinite loops.");
                        return;
                    }
#endif
                    m_Animator.Update(Time.fixedDeltaTime * 2);
                }

                // The animator should be positioned at the start of each state.
                for (int i = 0; i < m_Animator.layerCount; ++i) {
                    m_Animator.Play(m_Animator.GetCurrentAnimatorStateInfo(i).fullPathHash, i, 0);
                }

                if (m_Animator.isActiveAndEnabled) {
                    m_Animator.Update(Time.fixedDeltaTime);
                }

                // Prevent the change parameters from staying triggered when the animator is on the idle state.
                SetAbilityChangeParameter(false);

                m_Transform.SetPositionAndRotation(position, rotation);
                if (ikEnabled) {
                    m_CharacterIK.enabled = true;
                }
            }

            // The item animators should also snap.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    SetItemStateIndexChangeParameter(i, false);
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SnapAnimator();
                    }
                }
            }

            if (executeEvent) {
                EventHandler.ExecuteEvent(m_GameObject, "OnAnimatorSnapped");
            }
        }

        /// <summary>
        /// Is the Animator Controller currently in a transition?
        /// </summary>
        /// <returns>True if any layer within the Animator Controller is within a transition.</returns>
        private bool IsInTrasition()
        {
            for (int i = 0; i < m_Animator.layerCount; ++i) {
                if (m_Animator.IsInTransition(i)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Updates the animation paremters.
        /// </summary>
        protected override void UpdateAnimationParameters()
        {
            SetHorizontalMovementParameter(m_CharacterLocomotion.InputVector.x, m_CharacterLocomotion.TimeScale, m_HorizontalMovementDampingTime);
            SetForwardMovementParameter(m_CharacterLocomotion.InputVector.y, m_CharacterLocomotion.TimeScale, m_ForwardMovementDampingTime);
            if (m_CharacterLocomotion.LookSource != null) {
                SetPitchParameter(m_CharacterLocomotion.LookSource.Pitch, m_CharacterLocomotion.TimeScale, m_PitchDampingTime);
            }
            float yawAngle;
            if (m_CharacterLocomotion.UsingRootMotionRotation) {
                yawAngle = MathUtility.ClampInnerAngle(m_CharacterLocomotion.DeltaRotation.y);
            } else {
                yawAngle = MathUtility.ClampInnerAngle((m_CharacterLocomotion.LastDesiredRotation * Quaternion.Inverse(m_CharacterLocomotion.MovingPlatformRotation)).eulerAngles.y);
            }
            SetYawParameter(yawAngle * m_YawMultiplier, m_CharacterLocomotion.TimeScale, m_YawDampingTime);
            if (!m_SpeedParameterOverride) {
                SetSpeedParameter(m_CharacterLocomotion.Moving ? m_MovingSpeedParameterValue : 0, m_CharacterLocomotion.TimeScale);
            }

            UpdateDirtyAbilityAnimatorParameters();
            UpdateItemIDParameters();
        }

        /// <summary>
        /// Sets the Horizontal Movement parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetHorizontalMovementParameter(float value, float timeScale)
        {
            return SetHorizontalMovementParameter(value, timeScale, m_HorizontalMovementDampingTime);
        }

        /// <summary>
        /// Sets the Horizontal Movement parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetHorizontalMovementParameter(float value, float timeScale, float dampingTime)
        {
            var change = m_HorizontalMovement != value;
            if (change) {
                if (m_Animator != null) {
                    m_Animator.SetFloat(s_HorizontalMovementHash, value, dampingTime, TimeUtility.DeltaTimeScaled / timeScale);
                    m_HorizontalMovement = m_Animator.GetFloat(s_HorizontalMovementHash);
                    if (Mathf.Abs(m_HorizontalMovement) < 0.001f) {
                        m_HorizontalMovement = 0;
                        m_Animator.SetFloat(s_HorizontalMovementHash, 0);
                    }
                } else {
                    m_HorizontalMovement = value;
                }
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetHorizontalMovementParameter(value, timeScale, dampingTime);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Sets the Forward Movement parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        public override bool SetForwardMovementParameter(float value, float timeScale)
        {
            return SetForwardMovementParameter(value, timeScale, m_ForwardMovementDampingTime);
        }

        /// <summary>
        /// Sets the Forward Movement parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetForwardMovementParameter(float value, float timeScale, float dampingTime)
        {
            var change = m_ForwardMovement != value;
            if (change) {
                if (m_Animator != null) {
                    m_Animator.SetFloat(s_ForwardMovementHash, value, dampingTime, TimeUtility.DeltaTimeScaled / timeScale);
                    m_ForwardMovement = m_Animator.GetFloat(s_ForwardMovementHash);
                    if (Mathf.Abs(m_ForwardMovement) < 0.001f) {
                        m_ForwardMovement = 0;
                        m_Animator.SetFloat(s_ForwardMovementHash, 0);
                    }
                } else {
                    m_ForwardMovement = value;
                }
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetForwardMovementParameter(value, timeScale, dampingTime);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Sets the Pitch parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetPitchParameter(float value, float timeScale)
        {
            return SetPitchParameter(value, timeScale, m_PitchDampingTime);
        }

        /// <summary>
        /// Sets the Pitch parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetPitchParameter(float value, float timeScale, float dampingTime)
        {
            var change = m_Pitch != value;
            if (change) {
                if (m_Animator != null) {
                    m_Animator.SetFloat(s_PitchHash, value, dampingTime, TimeUtility.DeltaTimeScaled / timeScale);
                    m_Pitch = m_Animator.GetFloat(s_PitchHash);
                    if (Mathf.Abs(m_Pitch) < 0.001f) {
                        m_Pitch = 0;
                        m_Animator.SetFloat(s_PitchHash, 0);
                    }
                } else {
                    m_Pitch = value;
                }
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetPitchParameter(value, timeScale, dampingTime);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Sets the Yaw parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetYawParameter(float value, float timeScale)
        {
            return SetYawParameter(value, timeScale, m_YawDampingTime);
        }

        /// <summary>
        /// Sets the Yaw parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetYawParameter(float value, float timeScale, float dampingTime)
        {
            var change = m_Yaw != value;
            if (change) {
                if (m_Animator != null) {
                    m_Animator.SetFloat(s_YawHash, value, dampingTime, TimeUtility.DeltaTimeScaled / timeScale);
                    m_Yaw = m_Animator.GetFloat(s_YawHash);
                    if (Mathf.Abs(m_Yaw) < 0.001f) {
                        m_Yaw = 0;
                        m_Animator.SetFloat(s_YawHash, 0);
                    }
                } else {
                    m_Yaw = value;
                }
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetYawParameter(value, timeScale, dampingTime);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Sets the Speed parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetSpeedParameter(float value, float timeScale)
        {
            return SetSpeedParameter(value, timeScale, 0);
        }

        /// <summary>
        /// Sets the Speed parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetSpeedParameter(float value, float timeScale, float dampingTime)
        {
            var change = m_Speed != value;
            if (change) {
                if (m_Animator != null) {
                    m_Animator.SetFloat(s_SpeedHash, value, dampingTime, TimeUtility.DeltaTimeScaled / timeScale);
                    m_Speed = m_Animator.GetFloat(s_SpeedHash);
                    if (Mathf.Abs(m_Speed) < 0.001f) {
                        m_Speed = 0;
                        m_Animator.SetFloat(s_SpeedHash, 0);
                    }
                } else {
                    m_Speed = value;
                }
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetSpeedParameter(value, timeScale, dampingTime);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Sets the Height parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetHeightParameter(float value)
        {
            var change = m_Height != value;
            if (change) {
                if (m_Animator != null) {
                    m_Animator.SetFloat(s_HeightHash, value, 0, 0);
                    m_Height = (int)m_Animator.GetFloat(s_HeightHash);
                    if (Mathf.Abs(m_Height) < 0.001f) {
                        m_Height = 0;
                        m_Animator.SetFloat(s_HeightHash, 0);
                    }
                } else {
                    m_Height = value;
                }
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetHeightParameter(value);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Sets the Moving parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetMovingParameter(bool value)
        {
            var change = m_Moving != value;
            if (change) {
                if (m_Animator != null) {
                    m_Animator.SetBool(s_MovingHash, value);
                }
                m_Moving = value;
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetMovingParameter(value);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Sets the Aiming parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetAimingParameter(bool value)
        {
            var change = m_Aiming != value;
            if (change) {
                if (m_Animator != null) {
                    m_Animator.SetBool(s_AimingHash, value);
                }
                m_Aiming = value;
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetAimingParameter(value);
                    }
                }
            }
            return change;
        }

        /// <summary>
        /// Sets the Movement Set ID parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetMovementSetIDParameter(int value)
        {
            var change = m_MovementSetID != value;
            if (change) {
                if (m_Animator != null) {
                    m_Animator.SetInteger(s_MovementSetIDHash, value);
                }
                m_MovementSetID = value;
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetMovementSetIDParameter(value);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Sets the Ability Index parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetAbilityIndexParameter(int value)
        {
            var change = m_AbilityIndex != value;
            if (change) {
#if UNITY_EDITOR
                if (m_LogAbilityParameterChanges) {
                    Debug.Log($"{Time.frameCount} Changed AbilityIndex to {value} on GameObject {m_GameObject.name}.");
                }
#endif
                if (m_Animator != null) {
                    m_Animator.SetInteger(s_AbilityIndexHash, value);
                    SetAbilityChangeParameter(true);
                }
                m_AbilityIndex = value;
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetAbilityIndexParameter(value);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Sets the Ability Change parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetAbilityChangeParameter(bool value)
        {
            if (m_Animator != null && m_Animator.GetBool(s_AbilityChangeHash) != value) {
                if (value) {
                    m_Animator.SetTrigger(s_AbilityChangeHash);
                } else {
                    m_Animator.ResetTrigger(s_AbilityChangeHash);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the Int Data parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetAbilityIntDataParameter(int value)
        {
            var change = m_AbilityIntData != value;
            if (change) {
#if UNITY_EDITOR
                if (m_LogAbilityParameterChanges) {
                    Debug.Log($"{Time.frameCount} Changed AbilityIntData to {value} on GameObject {m_GameObject.name}.");
                }
#endif
                if (m_Animator != null) {
                    m_Animator.SetInteger(s_AbilityIntDataHash, value);
                }
                m_AbilityIntData = value;
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetAbilityIntDataParameter(value);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Sets the Float Data parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        public override bool SetAbilityFloatDataParameter(float value, float timeScale)
        {
            return SetAbilityFloatDataParameter(value, timeScale, 0);
        }

        /// <summary>
        /// Sets the Float Data parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetAbilityFloatDataParameter(float value, float timeScale, float dampingTime)
        {
            var change = m_AbilityFloatData != value;
            if (change) {
                if (m_Animator != null) {
                    m_Animator.SetFloat(s_AbilityFloatDataHash, value, dampingTime, TimeUtility.DeltaTimeScaled / timeScale);
                    m_AbilityFloatData = m_Animator.GetFloat(s_AbilityFloatDataHash);
                } else {
                    m_AbilityFloatData = value;
                }
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetAbilityFloatDataParameter(value, timeScale, dampingTime);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Sets the Item ID parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot that the item occupies.</param>
        /// <param name="value">The new value.</param>
        public override bool SetItemIDParameter(int slotID, int value)
        {
            var change = m_ItemSlotID[slotID] != value;
            if (change) {
#if UNITY_EDITOR
                if (m_LogItemParameterChanges) {
                    Debug.Log($"{Time.frameCount} Changed Slot{slotID}ItemID to {value} on GameObject {m_GameObject.name}.");
                }
#endif
                if (m_Animator != null && m_ItemParameterExists.Contains(slotID)) {
                    m_Animator.SetInteger(s_ItemSlotIDHash[slotID], value);
                    // Even though no state index was changed the trigger should be set to true so the animator can transition to the new item id.
                    SetItemStateIndexChangeParameter(slotID, value != 0);
                }
                m_ItemSlotID[slotID] = value;
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetItemIDParameter(slotID, value);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Sets the Primary Item State Index parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot that the item occupies.</param>
        /// <param name="value">The new value.</param>
        /// <param name="forceChange">Force the change the new value?</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetItemStateIndexParameter(int slotID, int value, bool forceChange)
        {
            var change = forceChange || m_ItemSlotStateIndex[slotID] != value;
            if (change) {
#if UNITY_EDITOR
                if (m_LogItemParameterChanges) {
                    Debug.Log($"{Time.frameCount} Changed Slot{slotID}ItemStateIndex to {value} on GameObject {m_GameObject.name}.");
                }
#endif
                if (m_Animator != null && m_ItemParameterExists.Contains(slotID)) {
                    m_Animator.SetInteger(s_ItemSlotStateIndexHash[slotID], value);
                    SetItemStateIndexChangeParameter(slotID, value != 0);
                }
                m_ItemSlotStateIndex[slotID] = value;
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetItemStateIndexParameter(slotID, value);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Sets the Item State Index Change parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot of that item that should be set.</param>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetItemStateIndexChangeParameter(int slotID, bool value)
        {
            if (!m_ItemParameterExists.Contains(slotID)) {
                return false;
            }

            if (m_Animator != null && m_Animator.GetBool(s_ItemSlotStateIndexChangeHash[slotID]) != value) {
                if (value) {
                    m_Animator.SetTrigger(s_ItemSlotStateIndexChangeHash[slotID]);
                } else {
                    m_Animator.ResetTrigger(s_ItemSlotStateIndexChangeHash[slotID]);
                }

#if UNITY_EDITOR
                if (m_LogItemParameterChanges) {
                    Debug.Log($"{Time.frameCount} Changed Slot{slotID}ItemStateIndexChange Trigger to {value} on GameObject {m_GameObject.name}.");
                }
#endif

                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the Item Substate Index parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot that the item occupies.</param>
        /// <param name="value">The new value.</param>
        /// <param name="forceChange">Force the change the new value?</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetItemSubstateIndexParameter(int slotID, int value, bool forceChange)
        {
            var change = forceChange || m_ItemSlotSubstateIndex[slotID] != value;
            if (change) {
#if UNITY_EDITOR
                if (m_LogItemParameterChanges) {
                    Debug.Log($"{Time.frameCount} Changed Slot{slotID}ItemSubstateIndex to {value} on GameObject {m_GameObject.name}.");
                }
#endif
                if (m_Animator != null && m_ItemParameterExists.Contains(slotID)) {
                    m_Animator.SetInteger(s_ItemSlotSubstateIndexHash[slotID], value);
                }
                m_ItemSlotSubstateIndex[slotID] = value;
            }

            // The item's Animator should also be aware of the updated parameter value.
            if (m_EquippedItems != null) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (m_EquippedItems[i] != null) {
                        m_EquippedItems[i].SetItemSubstateIndexParameter(slotID, value);
                    }
                }
            }

            return change;
        }

        /// <summary>
        /// Root motion has moved the character.
        /// </summary>
        private void OnAnimatorMove()
        {
            m_CharacterLocomotion.UpdateRootMotion(m_Animator.deltaPosition, m_Animator.deltaRotation);
        }

        /// <summary>
        /// The character's local timescale has changed.
        /// </summary>
        /// <param name="timeScale">The new timescale.</param>
        private void OnChangeTimeScale(float timeScale)
        {
            if (timeScale < 0) {
                return;
            }

            m_Animator.speed = timeScale;
        }

        /// <summary>
        /// The GameObject has been destroyed.
        /// </summary>
        private void OnDestroy()
        {
            EventHandler.UnregisterEvent<float>(m_GameObject, "OnCharacterChangeTimeScale", OnChangeTimeScale);
        }

        /// <summary>
        /// Reset the static variables for domain reloading.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void DomainReset()
        {
            s_ItemSlotIDHash = null;
            s_ItemSlotStateIndexHash = null;
            s_ItemSlotStateIndexChangeHash = null;
            s_ItemSlotSubstateIndexHash = null;
        }
    }
}