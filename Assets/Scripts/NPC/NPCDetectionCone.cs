// File: NPCDetectionCone.cs
// Folder: Assets/Scripts/NPC/
// Purpose: MonoBehaviour that provides configurable detection (sphere or cone) against a LayerMask.
//          Implements IAGISPersistentNodeType so the runner auto-populates
//          npc.show_detection_cone in AGISActorState at startup.
//
// Inspector fields:
//   Shape            — Sphere (omnidirectional) or Cone (directional)
//   Detection Mask   — which layers to detect objects on
//   Range            — maximum detection distance in world units
//   Angle            — full cone aperture in degrees (Cone shape only; e.g. 60 = ±30° from forward)
//
// Cone check is fully 3D: angle is measured from the actor's 3D forward direction,
// so a target above or below the actor is treated the same as one beside it.
//
// Pursuit override:
//   NPCFollowTargetNodeType calls SetPursuitOverride/ClearPursuitOverride to temporarily
//   widen range and angle while the NPC is actively chasing. EffectiveRange / EffectiveAngle
//   reflect the current values (override takes precedence over inspector values).
//
// Gizmo:
//   When npc.show_detection_cone = true in AGISActorState:
//     Sphere shape → draws a wire sphere at EffectiveRange
//     Cone   shape → draws a 3D wire cone (4 boundary rays + rim circle) at EffectiveRange/EffectiveAngle
//
// Usage by conditions:
//   var det = actor.GetComponent<NPCDetectionCone>();
//   bool hit      = det.IsDetected(target.transform);
//   Collider[] all = det.DetectAll();

using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC.Params;

namespace AGIS.NPC
{
    public enum NPCDetectionShape { Sphere, Cone }

    public sealed class NPCDetectionCone : MonoBehaviour, IAGISPersistentNodeType
    {
        [Header("Detection")]
        [Tooltip("Sphere = omnidirectional range check. Cone = directional with angle limit.")]
        public NPCDetectionShape shape = NPCDetectionShape.Cone;

        [Tooltip("Only objects on these layers will be detected.")]
        public LayerMask detectionMask = ~0;

        [Tooltip("Maximum detection distance in world units.")]
        [Min(0.1f)] public float range = 10f;

        [Tooltip("Full aperture of the cone in degrees (Cone shape only). E.g. 60 = ±30° from forward. " +
                 "Measured in 3D — targets above or below are subject to the same angle limit.")]
        [Range(1f, 359f)] public float angle = 60f;

        // ── Pursuit override ──────────────────────────────────────────────────────────
        // Set by NPCFollowTargetNodeType on Enter and cleared on Exit.

        private float? _overrideRange;
        private float? _overrideAngle;

        /// <summary>Active range — override value while in pursuit, otherwise inspector value.</summary>
        public float EffectiveRange => _overrideRange ?? range;

        /// <summary>Active angle — override value while in pursuit, otherwise inspector value.</summary>
        public float EffectiveAngle => _overrideAngle ?? angle;

        /// <summary>
        /// Temporarily widens range and angle (called by NPCFollowTargetNodeType on Enter).
        /// </summary>
        public void SetPursuitOverride(float overrideRange, float overrideAngle)
        {
            _overrideRange = overrideRange;
            _overrideAngle = overrideAngle;
        }

        /// <summary>
        /// Restores inspector values (called by NPCFollowTargetNodeType on Exit).
        /// </summary>
        public void ClearPursuitOverride()
        {
            _overrideRange = null;
            _overrideAngle = null;
        }

        // ── IAGISPersistentNodeType ───────────────────────────────────────────────────

        public IReadOnlyList<AGISParamSpec> PersistentParams { get; } = new AGISParamSpec[]
        {
            new AGISParamSpec("npc.show_detection_cone", AGISParamType.Bool, AGISValue.FromBool(false))
                { displayName = "Show Detection Volume",
                  tooltip     = "When true, the detection volume is drawn as a gizmo in the Scene view." },
        };

        // ── Detection API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true when <paramref name="target"/> is within the detection volume.
        /// Sphere: range check only. Cone: 3D range + angle from forward direction.
        /// Uses EffectiveRange / EffectiveAngle (respects pursuit override).
        /// Does not check LayerMask — use DetectAll for physics-filtered sweeps.
        /// </summary>
        public bool IsDetected(Transform target)
        {
            if (target == null) return false;

            Vector3 toTarget = target.position - transform.position;
            float effectiveRange = EffectiveRange;

            if (toTarget.sqrMagnitude > effectiveRange * effectiveRange) return false;

            if (shape == NPCDetectionShape.Sphere) return true;

            // 3D cone: angle between forward and direction-to-target (no axis flattening).
            float dot = Vector3.Dot(transform.forward, toTarget.normalized);
            return dot >= Mathf.Cos(EffectiveAngle * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>
        /// Physics sweep with detectionMask, then filters by the configured shape.
        /// Returns all Colliders inside the detection volume (self excluded).
        /// Uses EffectiveRange (respects pursuit override).
        /// </summary>
        public Collider[] DetectAll()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, EffectiveRange, detectionMask);
            if (hits.Length == 0) return hits;

            int writeIdx = 0;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] != null && hits[i].transform != transform && IsDetected(hits[i].transform))
                    hits[writeIdx++] = hits[i];
            }

            if (writeIdx == hits.Length) return hits;

            var result = new Collider[writeIdx];
            System.Array.Copy(hits, result, writeIdx);
            return result;
        }

        /// <summary>
        /// Returns the closest Collider inside the detection volume (null if none).
        /// </summary>
        public Collider DetectClosest()
        {
            Collider[] hits = DetectAll();
            if (hits.Length == 0) return null;

            Collider closest = null;
            float minSqr = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                float sqr = (hits[i].transform.position - transform.position).sqrMagnitude;
                if (sqr < minSqr) { minSqr = sqr; closest = hits[i]; }
            }
            return closest;
        }

        // ── Gizmo ─────────────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            var actorState = GetComponent<AGISActorState>();
            if (actorState == null || !actorState.GetBool("npc.show_detection_cone"))
                return;

            bool hasPursuitOverride = _overrideRange.HasValue || _overrideAngle.HasValue;

            if (shape == NPCDetectionShape.Sphere)
            {
                DrawSphereGizmo(hasPursuitOverride);
            }
            else
            {
                // Draw base cone first, then pursuit cone on top if active.
                DrawConeGizmo(range, angle, new Color(1f, 0.85f, 0f, 0.8f));

                if (hasPursuitOverride)
                    DrawConeGizmo(EffectiveRange, EffectiveAngle, new Color(1f, 0.35f, 0.1f, 0.9f));
            }
        }

        private void DrawSphereGizmo(bool hasPursuitOverride)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, range);

            if (hasPursuitOverride)
            {
                Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.9f);
                Gizmos.DrawWireSphere(transform.position, EffectiveRange);
            }
        }

        private void DrawConeGizmo(float drawRange, float drawAngle, Color color)
        {
            Gizmos.color = color;

            float halfAngle    = drawAngle * 0.5f * Mathf.Deg2Rad;
            Vector3 origin     = transform.position;
            Vector3 fwd        = transform.forward;
            Vector3 up         = transform.up;
            Vector3 right      = transform.right;

            // Rim circle: center is forward * range * cos(halfAngle),
            // radius = range * sin(halfAngle).
            float rimDepth  = drawRange * Mathf.Cos(halfAngle);
            float rimRadius = drawRange * Mathf.Sin(halfAngle);
            Vector3 rimCenter = origin + fwd * rimDepth;

            // 4 boundary rays: ±up, ±right rotated by halfAngle from fwd.
            Vector3 topDir    = (fwd * Mathf.Cos(halfAngle) + up    * Mathf.Sin(halfAngle)).normalized;
            Vector3 bottomDir = (fwd * Mathf.Cos(halfAngle) - up    * Mathf.Sin(halfAngle)).normalized;
            Vector3 leftDir   = (fwd * Mathf.Cos(halfAngle) - right * Mathf.Sin(halfAngle)).normalized;
            Vector3 rightDir  = (fwd * Mathf.Cos(halfAngle) + right * Mathf.Sin(halfAngle)).normalized;

            Gizmos.DrawLine(origin, origin + topDir    * drawRange);
            Gizmos.DrawLine(origin, origin + bottomDir * drawRange);
            Gizmos.DrawLine(origin, origin + leftDir   * drawRange);
            Gizmos.DrawLine(origin, origin + rightDir  * drawRange);

            // Rim circle (32 segments).
            const int Segments = 32;
            for (int i = 0; i < Segments; i++)
            {
                float a1 = (float) i      / Segments * Mathf.PI * 2f;
                float a2 = (float)(i + 1) / Segments * Mathf.PI * 2f;
                Vector3 p1 = rimCenter + (right * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * rimRadius;
                Vector3 p2 = rimCenter + (right * Mathf.Cos(a2) + up * Mathf.Sin(a2)) * rimRadius;
                Gizmos.DrawLine(p1, p2);
            }
        }
    }
}
