/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Editor.Managers
{
    using Opsive.Shared.Editor.Managers;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Game;
    using Opsive.Shared.Editor.Inspectors.Input;
    using Opsive.Shared.StateSystem;
    using Opsive.UltimateCharacterController.Camera;
    using Opsive.UltimateCharacterController.Editor.Inspectors.Utility;
    using Opsive.UltimateCharacterController.Game;
    using Opsive.UltimateCharacterController.Utility;
    using Opsive.UltimateCharacterController.Utility.Builders;
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// The SetupManager shows any project or scene related setup options.
    /// </summary>
    [OrderedEditorItem("Setup", 1)]
    public class SetupManager : Manager
    {
        private const string c_MonitorsPrefabGUID = "b5bf2e4077598914b83fc5e4ca20f2f4";
        private const string c_OnScreenControlsPrefabGUID = "4a06fe682d511f14ea708ae61a985b92";
        private const string c_VirtualControlsPrefabGUID = "33d3d57ba5fc7484c8d09150e45066a4";
        private const string c_3DAudioManagerModuleGUID = "7c2f6e9d4d7571042964493904b06c50";
        private const string c_ObjectFaderAimStateGUID = "5c1fe60fde7c54e48ad118439bf49b9b";
        private const string c_URPPackageGUID = "95d1ccc37ca9fa7438a710a8e25867cf";
        private const string c_HDRPPackageGUID = "d85bbc3a58fa48d4198fce75244d0532";

        /// <summary>
        /// Specifies the perspective that the ViewType can change into.
        /// </summary>
        private enum Perspective
        {
            First,  // The ViewType can only be in first person perspective.
            Third,  // The ViewType can only be in third person perspective.
            Both,   // The ViewType can be in first or third person perspective.
            None    // Default value.
        }

        /// <summary>
        /// Specifies which tab should be shown.
        /// </summary>
        private enum TabSelection
        {
            Scene,  // Scene setup.
            Sample, // Sample setup.
            Project // Project setup.
        }

        /// <summary>
        /// Specifies which type of virtual controls should be shown.
        /// </summary>
        private enum InputType
        {
            InputSystem,    // On-Screen controls.
            InputManager    // Legacy virtual controls.
        }

        private string[] m_ToolbarStrings = { "Scene", "Sample", "Project" };
        [SerializeField] private TabSelection m_Selection = TabSelection.Scene;

        [SerializeField] private Perspective m_Perspective = Perspective.None;
        [SerializeField] private string m_FirstPersonViewType;
        [SerializeField] private string m_ThirdPersonViewType;
        [SerializeField] private bool m_StartFirstPersonPerspective;
        [SerializeField] private bool m_CanSetupCamera;
        [SerializeField] private InputType m_VirtualControlsInputType;

        private TabToolbar m_TabToolbar;
        private List<Type> m_FirstPersonViewTypes = new List<Type>();
        private List<string> m_FirstPersonViewTypeStrings = new List<string>();
        private List<Type> m_ThirdPersonViewTypes = new List<Type>();
        private List<string> m_ThirdPersonViewTypeStrings = new List<string>();
        private List<string> m_PerspectiveNames = new List<string>() { "First", "Third", "Both" };
        private VisualElement m_Container;
        private PopupField<string> m_RenderPipelinePopup;

        /// <summary>
        /// Initializes the manager after deserialization.
        /// </summary>
        /// <param name="mainManagerWindow">A reference to the Main Manager Window.</param>
        public override void Initialize(MainManagerWindow mainManagerWindow)
        {
            base.Initialize(mainManagerWindow);

            // Set the default perspective based on what asset is installed.
            if (m_Perspective == Perspective.None) {
#if FIRST_PERSON_CONTROLLER
                m_Perspective = Perspective.First;
#elif THIRD_PERSON_CONTROLLER
                m_Perspective = Perspective.Third;
#endif
            }

            // Get a list of the available view types.
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; ++i) {
                try {
                    var assemblyTypes = assemblies[i].GetTypes();
                    for (int j = 0; j < assemblyTypes.Length; ++j) {
                        // Must derive from ViewType.
                        if (!typeof(UltimateCharacterController.Camera.ViewTypes.ViewType).IsAssignableFrom(assemblyTypes[j])) {
                            continue;
                        }

                        // Ignore abstract classes.
                        if (assemblyTypes[j].IsAbstract) {
                            continue;
                        }

                        if (assemblyTypes[j].FullName.Contains("FirstPersonController")) {
                            m_FirstPersonViewTypes.Add(assemblyTypes[j]);
                        } else if (assemblyTypes[j].FullName.Contains("ThirdPersonController")) {
                            m_ThirdPersonViewTypes.Add(assemblyTypes[j]);
                        }
                    }
                } catch (Exception) {
                    continue;
                }
            }

            // Create an array of display names for the popup.
            for (int i = 0; i < m_FirstPersonViewTypes.Count; ++i) {
                m_FirstPersonViewTypeStrings.Add(InspectorUtility.DisplayTypeName(m_FirstPersonViewTypes[i], true));
            }
            for (int i = 0; i < m_ThirdPersonViewTypes.Count; ++i) {
                m_ThirdPersonViewTypeStrings.Add(InspectorUtility.DisplayTypeName(m_ThirdPersonViewTypes[i], true));
            }
        }

        /// <summary>
        /// Opens the Project Setup tab.
        /// </summary>
        public void OpenProjectSetup()
        {
            m_Container.Clear();
            m_Selection = TabSelection.Project;
            m_TabToolbar.Selected = (int)m_Selection;
            ShowProjectSetup();
        }

        /// <summary>
        /// Adds the visual elements to the ManagerContentContainer visual element. 
        /// </summary>
        public override void BuildVisualElements()
        {
            m_TabToolbar = new TabToolbar(m_ToolbarStrings, (int)m_Selection, (int selected) =>
            {
                m_Selection = (TabSelection)selected;
                m_Container.Clear();
                if (m_Selection == TabSelection.Scene) {
                    ShowSceneSetup();
                } else if (m_Selection == TabSelection.Sample) {
                    ShowSampleSetup();
                } else {
                    ShowProjectSetup();
                }
            }, true);
            m_ManagerContentContainer.Add(m_TabToolbar);

            m_Container = new VisualElement();
            m_ManagerContentContainer.Add(m_Container);

            if (m_Selection == TabSelection.Scene) {
                ShowSceneSetup();
            } else if (m_Selection == TabSelection.Sample) {
                ShowSampleSetup();
            } else {
                ShowProjectSetup();
            }
        }

        /// <summary>
        /// Shows the scene setup control boxes.
        /// </summary>
        private void ShowSceneSetup()
        {
            ManagerUtility.ShowControlBox("Manager Setup", "Adds the scene-level manager components to the scene.", null, "Add Managers", AddManagers, m_Container, true);
            ManagerUtility.ShowControlBox("Camera Setup", "Sets up the camera within the scene to use the Ultimate Character Controller Camera Controller component.", ShowCameraSetup,
                                    "Setup Camera", SetupCamera, m_Container, true);
            ManagerUtility.ShowControlBox("UI Setup", "Adds the UI monitors to the scene.", null, "Add UI", AddUI, m_Container, true);
            ManagerUtility.ShowControlBox("Virtual Controls Setup", "Adds the virtual controls to the scene.", ShowOnScreenControlsSetup, "Add Virtual Controls", AddVirtualControls, m_Container, true);
        }

        /// <summary>
        /// Shows the samples setup control boxes.
        /// </summary>
        private void ShowSampleSetup()
        {
            ManagerUtility.ShowControlBox("Import Sample", "Imports the sample scene. This scene requires URP 14.0.11+ and the Input System.", null, "Import Sample", () =>
            {
                if (Shared.Editor.Managers.ManagerUtility.ImportSample(AssetInfo.PackageName)) {
                    ImportRenderPipeline(true);
                }
            }, m_Container, true);
        }

        /// <summary>
        /// Shows the project setup control boxes.
        /// </summary>
        private void ShowProjectSetup()
        {
            // Show a warning if the button mappings, layers or URP have not been updated.
            var serializedObject = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);
            var axisProperty = serializedObject.FindProperty("m_Axes");
            var hasInputs = UnityInputBuilder.FindAxisProperty(axisProperty, "Action", string.Empty, string.Empty, UnityInputBuilder.AxisType.KeyMouseButton, false) != null && 
                            UnityInputBuilder.FindAxisProperty(axisProperty, "Crouch", string.Empty, string.Empty, UnityInputBuilder.AxisType.KeyMouseButton, false) != null;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProperty = tagManager.FindProperty("layers");
            var hasLayers = layersProperty.GetArrayElementAtIndex(LayerManager.Character).stringValue == "Character";

            var hasRenderPipeline = (UnityEngineUtility.GetActiveRenderPipeline() != UnityEngineUtility.RenderPipeline.BuiltIn) && (ManagerUtility.FindInvisibleShadowCaster() != null);

            var importParent = new VisualElement();
            var samplesParent = new VisualElement();
            m_Container.Add(importParent);
            m_Container.Add(samplesParent);

            if (!hasInputs || !hasLayers || !hasRenderPipeline) {
                string helpText;
                string buttonText;
                if (UnityEngineUtility.GetActiveRenderPipeline() != UnityEngineUtility.RenderPipeline.BuiltIn) {
                    helpText = "The default button mappings, layers or render pipeline assets have not been updated.\nIf you are just getting started you should update these with the button below.";
                    buttonText = "Update Buttons, Layers, and Render Pipeline";
                } else {
                    helpText = "The default button mappings or layers have not been updated.\nIf you are just getting started you should update the button mappings and layers with the button below. ";
                    buttonText = "Update Buttons and Layers";
                }
                Shared.Editor.Managers.ManagerUtility.AddHelpBox(importParent, helpText, HelpBoxMessageType.Warning, buttonText, (HelpBox helpBox, Button actionButton) =>
                {
                    Utility.CharacterInputBuilder.UpdateInputManager();
                    UpdateLayers();
                    if (UnityEngineUtility.GetActiveRenderPipeline() != UnityEngineUtility.RenderPipeline.BuiltIn) {
                        ImportRenderPipeline(UnityEngineUtility.GetActiveRenderPipeline() == UnityEngineUtility.RenderPipeline.URP);
                    }
                    importParent.style.display = DisplayStyle.None;
                    samplesParent.style.display = DisplayStyle.Flex;
                });

                // The samples box will be shown after the import.
                samplesParent.style.display = DisplayStyle.None;
            }

            Shared.Editor.Managers.ManagerUtility.ShowSamplesHelpBox(samplesParent, AssetInfo.PackageName, () => { ImportRenderPipeline(true); });

            ManagerUtility.ShowControlBox("Layers", "Update the project layers to the default character controller layers. The layers do not need to be updated " +
                            "if you have already setup a custom set of layers.", null, "Update Layers", UpdateLayers, m_Container, true);

            ManagerUtility.ShowControlBox("Render Pipeline", "Import support packages for the universal or high definition render pipeline.", ShowRenderPipelines, "Import", ImportRenderPipeline, m_Container, true);

            ManagerUtility.ShowControlBox("Input Manager Button Mappings", "Add the default button mappings to the Unity Input Manager. If you are using the input system, custom button mappings, or " +
                            "another input integration then you do not need to update the Unity button mappings.", null, "Update Buttons",
                            Utility.CharacterInputBuilder.UpdateInputManager, m_Container, true);
        }

        /// <summary>
        /// Shows the camera setup fields.
        /// </summary>
        /// <param name="container">The VisualElement that contains the setup fields.</param>
        private void ShowCameraSetup(VisualElement container)
        {
            // Draw the perspective.
            var selectedPerspectivePopup = new PopupField<string>("Perspective", m_PerspectiveNames, (int)m_Perspective);
            selectedPerspectivePopup.RegisterValueChangedCallback(c =>
            {
                m_Container.Clear();
                m_Perspective = (Perspective)selectedPerspectivePopup.index;
                if (m_Selection == TabSelection.Scene) {
                    ShowSceneSetup();
                } else if (m_Selection == TabSelection.Sample) {
                    ShowSampleSetup();
                } else {
                    ShowProjectSetup();
                }
            });
            container.Add(selectedPerspectivePopup);
            m_CanSetupCamera = true;
            // Determine if the selected perspective is supported.
#if !FIRST_PERSON_CONTROLLER
            if (m_Perspective == Perspective.First || m_Perspective == Perspective.Both) {
                var helpBox = new HelpBox();
                helpBox.messageType = HelpBoxMessageType.Error;
                helpBox.text = "Unable to select the First Person Controller perspective. If you'd like to use a first person perspective ensure the " +
                                        "First Person Controller is imported.";
                helpBox.Q<Label>().style.fontSize = 12;
                container.Add(helpBox);
                m_CanSetupCamera = false;
            }
#endif
#if !THIRD_PERSON_CONTROLLER
            if (m_Perspective == Perspective.Third || m_Perspective == Perspective.Both) {
                var helpBox = new HelpBox();
                helpBox.messageType = HelpBoxMessageType.Error;
                helpBox.text = "Unable to select the Third Person Controller perspective. If you'd like to use a third person perspective ensure the " +
                                        "Third Person Controller is imported.";
                helpBox.Q<Label>().style.fontSize = 12;
                container.Add(helpBox);
                m_CanSetupCamera = false;
            }
#endif
            if (!m_CanSetupCamera) {
                return;
            }

            // Show the available first person ViewTypes.
            if (m_Perspective == Perspective.First || m_Perspective == Perspective.Both) {
                var selectedViewType = -1;
                for (int i = 0; i < m_FirstPersonViewTypes.Count; ++i) {
                    if (m_FirstPersonViewTypes[i].FullName == m_FirstPersonViewType) {
                        selectedViewType = i;
                        break;
                    }
                }
                if (selectedViewType == -1) {
                    selectedViewType = 0;
                    m_FirstPersonViewType = m_FirstPersonViewTypes[0].FullName;
                }
                var selectedViewTypePopup = new PopupField<string>("First Person View Type", m_FirstPersonViewTypeStrings, selectedViewType);
                selectedViewTypePopup.RegisterValueChangedCallback(c =>
                {
                    m_FirstPersonViewType = m_FirstPersonViewTypes[selectedViewTypePopup.index].FullName;

                });
                container.Add(selectedViewTypePopup);
            }
            // Show the available third person ViewTypes.
            if (m_Perspective == Perspective.Third || m_Perspective == Perspective.Both) {
                var selectedViewType = -1;
                for (int i = 0; i < m_ThirdPersonViewTypes.Count; ++i) {
                    if (m_ThirdPersonViewTypes[i].FullName == m_ThirdPersonViewType) {
                        selectedViewType = i;
                        break;
                    }
                }
                if (selectedViewType == -1) {
                    selectedViewType = 0;
                    m_ThirdPersonViewType = m_ThirdPersonViewTypes[0].FullName;
                }
                var selectedViewTypePopup = new PopupField<string>("Third Person View Type", m_ThirdPersonViewTypeStrings, selectedViewType);
                selectedViewTypePopup.RegisterValueChangedCallback(c =>
                {
                    m_ThirdPersonViewType = m_ThirdPersonViewTypes[selectedViewTypePopup.index].FullName;

                });
                container.Add(selectedViewTypePopup);
            }
            if (m_Perspective == Perspective.Both) {
                var startPerspectivePopup = new PopupField<string>("Start Perspective", new List<string>() { "First Person", "Third Person" }, m_StartFirstPersonPerspective ? 0 : 1);
                startPerspectivePopup.RegisterValueChangedCallback(c =>
                {
                    m_StartFirstPersonPerspective = startPerspectivePopup.index == 0;
                });
                container.Add(startPerspectivePopup);
            } else {
                m_StartFirstPersonPerspective = (m_Perspective == Perspective.First);
            }
        }

        /// <summary>
        /// Sets up the camera if it hasn't already been setup.
        /// </summary>
        private void SetupCamera()
        {
            if (!m_CanSetupCamera) {
                return;
            }

            // Setup the camera.
            GameObject cameraGameObject;
            var addedCameraController = false;
            var camera = UnityEngine.Camera.main;
            if (camera == null) {
                // If the main camera can't be found then use the first available camera.
                var cameras = UnityEngine.Camera.allCameras;
                if (cameras != null && cameras.Length > 0) {
                    // Prefer cameras that are at the root level.
                    for (int i = 0; i < cameras.Length; ++i) {
                        if (cameras[i].transform.parent == null) {
                            camera = cameras[i];
                            break;
                        }
                    }
                    // No cameras are at the root level. Set the first available camera.
                    if (camera == null) {
                        camera = cameras[0];
                    }
                }

                // A new camera should be created if there isn't a valid camera.
                if (camera == null) {
                    cameraGameObject = new GameObject("Camera");
                    cameraGameObject.tag = "MainCamera";
                    camera = cameraGameObject.AddComponent<UnityEngine.Camera>();
                    cameraGameObject.AddComponent<AudioListener>();
                }
            }

            // The near clip plane should adjusted for viewing close objects.
            camera.nearClipPlane = 0.01f;

            // Add the CameraController if it isn't already added.
            cameraGameObject = camera.gameObject;
            if (cameraGameObject.GetComponent<CameraController>() == null) {
                var cameraController = cameraGameObject.AddComponent<CameraController>();
                if (m_Perspective == Perspective.Both) {
                    ViewTypeBuilder.AddViewType(cameraController, typeof(UltimateCharacterController.Camera.ViewTypes.Transition));
                }
                if (m_StartFirstPersonPerspective) {
                    if (m_Perspective != Perspective.First && !string.IsNullOrEmpty(m_ThirdPersonViewType)) {
                        ViewTypeBuilder.AddViewType(cameraController, Shared.Utility.TypeUtility.GetType(m_ThirdPersonViewType));
                    }
                    if (m_Perspective != Perspective.Third && !string.IsNullOrEmpty(m_FirstPersonViewType)) {
                        ViewTypeBuilder.AddViewType(cameraController, Shared.Utility.TypeUtility.GetType(m_FirstPersonViewType));
                    }
                } else {
                    if (m_Perspective != Perspective.Third && !string.IsNullOrEmpty(m_FirstPersonViewType)) {
                        ViewTypeBuilder.AddViewType(cameraController, Shared.Utility.TypeUtility.GetType(m_FirstPersonViewType));
                    }
                    if (m_Perspective != Perspective.First && !string.IsNullOrEmpty(m_ThirdPersonViewType)) {
                        ViewTypeBuilder.AddViewType(cameraController, Shared.Utility.TypeUtility.GetType(m_ThirdPersonViewType));
                    }
                }

                // Detect if a character exists in the scene. Automatically add the character if it does.
#if UNITY_2023_1_OR_NEWER
                var characters = UnityEngine.Object.FindObjectsByType<UltimateCharacterController.Character.CharacterLocomotion>(FindObjectsSortMode.None);
#else
                var characters = UnityEngine.Object.FindObjectsOfType<UltimateCharacterController.Character.CharacterLocomotion>();
#endif
                if (characters != null && characters.Length == 1) {
                    cameraController.InitCharacterOnAwake = true;
                    cameraController.Character = characters[0].gameObject;
                }

                // Setup the components which help the Camera Controller.
                Shared.Editor.Inspectors.Utility.InspectorUtility.AddComponent<CameraControllerHandler>(cameraGameObject);
#if THIRD_PERSON_CONTROLLER
                if (m_Perspective != Perspective.First) {
                    var objectFader = Shared.Editor.Inspectors.Utility.InspectorUtility.AddComponent<ThirdPersonController.Camera.ObjectFader>(cameraGameObject);
                    if (UnityEngineUtility.GetActiveRenderPipeline() != UnityEngineUtility.RenderPipeline.BuiltIn) {
                        objectFader.ColorPropertyName = "_BaseColor";
                        objectFader.ModePropertyName = "_Surface";
                        objectFader.ModeTransparentValue = 1;
                    }

                    if (!Application.isPlaying) {
                        // The Moving and Move Towards states should automatically be added.
                        var aimPresetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(c_ObjectFaderAimStateGUID);
                        if (!string.IsNullOrEmpty(aimPresetPath)) {
                            var aimPreset = UnityEditor.AssetDatabase.LoadAssetAtPath(aimPresetPath, typeof(PersistablePreset)) as PersistablePreset;
                            if (aimPreset != null) {
                                var states = objectFader.States;
                                System.Array.Resize(ref states, states.Length + 1);
                                // Default must always be at the end.
                                states[states.Length - 1] = states[0];
                                states[states.Length - 2] = new State("Aim", aimPreset, null);
                                objectFader.States = states;
                            }
                        }
                    }
                }
#endif
                addedCameraController = true;
            }

            if (addedCameraController) {
                Debug.Log("The Camera Controller has been added.");
                if (m_Perspective != Perspective.Third && UnityEngineUtility.GetActiveRenderPipeline() != UnityEngineUtility.RenderPipeline.BuiltIn) {
                    if (UnityEngineUtility.GetActiveRenderPipeline() == UnityEngineUtility.RenderPipeline.URP) {
                        Debug.Log("See this page for instructions on how to setup your first person URP renderer: https://opsive.com/support/documentation/ultimate-character-controller/integrations/universal-render-pipeline/");
                    } else {
                        Debug.Log("See this page for instructions on how to setup your first person HDRP renderer: https://opsive.com/support/documentation/ultimate-character-controller/integrations/high-definition-render-pipeline/");
                    }
                }
            } else {
                Debug.LogWarning("Warning: No action was performed, the Camera Controller component has already been added.");
            }
        }

        /// <summary>
        /// Adds the singleton manager components.
        /// </summary>
        public static void AddManagers()
        {
            // Create the "Game" components if it doesn't already exists.
            GameObject gameGameObject;
#if UNITY_2023_1_OR_NEWER
            var scheduler = UnityEngine.Object.FindFirstObjectByType<SchedulerBase>();
#else
            var scheduler = UnityEngine.Object.FindObjectOfType<SchedulerBase>();
#endif
            if (scheduler == null) {
                gameGameObject = new GameObject("Game");
            } else {
                gameGameObject = scheduler.gameObject;
            }

            // Add the singletons.
            var surfaceManager = Shared.Editor.Inspectors.Utility.InspectorUtility.AddComponent<SurfaceSystem.SurfaceManager>(gameGameObject);
            if (UnityEngineUtility.GetActiveRenderPipeline() != UnityEngineUtility.RenderPipeline.BuiltIn) {
                surfaceManager.MainTexturePropertyName = "_BaseMap";
            }

            Shared.Editor.Inspectors.Utility.InspectorUtility.AddComponent<SurfaceSystem.DecalManager>(gameGameObject);
            Shared.Editor.Inspectors.Utility.InspectorUtility.AddComponent<SimulationManager>(gameGameObject);
            Shared.Editor.Inspectors.Utility.InspectorUtility.AddComponent<ObjectPool>(gameGameObject);
            Shared.Editor.Inspectors.Utility.InspectorUtility.AddComponent<Scheduler>(gameGameObject);
            var audiomanager = Shared.Editor.Inspectors.Utility.InspectorUtility.AddComponent<Shared.Audio.AudioManager>(gameGameObject);
            if (audiomanager.AudioManagerModule == null) {
                var defaultAudioManagerModulePath = AssetDatabase.GUIDToAssetPath(c_3DAudioManagerModuleGUID);
                if (!string.IsNullOrEmpty(defaultAudioManagerModulePath)) {
                    var audioManagerModule = AssetDatabase.LoadAssetAtPath(defaultAudioManagerModulePath, typeof(Shared.Audio.AudioManagerModule)) as Shared.Audio.AudioManagerModule;
                    audiomanager.AudioManagerModule = audioManagerModule;
                }
            }

            Shared.Editor.Inspectors.Utility.InspectorUtility.AddComponent<SpawnPointManager>(gameGameObject);
            Shared.Editor.Inspectors.Utility.InspectorUtility.AddComponent<StateManager>(gameGameObject);
            Shared.Editor.Inspectors.Utility.InspectorUtility.AddComponent<LayerManager>(gameGameObject);

            Debug.Log("The managers have been added.");
        }

        /// <summary>
        /// Adds the UI to the scene.
        /// </summary>
        private void AddUI()
        {
#if UNITY_2023_1_OR_NEWER
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
#else
            var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
#endif
            if (canvas == null) {
                EditorApplication.ExecuteMenuItem("GameObject/UI/Canvas");
#if UNITY_2023_1_OR_NEWER
                canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
#else
                canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
#endif
            }

            // Look up based on guid.
            GameObject uiPrefab = null;
            var monitorsPath = AssetDatabase.GUIDToAssetPath(c_MonitorsPrefabGUID);
            if (!string.IsNullOrEmpty(monitorsPath)) {
                uiPrefab = AssetDatabase.LoadAssetAtPath(monitorsPath, typeof(GameObject)) as GameObject;
            }

            // If the guid wasn't found try the path.
            if (uiPrefab == null) {
                var baseDirectory = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(m_MainManagerWindow))).Replace("\\", "/").Replace("Editor/Managers", "");
                uiPrefab = AssetDatabase.LoadAssetAtPath(baseDirectory + "Demo/Prefabs/UI/Monitors.prefab", typeof(GameObject)) as GameObject;
            }

            if (uiPrefab == null) {
                Debug.LogError("Error: Unable to find the UI Monitors prefab.");
                return;
            }

            // Instantiate the Monitors prefab.
            var uiGameObject = PrefabUtility.InstantiatePrefab(uiPrefab) as GameObject;
            uiGameObject.name = "Monitors";
            uiGameObject.GetComponent<RectTransform>().SetParent(canvas.transform, false);

            // The GlobalDictionary is used by the UI. Add it to the "Game" GameObject.
            GameObject gameGameObject;
#if UNITY_2023_1_OR_NEWER
            var scheduler = UnityEngine.Object.FindFirstObjectByType<SchedulerBase>();
#else
            var scheduler = UnityEngine.Object.FindObjectOfType<SchedulerBase>();
#endif
            if (scheduler == null) {
                gameGameObject = new GameObject("Game");
            } else {
                gameGameObject = scheduler.gameObject;
            }
            Shared.Editor.Inspectors.Utility.InspectorUtility.AddComponent<Shared.Utility.GlobalDictionary>(gameGameObject);
        }

        /// <summary>
        /// Shows the on screen controls fields.
        /// </summary>
        /// <param name="container">The VisualElement that contains the setup fields.</param>
        private void ShowOnScreenControlsSetup(VisualElement container)
        {
            // Draw the perspective.
            var selectedInput = new PopupField<string>("Input Type", new List<string>(Enum.GetNames(typeof(InputType))), (int)m_VirtualControlsInputType);
            selectedInput.RegisterValueChangedCallback(c =>
            {
                m_VirtualControlsInputType = (InputType)selectedInput.index;
            });
            container.Add(selectedInput);
        }

        /// <summary>
        /// Adds the UI to the scene.
        /// </summary>
        private void AddVirtualControls()
        {
#if UNITY_2023_1_OR_NEWER
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
#else
            var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
#endif
            if (canvas == null) {
                EditorApplication.ExecuteMenuItem("GameObject/UI/Canvas");
#if UNITY_2023_1_OR_NEWER
                canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
#else
                canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
#endif
            }

            // Look up based on guid.
            GameObject virtualControlsPrefab = null;
            var virtualControlsPath = AssetDatabase.GUIDToAssetPath(m_VirtualControlsInputType == InputType.InputSystem ? c_OnScreenControlsPrefabGUID : c_VirtualControlsPrefabGUID);
            if (!string.IsNullOrEmpty(virtualControlsPath)) {
                virtualControlsPrefab = AssetDatabase.LoadAssetAtPath(virtualControlsPath, typeof(GameObject)) as GameObject;
            }

            // If the guid wasn't found try the path.
            if (virtualControlsPrefab == null) {
                var baseDirectory = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(m_MainManagerWindow))).Replace("\\", "/").Replace("Editor/Managers", "");
                virtualControlsPrefab = AssetDatabase.LoadAssetAtPath(string.Format("{0}Demo/Prefabs/UI/{2}.prefab", baseDirectory, m_VirtualControlsInputType == InputType.InputSystem ? "InputSystemOnScreenControls" : "InputManagerVirtualControls"), typeof(GameObject)) as GameObject;
            }

            if (virtualControlsPrefab == null) {
                Debug.LogError("Error: Unable to find the UI Virtual Controls prefab. Ensure the sample assets have been imported.");
                return;
            }

            // Instantiate the Virtual Controls prefab.
            var virtualControls = PrefabUtility.InstantiatePrefab(virtualControlsPrefab) as GameObject;
            virtualControls.name = "VirtualControls";
            virtualControls.GetComponent<RectTransform>().SetParent(canvas.transform, false);

            EditorUtility.SetDirty(virtualControls);
        }

        /// <summary>
        /// Updates all of the layers to the Ultimate Character Controller defaults.
        /// </summary>
        public static void UpdateLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProperty = tagManager.FindProperty("layers");

            // Add the layers.
            AddLayer(layersProperty, LayerManager.Enemy, "Enemy");
            AddLayer(layersProperty, LayerManager.MovingPlatform, "MovingPlatform");
            AddLayer(layersProperty, LayerManager.VisualEffect, "VisualEffect");
            AddLayer(layersProperty, LayerManager.Overlay, "Overlay");
            AddLayer(layersProperty, LayerManager.SubCharacter, "SubCharacter");
            AddLayer(layersProperty, LayerManager.Character, "Character");

            tagManager.ApplyModifiedProperties();

            Debug.Log("The layers were successfully updated.");
        }

        /// <summary>
        /// Sets the layer index to the specified name if the string value is empty.
        /// </summary>
        public static void AddLayer(SerializedProperty layersProperty, int index, string name)
        {
            var layerElement = layersProperty.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layerElement.stringValue)) {
                layerElement.stringValue = name;
            }
        }

        /// <summary>
        /// Shows a popup for which render pipeline can be imported.
        /// </summary>
        /// <param name="container">The parent container.</param>
        private void ShowRenderPipelines(VisualElement container)
        {
            container.Clear();

            m_RenderPipelinePopup = new PopupField<string>("Render Pipeline", new List<string>() { "URP", "HDRP" }, 0);
            container.Add(m_RenderPipelinePopup);
        }

        /// <summary>
        /// Imports the render pipeline specified by the popup.
        /// </summary>
        private void ImportRenderPipeline()
        {
#if !ULTIMATE_CHARACTER_CONTROLLER_UNIVERSALRP
            if (m_RenderPipelinePopup.index == 0) {
                EditorUtility.DisplayDialog("URP Not Imported", "The Universal Render Pipeline package is not imported. Please import it before installing the URP integration.", "OK");
                return;
            }
#endif
#if !ULTIMATE_CHARACTER_CONTROLLER_HDRP
            if (m_RenderPipelinePopup.index == 1) {
                EditorUtility.DisplayDialog("HDRP Not Imported", "The High Definition Render Pipeline package is not imported. Please import it before installing the HDRP integration.", "OK");
                return;
            }
#endif
            ImportRenderPipeline(m_RenderPipelinePopup.index == 0);
        }

        /// <summary>
        /// Imports the specified render pipeline.
        /// </summary>
        /// <param name="urp">Should the URP package be imported? If false HDRP will be imported.</param>
        public static void ImportRenderPipeline(bool urp)
        {
            var packageGUID = urp ? c_URPPackageGUID : c_HDRPPackageGUID;
            if (string.IsNullOrEmpty(packageGUID)) {
                Debug.LogError("Error: Unable to find the render pipeline package. Please ensure the entire asset has been imported.");
                return;
            }
            AssetDatabase.ImportPackage(AssetDatabase.GUIDToAssetPath(packageGUID), false);
        }
    }
}