/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Editor.Managers
{
    using Opsive.Shared.Editor.Inspectors.Utility;
    using Opsive.Shared.Editor.Managers;
    using Opsive.UltimateCharacterController.Editor.Controls.Types.AbilityDrawers;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Draws the inspector for an ability add-on that has been installed.
    /// </summary>
    public abstract class AbilityAddOnInspector : AddOnInspector
    {
        public abstract string AddOnName { get; }
        public abstract string AbilityNamespace { get; }
        public abstract bool ShowFirstPersonAnimatorController { get; }

        private GameObject m_Character;
        private bool m_AddAbilities = true;
        private bool m_AddAnimations = true;
        private AnimatorController m_AnimatorController;
        private AnimatorController m_FirstPersonAnimatorController;
        private Button m_BuildButton;

        public GameObject Character { get { return m_Character; } set { m_Character = value; } }
        public bool AddAbilities { get { return m_AddAbilities; } set { m_AddAbilities = value; } }
        public bool AddAnimations { get { return m_AddAnimations; } set { m_AddAnimations = value; } }
        public AnimatorController AnimatorController { get { return m_AnimatorController; } set { m_AnimatorController = value; } }
        public AnimatorController FirstPersonAnimatorController { get { return m_FirstPersonAnimatorController; } set { m_FirstPersonAnimatorController = value; } }
        public Button BuildButton { get { return m_BuildButton; } set { m_BuildButton = value; } }

        /// <summary>
        /// Draws the add-on inspector.
        /// </summary>
        /// <param name="container">The parent VisualElement container.</param>
        public override void ShowInspector(VisualElement container)
        {
            m_BuildButton = ManagerUtility.ShowControlBox(AddOnName + " Abilities & Animations", "This option will add the " + AddOnName.ToLower() + " abilities or animations to your character.",
                            ShowAgentSetup, "Setup Character", SetupCharacter, container, true, 4);
            m_BuildButton.SetEnabled(CanSetupCharacter());
        }

        /// <summary>
        /// Draws the additional controls for the animator.
        /// </summary>
        private void ShowAgentSetup(VisualElement container)
        {
            container.Clear();

            var characterField = new ObjectField("Character");
            characterField.objectType = typeof(GameObject);
            characterField.allowSceneObjects = true;
            characterField.value = m_Character;
            characterField.RegisterValueChangedCallback(c =>
            {
                m_Character = (GameObject)c.newValue;
                m_AnimatorController = null;
                if (m_Character != null) {
                    var animatorMonitor = m_Character.GetComponentInChildren<Character.AnimatorMonitor>(true);
                    if (animatorMonitor != null) {
                        var animator = animatorMonitor.GetComponent<Animator>();
                        if (animator != null) {
                            m_AnimatorController = (AnimatorController)animator.runtimeAnimatorController;
                        }
                    }
#if FIRST_PERSON_CONTROLLER
                    var firstPersonBaseObjects = m_Character.GetComponentsInChildren<FirstPersonController.Character.Identifiers.FirstPersonBaseObject>(true);
                    if (firstPersonBaseObjects != null && firstPersonBaseObjects.Length > 0) {
                        var firstPersonBaseObject = firstPersonBaseObjects[0];
                        // Choose the base object with the lowest ID.
                        for (int i = 1; i < firstPersonBaseObjects.Length; ++i) {
                            if (firstPersonBaseObjects[i].ID < firstPersonBaseObject.ID) {
                                firstPersonBaseObject = firstPersonBaseObjects[i];
                            }
                        }

                        var animator = firstPersonBaseObject.GetComponent<Animator>();
                        if (animator != null) {
                            m_FirstPersonAnimatorController = (AnimatorController)animator.runtimeAnimatorController;
                        }
                    }
#endif
                }

                ShowAgentSetup(container);
            });
            container.Add(characterField);

            // The character must first be created by the Character Manager.
            if (m_Character != null && m_Character.GetComponent<Character.UltimateCharacterLocomotion>() == null) {
                var helpBox = new HelpBox("The character must first be setup by the Character Manager.", HelpBoxMessageType.Error);
                container.Add(helpBox);
            }

            var addAbilitiesToggle = new Toggle("Add Abilities");
            addAbilitiesToggle.value = m_AddAbilities;
            addAbilitiesToggle.RegisterValueChangedCallback(c =>
            {
                m_AddAbilities = c.newValue;
                ShowAgentSetup(container);
            });
            container.Add(addAbilitiesToggle);

            var addAnimationsToggle = new Toggle("Add Animations");
            addAnimationsToggle.value = m_AddAnimations;
            addAnimationsToggle.RegisterValueChangedCallback(c =>
            {
                m_AddAnimations = c.newValue;
                ShowAgentSetup(container);
            });
            container.Add(addAnimationsToggle);

            if (m_AddAnimations) {
                var animatorControllerField = new ObjectField("Animator Controller");
                animatorControllerField.Q<Label>().AddToClassList("indent");
                animatorControllerField.objectType = typeof(AnimatorController);
                animatorControllerField.value = m_AnimatorController;
                animatorControllerField.RegisterValueChangedCallback(c =>
                {
                    m_AnimatorController = c.newValue as AnimatorController;
                    ShowAgentSetup(container);
                });
                container.Add(animatorControllerField);

                m_AnimatorController = ClampAnimatorControllerField("Animator Controller", m_AnimatorController, 33);
#if FIRST_PERSON_CONTROLLER
                if (ShowFirstPersonAnimatorController) {
                    animatorControllerField = new ObjectField("First Person Animator Controller");
                    animatorControllerField.Q<Label>().AddToClassList("indent");
                    animatorControllerField.objectType = typeof(AnimatorController);
                    animatorControllerField.value = m_FirstPersonAnimatorController;
                    animatorControllerField.RegisterValueChangedCallback(c =>
                    {
                        m_FirstPersonAnimatorController = c.newValue as AnimatorController;
                        ShowAgentSetup(container);
                    });
                    container.Add(animatorControllerField);
                }
#endif
            }

            if (m_BuildButton != null) {
                m_BuildButton.SetEnabled(CanSetupCharacter());
            }
        }

        /// <summary>
        /// Returns true if the character can be setup.
        /// </summary>
        /// <returns>True if the character can be setup.</returns>
        private bool CanSetupCharacter()
        {
            if (m_Character == null || m_Character.GetComponent<Character.UltimateCharacterLocomotion>() == null) {
                return false;
            }

            if (m_AddAnimations && m_AnimatorController == null) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Adds the abilities and animations to the animator controllers.
        /// </summary>
        private void SetupCharacter()
        {
            var types = InspectorDrawerUtility.GetAllTypesWithinNamespace(AbilityNamespace);
            if (types == null) {
                return;
            }

            if (m_AddAbilities) {
                var characterLocomotion = m_Character.GetComponent<Character.UltimateCharacterLocomotion>();
                var abilities = characterLocomotion.Abilities;
                // Call AbilityBuilder on all of the abilities.
                for (int i = 0; i < types.Count; ++i) {
                    if (!typeof(Character.Abilities.Ability).IsAssignableFrom(types[i])) {
                        continue;
                    }
                    var hasAbility = false;
                    // Do not add duplicates.
                    for (int j = 0; j < abilities.Length; ++j) {
                        if (abilities[j] != null && abilities[j].GetType() == types[i]) {
                            hasAbility = true;
                            break;
                        }
                    }
                    if (hasAbility) {
                        continue;
                    }
                    UltimateCharacterController.Utility.Builders.AbilityBuilder.AddAbility(characterLocomotion, types[i], i);
                }
                Shared.Editor.Utility.EditorUtility.SetDirty(characterLocomotion);
            }

            if (m_AddAnimations) {
                // Call BuildAnimator on all of the inspector drawers for the abilities.
                for (int i = 0; i < types.Count; ++i) {
                    var abilityDrawer = AbilityDrawerUtility.FindAbilityDrawer(types[i], true);
                    if (abilityDrawer == null || !abilityDrawer.CanBuildAnimator) {
                        continue;
                    }

                    abilityDrawer.BuildAnimator(new AnimatorController[] { m_AnimatorController }, new AnimatorController[] { m_FirstPersonAnimatorController });
                }
            }

            Debug.Log("The character was successfully setup." + (m_AddAbilities ? " Refer to the documentation for the steps to configure the abilities." : string.Empty));
        }

        /// <summary>
        /// Prevents the label from being too far away from the object field.
        /// </summary>
        /// <param name="label">The animator controller label.</param>
        /// <param name="animatorController">The animator controller value.</param>
        /// <param name="widthAddition">Any additional width to separate the label and the control.</param>
        /// <returns>The new animator controller.</returns>
        private static AnimatorController ClampAnimatorControllerField(string label, AnimatorController animatorController, int widthAddition)
        {
            var textDimensions = GUI.skin.label.CalcSize(new GUIContent(label));
            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = textDimensions.x + widthAddition;
            animatorController = EditorGUILayout.ObjectField(label, animatorController, typeof(AnimatorController), true) as AnimatorController;
            EditorGUIUtility.labelWidth = prevLabelWidth;
            return animatorController;
        }
    }

    /// <summary>
    /// Draws a list of all of the available add-ons.
    /// </summary>
    [OrderedEditorItem("Add-Ons", 11)]
    public class AddOnsManager : Opsive.Shared.Editor.Managers.AddOnsManager
    {
        protected override string AddOnsURL => "https://opsive.com/asset/UltimateCharacterController/AddOnsList.txt";
    }
}