// File: AGISConditionTreeView.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/Panels/
// Purpose: Recursive visual editor for AGISConditionExprDef trees.
//          Uses UIToolkit Runtime only (no UnityEditor).

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.Runtime;

namespace AGIS.ESM.RuntimeEditor.Panels
{
    public sealed class AGISConditionTreeView : VisualElement
    {
        private readonly AGISConditionTypeRegistry  _condTypes;
        private readonly Action<AGISConditionExprDef> _onChanged;

        private AGISConditionExprDef _root;

        // ─────────────────────────────────────────────────────────────────────
        public AGISConditionTreeView(
            AGISConditionTypeRegistry condTypes,
            Action<AGISConditionExprDef> onChanged)
        {
            _condTypes = condTypes ?? throw new ArgumentNullException(nameof(condTypes));
            _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));

            style.flexDirection = FlexDirection.Column;

            // Root-level buttons to replace root expression
            var rootBtnRow = new VisualElement();
            rootBtnRow.style.flexDirection = FlexDirection.Row;
            rootBtnRow.style.marginBottom  = 4f;
            rootBtnRow.Add(MakeReplaceRootBtn("True",  () => ReplaceRoot(AGISConditionExprDef.True())));
            rootBtnRow.Add(MakeReplaceRootBtn("False", () => ReplaceRoot(AGISConditionExprDef.False())));
            rootBtnRow.Add(MakeReplaceRootBtn("AND",   () => ReplaceRoot(AGISConditionExprDef.And())));
            rootBtnRow.Add(MakeReplaceRootBtn("OR",    () => ReplaceRoot(AGISConditionExprDef.Or())));
            rootBtnRow.Add(MakeReplaceRootBtn("LEAF",  () => ReplaceRoot(MakeNewLeaf())));
            Add(rootBtnRow);

            // Tree display area
            var treeArea = new VisualElement();
            treeArea.name = "tree-area";
            treeArea.style.flexDirection = FlexDirection.Column;
            Add(treeArea);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetExpression(AGISConditionExprDef expr)
        {
            _root = expr;
            Rebuild();
        }

        // ── Rebuild ───────────────────────────────────────────────────────────

        private void Rebuild()
        {
            var area = this.Q<VisualElement>("tree-area");
            if (area == null) return;
            area.Clear();

            if (_root == null)
            {
                area.Add(new Label("(null condition — edge always evaluates false)") { style = { color = new Color(0.5f, 0.5f, 0.5f) } });
                return;
            }

            var node = BuildExprNode(_root, replaceSelf: newExpr =>
            {
                _root = newExpr;
                _onChanged(_root);
                Rebuild();
            });
            area.Add(node);
        }

        // ── Recursive builder ─────────────────────────────────────────────────

        private VisualElement BuildExprNode(
            AGISConditionExprDef expr,
            Action<AGISConditionExprDef> replaceSelf)
        {
            if (expr == null)
            {
                var nullEl = new Label("(null)");
                nullEl.style.color = new Color(0.7f, 0.3f, 0.3f);
                return nullEl;
            }

            switch (expr.kind)
            {
                case AGISConditionExprDef.ExprKind.ConstBool:
                    return BuildConstBoolNode(expr, replaceSelf);
                case AGISConditionExprDef.ExprKind.And:
                    return BuildMultiNode(expr, replaceSelf, "AND", "agis-cond-node--and");
                case AGISConditionExprDef.ExprKind.Or:
                    return BuildMultiNode(expr, replaceSelf, "OR", "agis-cond-node--or");
                case AGISConditionExprDef.ExprKind.Not:
                    return BuildNotNode(expr, replaceSelf);
                case AGISConditionExprDef.ExprKind.Leaf:
                    return BuildLeafNode(expr, replaceSelf);
                default:
                    return new Label($"(unknown kind: {expr.kind})");
            }
        }

        private VisualElement BuildConstBoolNode(
            AGISConditionExprDef expr,
            Action<AGISConditionExprDef> replaceSelf)
        {
            var container = new VisualElement();
            container.AddToClassList("agis-cond-node");
            container.AddToClassList(expr.constValue ? "agis-cond-node--true" : "agis-cond-node--false");
            container.style.flexDirection = FlexDirection.Row;
            container.style.marginBottom  = 2f;

            var pill = new Label(expr.constValue ? "\u2713 Always True" : "\u2717 Never True");
            pill.style.color = expr.constValue ? new Color(0.2f, 0.9f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
            pill.style.flexGrow = 1;
            container.Add(pill);

            // Change-to buttons
            if (expr.constValue)
            {
                container.Add(MakeSmallBtn("→ False", () => replaceSelf(AGISConditionExprDef.False())));
            }
            else
            {
                container.Add(MakeSmallBtn("→ True", () => replaceSelf(AGISConditionExprDef.True())));
            }
            container.Add(MakeSmallBtn("→ AND",  () => replaceSelf(AGISConditionExprDef.And())));
            container.Add(MakeSmallBtn("→ OR",   () => replaceSelf(AGISConditionExprDef.Or())));
            container.Add(MakeSmallBtn("→ LEAF", () => replaceSelf(MakeNewLeaf())));
            container.Add(MakeRemoveBtn(() => replaceSelf(null)));

            return container;
        }

        private VisualElement BuildMultiNode(
            AGISConditionExprDef expr,
            Action<AGISConditionExprDef> replaceSelf,
            string label,
            string cssClass)
        {
            var outer = new VisualElement();
            outer.AddToClassList("agis-cond-node");
            outer.AddToClassList(cssClass);
            outer.style.flexDirection = FlexDirection.Column;
            outer.style.marginBottom  = 4f;

            // Header row
            var header = new VisualElement();
            header.AddToClassList("agis-cond-node__header");
            header.style.flexDirection = FlexDirection.Row;

            var lbl = new Label(label);
            lbl.AddToClassList("agis-cond-node__label");
            lbl.style.flexGrow = 1;
            header.Add(lbl);

            header.Add(MakeSmallBtn("+ Child", () =>
            {
                expr.children ??= new List<AGISConditionExprDef>();
                expr.children.Add(MakeNewLeaf());
                _onChanged(_root);
                Rebuild();
            }));
            header.Add(MakeSmallBtn("Wrap NOT", () =>
            {
                replaceSelf(AGISConditionExprDef.Not(expr));
                _onChanged(_root);
                Rebuild();
            }));
            header.Add(MakeRemoveBtn(() =>
            {
                replaceSelf(null);
                _onChanged(_root);
                Rebuild();
            }));

            outer.Add(header);

            // Children
            var childrenEl = new VisualElement();
            childrenEl.AddToClassList("agis-cond-node__children");
            childrenEl.style.paddingLeft = 16f;
            childrenEl.style.flexDirection = FlexDirection.Column;

            if (expr.children != null)
            {
                for (int i = 0; i < expr.children.Count; i++)
                {
                    var capturedIdx = i;
                    var childExpr   = expr.children[i];

                    var childEl = BuildExprNode(childExpr, newChild =>
                    {
                        if (newChild == null)
                            expr.children.RemoveAt(capturedIdx);
                        else
                            expr.children[capturedIdx] = newChild;
                        _onChanged(_root);
                        Rebuild();
                    });
                    childrenEl.Add(childEl);
                }
            }

            outer.Add(childrenEl);
            return outer;
        }

        private VisualElement BuildNotNode(
            AGISConditionExprDef expr,
            Action<AGISConditionExprDef> replaceSelf)
        {
            var outer = new VisualElement();
            outer.AddToClassList("agis-cond-node");
            outer.AddToClassList("agis-cond-node--not");
            outer.style.flexDirection = FlexDirection.Column;
            outer.style.marginBottom  = 4f;

            var header = new VisualElement();
            header.AddToClassList("agis-cond-node__header");
            header.style.flexDirection = FlexDirection.Row;

            var lbl = new Label("NOT");
            lbl.AddToClassList("agis-cond-node__label");
            lbl.style.flexGrow = 1;
            header.Add(lbl);

            header.Add(MakeRemoveBtn(() =>
            {
                replaceSelf(null);
                _onChanged(_root);
                Rebuild();
            }));
            outer.Add(header);

            // Child
            var childArea = new VisualElement();
            childArea.style.paddingLeft = 16f;

            var childEl = BuildExprNode(expr.child, newChild =>
            {
                expr.child = newChild;
                _onChanged(_root);
                Rebuild();
            });
            childArea.Add(childEl);
            outer.Add(childArea);

            return outer;
        }

        private VisualElement BuildLeafNode(
            AGISConditionExprDef expr,
            Action<AGISConditionExprDef> replaceSelf)
        {
            var outer = new VisualElement();
            outer.AddToClassList("agis-cond-node");
            outer.AddToClassList("agis-cond-node--leaf");
            outer.style.flexDirection = FlexDirection.Column;
            outer.style.marginBottom  = 4f;

            // Header
            var header = new VisualElement();
            header.AddToClassList("agis-cond-node__header");
            header.style.flexDirection = FlexDirection.Row;

            var leaf = expr.leaf;
            string displayName = "(no type)";
            IAGISConditionType ct = null;
            if (leaf != null && !string.IsNullOrEmpty(leaf.conditionTypeId))
            {
                if (_condTypes.TryGet(leaf.conditionTypeId, out ct))
                    displayName = ct.DisplayName ?? leaf.conditionTypeId;
                else
                    displayName = leaf.conditionTypeId;
            }

            var typeLabel = new Label($"LEAF: {displayName}");
            typeLabel.AddToClassList("agis-cond-node__label");
            typeLabel.style.flexGrow = 1;
            header.Add(typeLabel);

            // Wrap buttons
            header.Add(MakeSmallBtn("Wrap NOT", () =>
            {
                replaceSelf(AGISConditionExprDef.Not(expr));
                _onChanged(_root);
                Rebuild();
            }));
            header.Add(MakeSmallBtn("Wrap AND", () =>
            {
                replaceSelf(AGISConditionExprDef.And(expr));
                _onChanged(_root);
                Rebuild();
            }));
            header.Add(MakeSmallBtn("Wrap OR", () =>
            {
                replaceSelf(AGISConditionExprDef.Or(expr));
                _onChanged(_root);
                Rebuild();
            }));
            header.Add(MakeRemoveBtn(() =>
            {
                replaceSelf(null);
                _onChanged(_root);
                Rebuild();
            }));

            outer.Add(header);

            // Change type: inline dropdown of all condition types
            var typeRow = new VisualElement();
            typeRow.style.flexDirection = FlexDirection.Row;
            typeRow.style.marginTop = 2f;

            var typeChoices = new List<string>();
            var typeIds     = new List<string>();
            int selectedIdx = 0;

            typeChoices.Add("(none)");
            typeIds.Add("");

            foreach (var ctype in _condTypes.AllTypes)
            {
                typeChoices.Add(ctype.DisplayName ?? ctype.TypeId);
                typeIds.Add(ctype.TypeId);
                if (leaf != null && ctype.TypeId == leaf.conditionTypeId)
                    selectedIdx = typeChoices.Count - 1;
            }

            var typeDropdown = new DropdownField("Type", typeChoices, selectedIdx);
            typeDropdown.style.flexGrow = 1;
            typeDropdown.RegisterValueChangedCallback(evt =>
            {
                int idx = typeDropdown.index;
                if (idx < 0 || idx >= typeIds.Count) return;
                string newTypeId = typeIds[idx];

                if (expr.leaf == null)
                    expr.leaf = new AGISConditionInstanceDef();
                expr.leaf.conditionTypeId = newTypeId;
                expr.leaf.@params = new AGISParamTable();
                if (!expr.leaf.conditionId.IsValid)
                    expr.leaf.conditionId = AGISGuid.New();

                _onChanged(_root);
                Rebuild();
            });
            typeRow.Add(typeDropdown);
            outer.Add(typeRow);

            // Param fields for the condition's schema
            if (ct?.Schema?.Specs != null && leaf != null)
            {
                var paramsArea = new VisualElement();
                paramsArea.style.paddingLeft = 8f;
                paramsArea.style.paddingTop  = 4f;

                foreach (var spec in ct.Schema.Specs)
                {
                    var capturedSpec = spec;
                    var capturedLeaf = leaf;

                    var field = AGISParamFieldDrawer.CreateField(
                        spec,
                        capturedLeaf.@params,
                        (key, val) =>
                        {
                            capturedLeaf.@params ??= new AGISParamTable();
                            capturedLeaf.@params.Set(key, val);
                            _onChanged(_root);
                        });

                    paramsArea.Add(field);
                }

                outer.Add(paramsArea);
            }

            return outer;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ReplaceRoot(AGISConditionExprDef newRoot)
        {
            _root = newRoot;
            _onChanged(_root);
            Rebuild();
        }

        private static AGISConditionExprDef MakeNewLeaf()
        {
            return AGISConditionExprDef.Leaf(new AGISConditionInstanceDef
            {
                conditionId     = AGISGuid.New(),
                conditionTypeId = "",
                @params         = new AGISParamTable(),
            });
        }

        private Button MakeReplaceRootBtn(string label, Action onClick)
        {
            var btn = new Button(onClick) { text = label };
            btn.AddToClassList("agis-toolbar__button");
            btn.style.marginRight = 2f;
            return btn;
        }

        private static Button MakeSmallBtn(string label, Action onClick)
        {
            var btn = new Button(onClick) { text = label };
            btn.AddToClassList("agis-cond-node__btn");
            btn.style.height    = 18f;
            btn.style.marginLeft = 2f;
            return btn;
        }

        private static Button MakeRemoveBtn(Action onClick)
        {
            var btn = new Button(onClick) { text = "\u2715" }; // ×
            btn.AddToClassList("agis-cond-node__remove");
            btn.style.height    = 18f;
            btn.style.marginLeft = 2f;
            btn.style.color = new Color(1f, 0.4f, 0.4f);
            return btn;
        }
    }
}
