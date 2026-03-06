/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Character
{
    using Opsive.Shared.Events;
    using Opsive.Shared.Game;
    using Opsive.Shared.StateSystem;
    using Opsive.Shared.Utility;
    using Opsive.UltimateCharacterController.Inventory;
    using Opsive.UltimateCharacterController.Items;
    using UnityEngine;

    /// <summary>
    /// Base class for bridging the character controller with the animation system.
    /// </summary>
    public abstract class AnimationMonitorBase : StateBehavior
    {
#if UNITY_EDITOR
        [Tooltip("Should the Animator log any changes to the item parameters?")]
        [SerializeField] protected bool m_LogAbilityParameterChanges;
        [Tooltip("Should the Animator log any changes to the item parameters?")]
        [SerializeField] protected bool m_LogItemParameterChanges;
        [Tooltip("Should the Animator log any events that it sends?")]
        [SerializeField] protected bool m_LogEvents;
#endif
        protected float m_HorizontalMovement;
        protected float m_ForwardMovement;
        protected float m_Pitch;
        protected float m_Yaw;
        protected float m_Speed;
        protected float m_Height;
        protected bool m_Moving;
        protected bool m_Aiming;
        protected int m_MovementSetID;
        protected int m_AbilityIndex;
        protected int m_AbilityIntData;
        protected float m_AbilityFloatData;
        protected bool m_HasItemParameters;
        protected int[] m_ItemSlotID;
        protected int[] m_ItemSlotStateIndex;
        protected int[] m_ItemSlotSubstateIndex;

        public float HorizontalMovement { get { return m_HorizontalMovement; } }
        public float ForwardMovement { get { return m_ForwardMovement; } }
        public float Pitch { get { return m_Pitch; } }
        public float Yaw { get { return m_Yaw; } }
        public float Speed { get { return m_Speed; } }
        public float Height { get { return m_Height; } }
        public bool Moving { get { return m_Moving; } }
        public bool Aiming { get { return m_Aiming; } }
        public int MovementSetID { get { return m_MovementSetID; } }
        public int AbilityIndex { get { return m_AbilityIndex; } }
        public int AbilityIntData { get { return m_AbilityIntData; } }
        public float AbilityFloatData { get { return m_AbilityFloatData; } }
        public bool HasItemParameters { get { return m_HasItemParameters; } }
        public int ParameterSlotCount { get { return m_ItemSlotID.Length; } }
        public int[] ItemSlotID { get { return m_ItemSlotID; } }
        public int[] ItemSlotStateIndex { get { return m_ItemSlotStateIndex; } }
        public int[] ItemSlotSubstateIndex { get { return m_ItemSlotSubstateIndex; } }
        [Snapshot] protected CharacterItem[] EquippedItems { get { return m_EquippedItems; } set { m_EquippedItems = value; } }
        [NonSerialized] public abstract bool SpeedParameterOverride { get; set; }

#if UNITY_EDITOR
        public bool LogEvents { get { return m_LogEvents; } }
#endif

        protected GameObject m_GameObject;
        protected Transform m_Transform;
        protected Animator m_Animator;
        protected UltimateCharacterLocomotion m_CharacterLocomotion;

        private bool m_DirtyAbilityParameters;
        private bool m_DirtyItemAbilityParameters;
        private bool m_DirtyItemAbilityParametersForceChange;
        private bool m_DirtyEquippedItems;
        private bool[] m_DirtyItemStateIndexParameters;
        private bool[] m_DirtyItemSubstateIndexParameters;
        protected CharacterItem[] m_EquippedItems;

        public bool AnimatorEnabled { get { return m_Animator != null && m_Animator.enabled; } }

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            if (!Game.CharacterInitializer.AutoInitialization) {
                Game.CharacterInitializer.Instance.OnAwake += AwakeInternal;
                return;
            }

            AwakeInternal();
        }

        /// <summary>
        /// Internal method which initializes the default values.
        /// </summary>
        protected virtual void AwakeInternal()
        {
            if (!Game.CharacterInitializer.AutoInitialization) {
                Game.CharacterInitializer.Instance.OnAwake -= AwakeInternal;
            }

            m_CharacterLocomotion = gameObject.GetComponentInParent<UltimateCharacterLocomotion>();
            m_GameObject = m_CharacterLocomotion.gameObject;
            m_Transform = m_GameObject.transform;
            m_Animator = gameObject.GetComponent<Animator>(); // The Animator does not have to exist on the same GameObject as the CharacterLocomotion.

            InitializeItemParameters();

            EventHandler.RegisterEvent<bool>(m_GameObject, "OnCharacterMoving", OnMoving);
            EventHandler.RegisterEvent<Abilities.Ability, bool>(m_GameObject, "OnCharacterAbilityActive", OnAbilityActive);
            EventHandler.RegisterEvent<Abilities.Items.ItemAbility, bool>(m_GameObject, "OnCharacterItemAbilityActive", OnItemAbilityActive);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnCharacterUpdateAbilityParameters", UpdateAbilityAnimatorParameters);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnCharacterUpdateItemAbilityParameters", ExternalUpdateItemAbilityAnimatorParameters);
            EventHandler.RegisterEvent<CharacterItem, int>(m_GameObject, "OnAbilityWillEquipItem", OnWillEquipItem);
            EventHandler.RegisterEvent<CharacterItem, int>(m_GameObject, "OnAbilityUnequipItemComplete", OnUnequipItem);
            EventHandler.RegisterEvent<CharacterItem, int>(m_GameObject, "OnInventoryRemoveItem", OnUnequipItem);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAimAbilityAim", OnAiming);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnCharacterSnapAnimator", SnapAnimations);
            EventHandler.RegisterEvent<GameObject>(m_GameObject, "OnCharacterSwitchModels", OnSwitchModels);
            var modelManager = m_CharacterLocomotion.gameObject.GetCachedComponent<ModelManager>();
            if (modelManager == null || modelManager.ActiveModel == gameObject) {
                m_CharacterLocomotion.OnAnimationUpdate += UpdateAnimationParameters;
            }
        }

        /// <summary>
        /// Initializes the item parameters.
        /// </summary>
        /// <returns>True if the item parameters were initialized.</returns>
        public virtual bool InitializeItemParameters()
        {
            if (m_HasItemParameters) {
                return false;
            }
            // The Animator Controller may not have the item parameters if the character can never equip an item.
            m_HasItemParameters = m_GameObject.GetComponentInChildren<ItemPlacement>(true) != null;
            var inventory = m_GameObject.GetComponent<InventoryBase>();
            if (inventory == null) {
                return false;
            }

            var slotCount = inventory.SlotCount;
            m_ItemSlotID = new int[slotCount];
            m_ItemSlotStateIndex = new int[slotCount];
            m_ItemSlotSubstateIndex = new int[slotCount];
            m_DirtyItemStateIndexParameters = new bool[slotCount];
            m_DirtyItemSubstateIndexParameters = new bool[slotCount];
            m_EquippedItems = new CharacterItem[slotCount];

            m_ItemSlotID = new int[slotCount];
            m_ItemSlotStateIndex = new int[slotCount];
            m_ItemSlotSubstateIndex = new int[slotCount];

            return true;
        }

        /// <summary>
        /// Prepares the Animator parameters for start.
        /// </summary>
        protected virtual void Start()
        {
            SnapAnimations(false);
        }

        /// <summary>
        /// Snaps the animator to the default values.
        /// </summary>
        /// <param name="executeEvent">Should the animator snapped event be executed?</param>
        protected abstract void SnapAnimations(bool executeEvent);

        /// <summary>
        /// Returns true if the specified layer is in transition.
        /// </summary>
        /// <param name="layerIndex">The layer to determine if it is in transition.</param>
        /// <returns>True if the specified layer is in transition.</returns>
        public bool IsInTransition(int layerIndex)
        {
            if (m_Animator == null) {
                return false;
            }

            return m_Animator.IsInTransition(layerIndex);
        }

        /// <summary>
        /// Updates the animation paremters.
        /// </summary>
        protected abstract void UpdateAnimationParameters();

        /// <summary>
        /// Sets the Horizontal Movement parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <returns>True if the parameter was changed.</returns>
        public virtual bool SetHorizontalMovementParameter(float value, float timeScale) { return SetHorizontalMovementParameter(value, timeScale, 0); }

        /// <summary>
        /// Sets the Horizontal Movement parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetHorizontalMovementParameter(float value, float timeScale, float dampingTime);

        /// <summary>
        /// Sets the Forward Movement parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <returns>True if the parameter was changed.</returns>
        public virtual bool SetForwardMovementParameter(float value, float timeScale) { return SetForwardMovementParameter(value, timeScale, 0); }

        /// <summary>
        /// Sets the Forward Movement parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetForwardMovementParameter(float value, float timeScale, float dampingTime);

        /// <summary>
        /// Sets the Pitch parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <returns>True if the parameter was changed.</returns>
        public virtual bool SetPitchParameter(float value, float timeScale) { return SetPitchParameter(value, timeScale, 0); }

        /// <summary>
        /// Sets the Pitch parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetPitchParameter(float value, float timeScale, float dampingTime);

        /// <summary>
        /// Sets the Yaw parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <returns>True if the parameter was changed.</returns>
        public virtual bool SetYawParameter(float value, float timeScale) { return SetYawParameter(value, timeScale, 0); }

        /// <summary>
        /// Sets the Yaw parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetYawParameter(float value, float timeScale, float dampingTime);

        /// <summary>
        /// Sets the Speed parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <returns>True if the parameter was changed.</returns>
        public virtual bool SetSpeedParameter(float value, float timeScale) { return SetSpeedParameter(value, timeScale, 0); }

        /// <summary>
        /// Sets the Speed parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetSpeedParameter(float value, float timeScale, float dampingTime);

        /// <summary>
        /// Sets the Height parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetHeightParameter(float value);

        /// <summary>
        /// Sets the Moving parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetMovingParameter(bool value);

        /// <summary>
        /// Sets the Aiming parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetAimingParameter(bool value);

        /// <summary>
        /// Sets the Movement Set ID parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetMovementSetIDParameter(int value);

        /// <summary>
        /// Sets the Ability Index parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetAbilityIndexParameter(int value);

        /// <summary>
        /// Sets the Ability Change parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetAbilityChangeParameter(bool value);

        /// <summary>
        /// Sets the Int Data parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetAbilityIntDataParameter(int value);

        /// <summary>
        /// Sets the Float Data parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <returns>True if the parameter was changed.</returns>
        public virtual bool SetAbilityFloatDataParameter(float value, float timeScale) { return SetAbilityFloatDataParameter(value, timeScale, 0); }

        /// <summary>
        /// Sets the Float Data parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetAbilityFloatDataParameter(float value, float timeScale, float dampingTime);

        /// <summary>
        /// Sets the Item ID parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot that the item occupies.</param>
        /// <param name="value">The new value.</param>
        public abstract bool SetItemIDParameter(int slotID, int value);

        /// <summary>
        /// Sets the Primary Item State Index parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot that the item occupies.</param>
        /// <param name="value">The new value.</param>
        /// <param name="forceChange">Force the change the new value?</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetItemStateIndexParameter(int slotID, int value, bool forceChange);

        /// <summary>
        /// Sets the Item State Index Change parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot of that item that should be set.</param>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetItemStateIndexChangeParameter(int slotID, bool value);

        /// <summary>
        /// Sets the Item Substate Index parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot that the item occupies.</param>
        /// <param name="value">The new value.</param>
        /// <param name="forceChange">Force the change the new value?</param>
        /// <returns>True if the parameter was changed.</returns>
        public abstract bool SetItemSubstateIndexParameter(int slotID, int value, bool forceChange);


        /// <summary>
        /// The character's ability has been started or stopped.
        /// </summary>
        /// <param name="ability">The ability which was started or stopped.</param>
        /// <param name="active">True if the ability was started, false if it was stopped.</param>
        private void OnAbilityActive(Abilities.Ability ability, bool active)
        {
            UpdateAbilityAnimatorParameters();
        }

        /// <summary>
        /// The character's item ability has been started or stopped.
        /// </summary>
        /// <param name="itemAbility">The ItemAbility activated or deactivated.</param>
        /// <param name="active">True if the ability was started, false if it was stopped.</param>
        private void OnItemAbilityActive(Abilities.Items.ItemAbility itemAbility, bool active)
        {
            UpdateItemAbilityAnimatorParameters(false);
        }

        /// <summary>
        /// The character has started or stopped moving.
        /// </summary>
        /// <param name="moving">True if the character has started to move.</param>
        private void OnMoving(bool moving)
        {
            SetMovingParameter(moving);
        }

        /// <summary>
        /// The character has started or stopped aiming.
        /// </summary>
        /// <param name="aiming">Has the character started to aim?</param>
        private void OnAiming(bool aiming)
        {
            SetAimingParameter(aiming);
        }

        /// <summary>
        /// Updates the ability and item ability parameters if they are dirty.
        /// </summary>
        protected void UpdateDirtyAbilityAnimatorParameters()
        {
            if (m_DirtyAbilityParameters) {
                UpdateAbilityAnimatorParameters(true);
            }
            if (m_DirtyItemAbilityParameters) {
                UpdateItemAbilityAnimatorParameters(true);
            }
        }

        /// <summary>
        /// Sets the ability animator parameters to the ability with the highest priority.
        /// </summary>
        public void UpdateAbilityAnimatorParameters()
        {
            UpdateAbilityAnimatorParameters(false);
        }

        /// <summary>
        /// Sets the ability animator parameters to the ability with the highest priority.
        /// </summary>
        /// <param name="immediateUpdate">Should the parameters be updated immediately?</param>
        public void UpdateAbilityAnimatorParameters(bool immediateUpdate = false)
        {
            // Wait to update until the proper time so the animator synchronizes properly.
            if (!immediateUpdate) {
                m_DirtyAbilityParameters = true;
                return;
            }
            m_DirtyAbilityParameters = false;

            int abilityIndex = 0, intData = 0;
            var floatData = 0f;
            bool setAbilityIndex = true, setStateIndex = true, setAbilityFloatData = true;
            for (int i = 0; i < m_CharacterLocomotion.ActiveAbilityCount; ++i) {
                if (setAbilityIndex && m_CharacterLocomotion.ActiveAbilities[i].AbilityIndexParameter != -1) {
                    abilityIndex = m_CharacterLocomotion.ActiveAbilities[i].AbilityIndexParameter;
                    setAbilityIndex = false;
                }
                if (setStateIndex && m_CharacterLocomotion.ActiveAbilities[i].AbilityIntData != int.MinValue) {
                    intData = m_CharacterLocomotion.ActiveAbilities[i].AbilityIntData;
                    setStateIndex = false;
                }
                if (setAbilityFloatData && m_CharacterLocomotion.ActiveAbilities[i].AbilityFloatData != float.NegativeInfinity) {
                    floatData = m_CharacterLocomotion.ActiveAbilities[i].AbilityFloatData;
                    setAbilityFloatData = false;
                }
            }
            SetAbilityIndexParameter(abilityIndex);
            SetAbilityIntDataParameter(intData);
            SetAbilityFloatDataParameter(floatData, m_CharacterLocomotion.TimeScale);
        }

        /// <summary>
        /// Sets the item animator parameters to the item ability with the highest priority.
        /// </summary>
        /// <param name="forceChange">Should the parameters be forced to be updated?</param>
        public void ExternalUpdateItemAbilityAnimatorParameters(bool forceChange)
        {
            UpdateItemAbilityAnimatorParameters(false, forceChange);
        }

        /// <summary>
        /// Sets the item animator parameters to the item ability with the highest priority.
        /// </summary>
        /// <param name="immediateUpdate">Should the parameters be updated immediately?</param>
        /// <param name="forceChange">Force the trigger to be changed?</param>
        public void UpdateItemAbilityAnimatorParameters(bool immediateUpdate = false, bool forceChange = false)
        {
            if (!m_HasItemParameters) {
                return;
            }

            // Wait to update until the proper time so the animator synchronizes properly.
            if (!immediateUpdate) {
                m_DirtyItemAbilityParameters = true;
                if (forceChange) {
                    m_DirtyItemAbilityParametersForceChange = true;
                }
                return;
            }

            forceChange = forceChange || m_DirtyItemAbilityParametersForceChange;
            m_DirtyItemAbilityParameters = false;
            m_DirtyItemAbilityParametersForceChange = false;

            // Reset the dirty parmaeters for the next use.
            for (int i = 0; i < m_ItemSlotSubstateIndex.Length; ++i) {
                m_DirtyItemStateIndexParameters[i] = m_DirtyItemSubstateIndexParameters[i] = false;
            }

            // The value can only be assigned if it hasn't already been assigned.
            int value;
            for (int i = 0; i < m_CharacterLocomotion.ActiveItemAbilityCount; ++i) {
                for (int j = 0; j < m_ItemSlotSubstateIndex.Length; ++j) {
                    if (!m_DirtyItemStateIndexParameters[j] && (value = m_CharacterLocomotion.ActiveItemAbilities[i].GetItemStateIndex(j)) != -1) {
                        m_DirtyItemStateIndexParameters[j] = true;
                        SetItemStateIndexParameter(j, value, forceChange);
                    }
                    if (!m_DirtyItemSubstateIndexParameters[j] && (value = m_CharacterLocomotion.ActiveItemAbilities[i].GetItemSubstateIndex(j)) != -1) {
                        m_DirtyItemSubstateIndexParameters[j] = true;
                        SetItemSubstateIndexParameter(j, value, forceChange);
                    }
                }
            }

            // The parameter may need to be reset to the default value.
            for (int i = 0; i < m_ItemSlotSubstateIndex.Length; ++i) {
                if (!m_DirtyItemStateIndexParameters[i]) {
                    SetItemStateIndexParameter(i, 0, forceChange);
                }

                if (!m_DirtyItemSubstateIndexParameters[i]) {
                    SetItemSubstateIndexParameter(i, 0, forceChange);
                }
            }

            SetAimingParameter(m_Aiming);
        }

        /// <summary>
        /// Updates the ItemID and MovementSetID parameters to the equipped items.
        /// </summary>
        public void UpdateItemIDParameters()
        {
            if (m_DirtyEquippedItems) {
                var movementSetID = 0;
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    var itemID = 0;
                    if (m_EquippedItems[i] != null) {
                        if (m_EquippedItems[i].DominantItem) {
                            movementSetID = m_EquippedItems[i].AnimatorMovementSetID;
                        }
                        itemID = m_EquippedItems[i].AnimatorItemID;
                    }
                    SetItemIDParameter(i, itemID);
                }
                SetMovementSetIDParameter(movementSetID);
                m_DirtyEquippedItems = false;
            }
        }

        /// <summary>
        /// The specified item will be equipped.
        /// </summary>
        /// <param name="characterItem">The item that will be equipped.</param>
        /// <param name="slotID">The slot that the item will occupy.</param>
        private void OnWillEquipItem(CharacterItem characterItem, int slotID)
        {
            m_EquippedItems[slotID] = characterItem;
            m_DirtyEquippedItems = true;
        }

        /// <summary>
        /// An item has been unequipped.
        /// </summary>
        /// <param name="characterItem">The item that was unequipped.</param>
        /// <param name="slotID">The slot that the item was unequipped from.</param>
        private void OnUnequipItem(CharacterItem characterItem, int slotID)
        {
            if (characterItem != m_EquippedItems[slotID]) {
                return;
            }

            SetItemIDParameter(slotID, 0);
            m_EquippedItems[slotID] = null;
            m_DirtyEquippedItems = true;
        }

        /// <summary>
        /// Executes an event on the EventHandler.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        public void ExecuteEvent(string eventName)
        {
#if UNITY_EDITOR
            if (m_LogEvents) {
                Debug.Log($"{Time.frameCount} Execute {eventName} on GameObject {m_GameObject.name}.");
            }
#endif
            EventHandler.ExecuteEvent(m_GameObject, eventName);
        }

        /// <summary>
        /// Enables or disables the Animator.
        /// </summary>
        /// <param name="enable">Should the animator be enabled?</param>
        public void EnableAnimator(bool enable)
        {
            m_Animator.enabled = enable;
        }

        /// <summary>
        /// The character's model has switched.
        /// </summary>
        /// <param name="activeModel">The active character model.</param>
        private void OnSwitchModels(GameObject activeModel)
        {
            if (activeModel == gameObject) {
                m_CharacterLocomotion.OnAnimationUpdate += UpdateAnimationParameters;
            } else {
                m_CharacterLocomotion.OnAnimationUpdate -= UpdateAnimationParameters;
            }
        }

        /// <summary>
        /// Copies the Animator parameters from the target Animator Monitor.
        /// </summary>
        /// <param name="targetAnimationMonitor">The Animation Monitor whose values should be copied.</param>
        public void CopyParameters(AnimationMonitorBase targetAnimationMonitor)
        {
            m_HorizontalMovement = targetAnimationMonitor.HorizontalMovement;
            m_ForwardMovement = targetAnimationMonitor.ForwardMovement;
            m_Pitch = targetAnimationMonitor.Pitch;
            m_Yaw = targetAnimationMonitor.Yaw;
            m_Speed = targetAnimationMonitor.Speed;
            m_Height = targetAnimationMonitor.Height;
            m_Moving = targetAnimationMonitor.Moving;
            m_Aiming = targetAnimationMonitor.Aiming;
            m_MovementSetID = targetAnimationMonitor.MovementSetID;
            m_AbilityIndex = targetAnimationMonitor.AbilityIndex;
            m_AbilityIntData = targetAnimationMonitor.AbilityIntData;
            m_AbilityFloatData = targetAnimationMonitor.AbilityFloatData;

            if (m_HasItemParameters && targetAnimationMonitor.HasItemParameters) {
                for (int i = 0; i < m_EquippedItems.Length; ++i) {
                    if (targetAnimationMonitor.ItemSlotID.Length <= i) {
                        continue;
                    }
                    m_ItemSlotID[i] = targetAnimationMonitor.ItemSlotID[i];
                    m_ItemSlotStateIndex[i] = targetAnimationMonitor.ItemSlotStateIndex[i];
                    m_ItemSlotSubstateIndex[i] = targetAnimationMonitor.ItemSlotSubstateIndex[i];
                }
            }

            SnapAnimations(false);
        }


        /// <summary>
        /// The GameObject has been destroyed.
        /// </summary>
        private void OnDestroy()
        {
            m_CharacterLocomotion.OnAnimationUpdate -= UpdateAnimationParameters;

            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnCharacterSnapAnimator", SnapAnimations);
            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnCharacterMoving", OnMoving);
            EventHandler.UnregisterEvent<Abilities.Ability, bool>(m_GameObject, "OnCharacterAbilityActive", OnAbilityActive);
            EventHandler.UnregisterEvent<Abilities.Items.ItemAbility, bool>(m_GameObject, "OnCharacterItemAbilityActive", OnItemAbilityActive);
            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnCharacterUpdateAbilityParameters", UpdateAbilityAnimatorParameters);
            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnCharacterUpdateItemAbilityParameters", ExternalUpdateItemAbilityAnimatorParameters);
            EventHandler.UnregisterEvent<CharacterItem, int>(m_GameObject, "OnAbilityWillEquipItem", OnWillEquipItem);
            EventHandler.UnregisterEvent<CharacterItem, int>(m_GameObject, "OnAbilityUnequipItemComplete", OnUnequipItem);
            EventHandler.UnregisterEvent<CharacterItem, int>(m_GameObject, "OnInventoryRemoveItem", OnUnequipItem);
            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnAimAbilityAim", OnAiming);
            EventHandler.UnregisterEvent<GameObject>(m_GameObject, "OnCharacterSwitchModels", OnSwitchModels);
        }
    }
}