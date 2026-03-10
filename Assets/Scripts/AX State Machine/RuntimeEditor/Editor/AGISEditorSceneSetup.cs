// File: AGISEditorSceneSetup.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/Editor/
// Purpose: One-click scene setup for testing the AGIS Runtime Graph Editor.
//          Menu: AGIS / Setup Runtime Editor Test Scene
//
// What it creates:
//   • Panel Settings asset (if missing)          → Assets/AGIS_PanelSettings.asset
//   • "AGIS Editor" GameObject                   → UIDocument + AGISGraphEditorWindow
//   • Wires the USS stylesheet automatically
//   • Finds an existing AGISStateMachineRunner in the scene and connects it
//   • Warns clearly if anything is missing

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using AGIS.ESM.Runtime;
using AGIS.ESM.RuntimeEditor;

namespace AGIS.ESM.Editor
{
    public static class AGISEditorSceneSetup
    {
        private const string MenuPath        = "AGIS/Setup Runtime Editor Test Scene";
        private const string GoName          = "AGIS Editor";
        private const string PanelSettingsPath = "Assets/AGIS_PanelSettings.asset";
        private const string UssSearchFilter = "AGISEditor t:StyleSheet";

        [MenuItem(MenuPath)]
        public static void SetupScene()
        {
            // ── 1. Panel Settings ─────────────────────────────────────────────
            var panelSettings = GetOrCreatePanelSettings();

            // ── 2. USS stylesheet ─────────────────────────────────────────────
            var uss = FindUss();
            if (uss == null)
                Debug.LogWarning("[AGIS Setup] Could not find AGISEditor.uss. " +
                    "Assign it manually to the UIDocument's Panel Settings or the " +
                    "AGISGraphEditorWindow's editorStyleSheet field.");

            // ── 3. Find or create the editor GameObject ───────────────────────
            var go = FindOrCreateEditorGo();

            // ── 4. UIDocument ─────────────────────────────────────────────────
            var uiDoc = go.GetComponent<UIDocument>();
            if (uiDoc == null)
                uiDoc = go.AddComponent<UIDocument>();

            uiDoc.panelSettings = panelSettings;

            // ── 5. AGISGraphEditorWindow ──────────────────────────────────────
            var editorWindow = go.GetComponent<AGISGraphEditorWindow>();
            if (editorWindow == null)
                editorWindow = go.AddComponent<AGISGraphEditorWindow>();

            // Assign USS via the serialized field
            if (uss != null)
            {
                var so = new SerializedObject(editorWindow);
                var ssField = so.FindProperty("editorStyleSheet");
                if (ssField != null)
                {
                    ssField.objectReferenceValue = uss;
                    so.ApplyModifiedProperties();
                }
            }

            // ── 6. Wire runner ────────────────────────────────────────────────
            var runner = Object.FindFirstObjectByType<AGISStateMachineRunner>();
            if (runner != null)
            {
                var so = new SerializedObject(editorWindow);
                var runnerField = so.FindProperty("targetRunner");
                if (runnerField != null)
                {
                    runnerField.objectReferenceValue = runner;
                    so.ApplyModifiedProperties();
                }
                Debug.Log($"[AGIS Setup] Connected runner on '{runner.gameObject.name}'.");
            }
            else
            {
                Debug.LogWarning("[AGIS Setup] No AGISStateMachineRunner found in the scene. " +
                    "Drag one into the 'Target Runner' field on AGISGraphEditorWindow before pressing Play.");
            }

            // ── 7. Mark scene dirty & select the new object ───────────────────
            Selection.activeGameObject = go;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[AGIS Setup] Done. Press Play then F12 to open the graph editor.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static GameObject FindOrCreateEditorGo()
        {
            // Reuse existing object if already in scene
            var existing = GameObject.Find(GoName);
            if (existing != null)
            {
                Debug.Log($"[AGIS Setup] Reusing existing '{GoName}' GameObject.");
                return existing;
            }

            var go = new GameObject(GoName);
            Undo.RegisterCreatedObjectUndo(go, "Create AGIS Editor");
            return go;
        }

        private static PanelSettings GetOrCreatePanelSettings()
        {
            // Try to load existing asset
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null)
                return existing;

            // Create a new one with sensible defaults
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ConstantPixelSize;

            // Set a dark theme if the built-in one is available
            var darkTheme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Packages/com.unity.ui/PackageResources/Styles/Themes/UnityDefaultRuntimeTheme.tss");
            if (darkTheme != null)
                ps.themeStyleSheet = darkTheme;

            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AGIS Setup] Created Panel Settings at {PanelSettingsPath}.");
            return ps;
        }

        private static StyleSheet FindUss()
        {
            var guids = AssetDatabase.FindAssets(UssSearchFilter);
            if (guids.Length == 0) return null;

            // Prefer the one in the RuntimeEditor/USS folder
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("RuntimeEditor"))
                    return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }

            // Fallback: first result
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
