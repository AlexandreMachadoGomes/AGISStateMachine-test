// File: AGISConditionSummary.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/
// Purpose: Pure static utility that turns a condition expression tree into a
//          short human-readable string for display in edge pills on the canvas.

using System.Text;
using AGIS.ESM.UGC;
using AGIS.ESM.Runtime;

namespace AGIS.ESM.RuntimeEditor
{
    public static class AGISConditionSummary
    {
        private const int MaxLength = 40;

        /// <summary>
        /// Returns a short, human-readable summary of the condition expression.
        /// Truncates the result to 40 characters.
        /// </summary>
        public static string Summarize(AGISConditionExprDef expr, AGISConditionTypeRegistry registry)
        {
            if (expr == null)
                return "(null)";

            var sb = new StringBuilder(64);
            AppendExpr(sb, expr, registry, depth: 0);

            var result = sb.ToString();
            if (result.Length > MaxLength)
                result = result.Substring(0, MaxLength - 1) + "\u2026"; // ellipsis

            return result;
        }

        private static void AppendExpr(StringBuilder sb, AGISConditionExprDef expr,
            AGISConditionTypeRegistry registry, int depth)
        {
            if (expr == null)
            {
                sb.Append("(null)");
                return;
            }

            switch (expr.kind)
            {
                case AGISConditionExprDef.ExprKind.ConstBool:
                    sb.Append(expr.constValue ? "TRUE" : "FALSE");
                    return;

                case AGISConditionExprDef.ExprKind.Leaf:
                    AppendLeaf(sb, expr.leaf, registry);
                    return;

                case AGISConditionExprDef.ExprKind.Not:
                    sb.Append("NOT ");
                    if (expr.child != null)
                        AppendExpr(sb, expr.child, registry, depth + 1);
                    else
                        sb.Append("(null)");
                    return;

                case AGISConditionExprDef.ExprKind.And:
                    AppendMulti(sb, expr, registry, depth, "AND");
                    return;

                case AGISConditionExprDef.ExprKind.Or:
                    AppendMulti(sb, expr, registry, depth, "OR");
                    return;

                default:
                    sb.Append("(unknown)");
                    return;
            }
        }

        private static void AppendLeaf(StringBuilder sb, AGISConditionInstanceDef leaf,
            AGISConditionTypeRegistry registry)
        {
            if (leaf == null)
            {
                sb.Append("(empty leaf)");
                return;
            }

            if (registry != null && registry.TryGet(leaf.conditionTypeId, out var ct))
            {
                sb.Append(ct.DisplayName ?? leaf.conditionTypeId ?? "?");
            }
            else
            {
                sb.Append(string.IsNullOrEmpty(leaf.conditionTypeId)
                    ? "(no type)"
                    : leaf.conditionTypeId);
            }
        }

        private static void AppendMulti(StringBuilder sb, AGISConditionExprDef expr,
            AGISConditionTypeRegistry registry, int depth, string op)
        {
            if (expr.children == null || expr.children.Count == 0)
            {
                sb.Append($"({op} ∅)");
                return;
            }

            if (expr.children.Count == 1)
            {
                AppendExpr(sb, expr.children[0], registry, depth + 1);
                return;
            }

            // For deep nesting, just show the operator and count
            if (depth >= 2)
            {
                sb.Append($"({op}×{expr.children.Count})");
                return;
            }

            bool first = true;
            for (int i = 0; i < expr.children.Count; i++)
            {
                // Bail out early if we're already long enough
                if (sb.Length >= MaxLength)
                    return;

                if (!first)
                {
                    sb.Append($" {op} ");
                }
                first = false;

                AppendExpr(sb, expr.children[i], registry, depth + 1);
            }
        }
    }
}
