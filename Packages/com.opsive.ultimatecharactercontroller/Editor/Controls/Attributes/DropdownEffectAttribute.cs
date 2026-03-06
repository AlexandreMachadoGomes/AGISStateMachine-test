/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.UltimateCharacterController.Editor.Controls.Attributes
{
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls;
    using Opsive.Shared.Editor.UIElements.Controls.Attributes;
    using Opsive.UltimateCharacterController.Editor.Inspectors.Utility;
    using Opsive.UltimateCharacterController.Utility;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine.UIElements;

    /// <summary>
    /// Implements AttributeControlBase for the DropdownSelection attribute.
    /// </summary>
    [ControlType(typeof(DropdownSelectionAttribute))]
    public class DropdownSelectionAttributeControl : AttributeControlBase
    {
        /// <summary>
        /// UIElement for the DropdownSelection.
        /// </summary>
        public class DropdownSelectionObjectStringView : VisualElement
        {
            private List<Type> m_Types;
            private List<string> m_Names;

            /// <summary>
            /// DropdownSelectionObjectStringView constructor.
            /// </summary>
            /// <param name="baseType">The base type that the dropdown represents.</param>
            /// <param name="field">The field responsible for the control.</param>
            /// <param name="target">The object that should have its fields displayed.</param>
            /// <param name="value">The value of the control.</param>
            /// <param name="label">The label of the control.</param>
            /// <param name="tooltip">The tooltip that should be added to the control.</param>
            /// <param name="onChangeEvent">An event that is sent when the value changes. Returns false if the control cannot be changed.</param>
            public DropdownSelectionObjectStringView(Type baseType, FieldInfo field, object target, object value, string label, string tooltip, Func<object, bool> onChangeEvent)
            {
                PopulateTypes(baseType);
                var stringValue = value as string;
                var index = 0;
                if (!string.IsNullOrEmpty(stringValue)) {
                    for (int i = 0; i < m_Types.Count; ++i) {
                        if (m_Types[i].FullName.Equals(stringValue)) {
                            index = i + 1;
                            break;
                        }
                    }
                }
                var dropdownField = new DropdownField(m_Names, index);
                var labelControl = new LabelControl(label, tooltip, dropdownField, LabelControl.WidthAdjustment.UnityDefault);
                System.Action<object> onBindingUpdateEvent = (object newValue) => {
                    var stringValue = newValue as string;
                    if (string.IsNullOrEmpty(stringValue)) {
                        stringValue = "(none)";
                    } else {
                        stringValue = InspectorUtility.DisplayTypeName(Shared.Utility.TypeUtility.GetType(stringValue), false);
                    }
                    dropdownField.SetValueWithoutNotify(stringValue);
                };
                dropdownField.RegisterCallback<AttachToPanelEvent>(c =>
                {
                    BindingUpdater.AddBinding(field, -1, target, onBindingUpdateEvent);
                });
                dropdownField.RegisterCallback<DetachFromPanelEvent>(c =>
                {
                    BindingUpdater.RemoveBinding(onBindingUpdateEvent);
                });
                dropdownField.RegisterValueChangedCallback(c =>
                {
                    dropdownField.SetValueWithoutNotify(c.newValue);
                    c.StopPropagation();
                    onChangeEvent(dropdownField.index > 0 ? m_Types[dropdownField.index - 1].FullName : string.Empty);
                });
                Add(labelControl);
            }

            /// <summary>
            /// Populates the types.
            /// </summary>
            /// <param name="baseType">The base type.</param>
            private void PopulateTypes(Type baseType)
            {
                if (m_Types != null) {
                    return;
                }

                m_Types = new List<Type>();
                m_Names = new List<string>();
                m_Names.Add("(none)");
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; ++i) {
                    var assemblyTypes = assemblies[i].GetTypes();
                    for (int j = 0; j < assemblyTypes.Length; ++j) {
                        if (baseType.IsAssignableFrom(assemblyTypes[j]) && !assemblyTypes[j].IsAbstract) {
                            m_Types.Add(assemblyTypes[j]);
                            m_Names.Add(InspectorUtility.DisplayTypeName(assemblyTypes[j], true));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Does the attribute override the type control?
        /// </summary>
        public override bool OverrideTypeControl { get { return true; } }

        /// <summary>
        /// Does the control use a label?
        /// </summary>
        public override bool UseLabel { get { return false; } }

        /// <summary>
        /// Returns the attribute control that should be used for the specified AttributeControlType.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(AttributeControlInput input)
        {
            return new DropdownSelectionObjectStringView(input.Field.GetCustomAttribute<DropdownSelectionAttribute>().BaseType, input.Field, input.Target, input.Value, input.Label, input.Tooltip, input.OnChangeEvent);
        }
    }
}