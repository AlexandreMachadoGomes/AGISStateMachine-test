// File: AGISConditionDefs.cs
// Folder: Assets/Scripts/AX State Machine/Definitions/

using System;
using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.UGC
{
    [Serializable]
    public class AGISConditionInstanceDef
    {
        /// <summary>
        /// Stable id for this specific condition leaf instance.
        /// Required so external bindings can target a specific leaf inside an edge expression tree.
        /// </summary>
        [SerializeField] public AGISGuid conditionId = AGISGuid.New();

        [SerializeField] public string conditionTypeId;
        [SerializeField] public AGISParamTable @params = new AGISParamTable();
    }

    /// <summary>
    /// Serializable boolean expression tree.
    /// Node types: And/Or/Not/Leaf/ConstBool.
    /// </summary>
    [Serializable]
    public class AGISConditionExprDef
    {
        public enum ExprKind
        {
            And = 0,
            Or = 1,
            Not = 2,
            Leaf = 3,

            /// <summary>
            /// Constant boolean.
            /// NOTE: An edge with a null condition should be treated as FALSE by runtime.
            /// Use ConstBool(true) for unconditional transitions.
            /// </summary>
            ConstBool = 4,
        }

        [SerializeField] public ExprKind kind;

        // ConstBool
        [SerializeField] public bool constValue;

        // And/Or
        [SerializeReference] public List<AGISConditionExprDef> children;

        // Not
        [SerializeReference] public AGISConditionExprDef child;

        // Leaf
        [SerializeField] public AGISConditionInstanceDef leaf;

        public static AGISConditionExprDef Const(bool value)
        {
            return new AGISConditionExprDef { kind = ExprKind.ConstBool, constValue = value };
        }

        public static AGISConditionExprDef True() => Const(true);
        public static AGISConditionExprDef False() => Const(false);

        public static AGISConditionExprDef Leaf(AGISConditionInstanceDef leaf)
        {
            if (leaf != null && !leaf.conditionId.IsValid)
                leaf.conditionId = AGISGuid.New();
            return new AGISConditionExprDef { kind = ExprKind.Leaf, leaf = leaf };
        }

        public static AGISConditionExprDef Not(AGISConditionExprDef child)
        {
            return new AGISConditionExprDef { kind = ExprKind.Not, child = child };
        }

        public static AGISConditionExprDef And(params AGISConditionExprDef[] exprs)
        {
            return new AGISConditionExprDef { kind = ExprKind.And, children = new List<AGISConditionExprDef>(exprs ?? Array.Empty<AGISConditionExprDef>()) };
        }

        public static AGISConditionExprDef Or(params AGISConditionExprDef[] exprs)
        {
            return new AGISConditionExprDef { kind = ExprKind.Or, children = new List<AGISConditionExprDef>(exprs ?? Array.Empty<AGISConditionExprDef>()) };
        }

        /// <summary>
        /// Ensures all leaf conditions in this expression have stable ids.
        /// Safe to call at edit-time, compile-time, or load-time.
        /// </summary>
        public void EnsureLeafIds()
        {
            switch (kind)
            {
                case ExprKind.Leaf:
                    if (leaf != null && !leaf.conditionId.IsValid)
                        leaf.conditionId = AGISGuid.New();
                    break;

                case ExprKind.Not:
                    child?.EnsureLeafIds();
                    break;

                case ExprKind.And:
                case ExprKind.Or:
                    if (children == null) return;
                    for (int i = 0; i < children.Count; i++)
                        children[i]?.EnsureLeafIds();
                    break;

                case ExprKind.ConstBool:
                default:
                    break;
            }
        }
    }
}
