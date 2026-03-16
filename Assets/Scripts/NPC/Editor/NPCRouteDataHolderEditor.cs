// File: NPCRouteDataHolderEditor.cs
// Folder: Assets/Scripts/NPC/Editor/
// Purpose: Custom inspector + scene handles for NPCRouteDataHolder.
//
// Scene view:
//   • Each route is drawn in a distinct colour
//   • Each waypoint is a draggable position handle
//   • Lines connect waypoints in order
//   • Route name label appears at the first waypoint
//   • The currently active waypoint (from AGISActorState at runtime) is
//     highlighted with a larger wire sphere
//
// Inspector:
//   • Per-route foldouts with Add / Remove waypoint buttons
//   • Direct Vector3 fields for each waypoint
//   • All edits are undo-recorded on the NPCRouteData ScriptableObject

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using AGIS.NPC.Routes;

namespace AGIS.NPC.Editor
{
    [CustomEditor(typeof(NPCRouteDataHolder))]
    public sealed class NPCRouteDataHolderEditor : UnityEditor.Editor
    {
        private static readonly Color[] RouteColors = new Color[]
        {
            new Color(0.0f, 0.8f, 1.0f),  // cyan
            new Color(1.0f, 0.85f, 0.0f), // yellow
            new Color(0.2f, 1.0f, 0.3f),  // green
            new Color(1.0f, 0.3f, 0.9f),  // magenta
            new Color(1.0f, 0.5f, 0.0f),  // orange
            new Color(0.6f, 0.6f, 1.0f),  // lavender
            new Color(1.0f, 0.4f, 0.4f),  // pink
        };

        // Foldout state per route (not serialized — resets on domain reload)
        private bool[] _routeFoldouts = System.Array.Empty<bool>();

        // ─────────────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            var holder = (NPCRouteDataHolder)target;
            var data   = holder.routeData;

            // Draw the default fields (routeData assignment etc.)
            DrawDefaultInspector();

            if (data == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an NPCRouteData asset to edit waypoints here.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Waypoint Editor", EditorStyles.boldLabel);

            // Ensure foldout array matches route count
            if (_routeFoldouts.Length != data.routes.Count)
            {
                System.Array.Resize(ref _routeFoldouts, data.routes.Count);
                for (int i = 0; i < _routeFoldouts.Length; i++)
                    _routeFoldouts[i] = true;
            }

            for (int r = 0; r < data.routes.Count; r++)
            {
                var route = data.routes[r];
                if (route == null) continue;

                Color col = RouteColors[r % RouteColors.Length];

                // Route header foldout
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = col * 0.6f + Color.black * 0.4f;

                string label = $"Route [{r}]: {(string.IsNullOrEmpty(route.name) ? "(unnamed)" : route.name)}  ({route.WaypointCount} wp)";
                _routeFoldouts[r] = EditorGUILayout.BeginFoldoutHeaderGroup(_routeFoldouts[r], label);
                GUI.backgroundColor = prevBg;

                if (_routeFoldouts[r])
                {
                    EditorGUI.indentLevel++;

                    // Route name field
                    EditorGUI.BeginChangeCheck();
                    string newName = EditorGUILayout.TextField("Name", route.name);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(data, "Rename Route");
                        route.name = newName;
                        EditorUtility.SetDirty(data);
                    }

                    // Waypoints
                    if (route.waypoints != null)
                    {
                        for (int w = 0; w < route.waypoints.Count; w++)
                        {
                            EditorGUILayout.BeginHorizontal();

                            EditorGUI.BeginChangeCheck();
                            var newPos = EditorGUILayout.Vector3Field($"  WP {w}", route.waypoints[w]);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(data, "Move Waypoint");
                                route.waypoints[w] = newPos;
                                EditorUtility.SetDirty(data);
                            }

                            // Remove waypoint button
                            if (GUILayout.Button("−", GUILayout.Width(22)))
                            {
                                Undo.RecordObject(data, "Remove Waypoint");
                                route.waypoints.RemoveAt(w);
                                EditorUtility.SetDirty(data);
                                break;
                            }

                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    // Add waypoint button
                    if (GUILayout.Button("+ Add Waypoint"))
                    {
                        Undo.RecordObject(data, "Add Waypoint");
                        Vector3 lastPos = route.waypoints?.Count > 0
                            ? route.waypoints[route.waypoints.Count - 1] + Vector3.forward * 3f
                            : holder.transform.position + Vector3.forward * 3f;
                        route.waypoints ??= new System.Collections.Generic.List<Vector3>();
                        route.waypoints.Add(lastPos);
                        EditorUtility.SetDirty(data);
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            EditorGUILayout.Space(4);

            // Add / Remove route buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Route"))
            {
                Undo.RecordObject(data, "Add Route");
                data.routes.Add(new NPCRoute { name = $"Route {data.routes.Count}" });
                System.Array.Resize(ref _routeFoldouts, data.routes.Count);
                _routeFoldouts[data.routes.Count - 1] = true;
                EditorUtility.SetDirty(data);
            }
            GUI.enabled = data.routes.Count > 0;
            if (GUILayout.Button("− Remove Last Route"))
            {
                Undo.RecordObject(data, "Remove Route");
                data.routes.RemoveAt(data.routes.Count - 1);
                System.Array.Resize(ref _routeFoldouts, data.routes.Count);
                EditorUtility.SetDirty(data);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Sequence editor
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Sequence", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Order in which routes are traversed. Indices reference the Routes list above.",
                MessageType.None);

            if (data.sequence != null)
            {
                for (int s = 0; s < data.sequence.Count; s++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    int newIdx = EditorGUILayout.IntField($"  Step {s}", data.sequence[s]);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(data, "Edit Sequence");
                        data.sequence[s] = Mathf.Clamp(newIdx, 0, Mathf.Max(0, data.routes.Count - 1));
                        EditorUtility.SetDirty(data);
                    }
                    if (GUILayout.Button("−", GUILayout.Width(22)))
                    {
                        Undo.RecordObject(data, "Remove Sequence Step");
                        data.sequence.RemoveAt(s);
                        EditorUtility.SetDirty(data);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (GUILayout.Button("+ Add Sequence Step"))
            {
                Undo.RecordObject(data, "Add Sequence Step");
                data.sequence ??= new System.Collections.Generic.List<int>();
                data.sequence.Add(0);
                EditorUtility.SetDirty(data);
            }

            // Validation
            if (!data.Validate(out string err))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox($"Route data invalid: {err}", MessageType.Warning);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Scene handles
        // ─────────────────────────────────────────────────────────────────────

        private void OnSceneGUI()
        {
            var holder = (NPCRouteDataHolder)target;
            var data   = holder.routeData;
            if (data?.routes == null) return;

            // Runtime active-waypoint highlight
            var actorState = holder.GetComponent<AGIS.ESM.Runtime.AGISActorState>();
            int activeSeq  = actorState?.GetInt("npc.route.sequence_index") ?? -1;
            int activeWp   = actorState?.GetInt("npc.route.waypoint_index") ?? -1;

            for (int r = 0; r < data.routes.Count; r++)
            {
                var route = data.routes[r];
                if (route?.waypoints == null || route.WaypointCount == 0) continue;

                Color col = RouteColors[r % RouteColors.Length];
                Handles.color = col;

                for (int w = 0; w < route.waypoints.Count; w++)
                {
                    Vector3 pos = route.waypoints[w];

                    // Draggable position handle
                    EditorGUI.BeginChangeCheck();
                    Vector3 newPos = Handles.PositionHandle(pos, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(data, "Move Waypoint");
                        route.waypoints[w] = newPos;
                        EditorUtility.SetDirty(data);
                    }

                    // Waypoint sphere cap
                    float size = HandleUtility.GetHandleSize(pos) * 0.12f;
                    Handles.SphereHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);

                    // Line to next waypoint
                    if (w + 1 < route.waypoints.Count)
                        Handles.DrawLine(pos, route.waypoints[w + 1], 2f);

                    // Label at first waypoint
                    if (w == 0)
                        Handles.Label(pos + Vector3.up * 0.5f, $"[{r}] {route.name}");

                    // Highlight active waypoint
                    bool isActiveRoute = data.sequence != null &&
                                        activeSeq >= 0 && activeSeq < data.sequence.Count &&
                                        data.sequence[activeSeq] == r;

                    if (isActiveRoute && w == activeWp)
                    {
                        var prev = Handles.color;
                        Handles.color = Color.red;
                        Handles.DrawWireDisc(pos, Vector3.up, 0.6f);
                        Handles.color = prev;
                    }
                }

                // Close-loop line back to first waypoint (sequence wraps)
                if (route.waypoints.Count > 1)
                {
                    Handles.color = col * 0.5f;
                    Handles.DrawDottedLine(
                        route.waypoints[route.waypoints.Count - 1],
                        route.waypoints[0],
                        4f);
                }
            }

            // Keep scene view refreshing at runtime so the active-waypoint highlight updates
            if (Application.isPlaying)
                HandleUtility.Repaint();
        }
    }
}

#endif
