// File: AGISConditionSearchWindow.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/Search/
// Purpose: Floating popup for picking a condition type. UIToolkit Runtime only.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using AGIS.ESM.Runtime;

namespace AGIS.ESM.RuntimeEditor.Search
{
    public sealed class AGISConditionSearchWindow : VisualElement
    {
        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly AGISConditionTypeRegistry    _condTypes;
        private readonly Action<IAGISConditionType>   _onSelect;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly List<RowEntry> _allEntries = new List<RowEntry>();
        private readonly List<RowEntry> _filtered   = new List<RowEntry>();
        private int _selectedIndex = -1;

        // ── UI refs ───────────────────────────────────────────────────────────
        private readonly TextField     _searchField;
        private readonly ScrollView    _scrollView;
        private readonly VisualElement _listContainer;

        private struct RowEntry
        {
            public IAGISConditionType Type;
            public string             Group;
            public VisualElement      Element;
        }

        // ─────────────────────────────────────────────────────────────────────
        public AGISConditionSearchWindow(
            AGISConditionTypeRegistry condTypes,
            Action<IAGISConditionType> onSelect,
            Vector2 position)
        {
            _condTypes = condTypes ?? throw new ArgumentNullException(nameof(condTypes));
            _onSelect  = onSelect  ?? throw new ArgumentNullException(nameof(onSelect));

            AddToClassList("agis-search-window");
            style.position   = Position.Absolute;
            style.left       = position.x;
            style.top        = position.y;
            style.width      = 340f;
            style.maxHeight  = 420f;
            style.flexDirection  = FlexDirection.Column;
            style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f, 0.97f));
            style.borderTopLeftRadius     = 4f;
            style.borderTopRightRadius    = 4f;
            style.borderBottomLeftRadius  = 4f;
            style.borderBottomRightRadius = 4f;

            // Search field
            _searchField = new TextField();
            _searchField.AddToClassList("agis-search-window__field");
            _searchField.style.marginTop   = 4f;
            _searchField.style.marginLeft  = 4f;
            _searchField.style.marginRight = 4f;
            _searchField.RegisterValueChangedCallback(evt => ApplyFilter(evt.newValue));
            Add(_searchField);

            // Scroll + list
            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.style.flexGrow = 1;
            Add(_scrollView);

            _listContainer = new VisualElement();
            _listContainer.style.flexDirection = FlexDirection.Column;
            _scrollView.Add(_listContainer);

            // Keyboard navigation
            _searchField.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            // Close on focus-out
            schedule.Execute(() =>
            {
                RegisterCallback<FocusOutEvent>(evt =>
                {
                    var focused = focusController?.focusedElement;
                    if (focused == null || !this.Contains(focused as VisualElement))
                        Close();
                });
            }).StartingIn(50);

            BuildEntries();
            ApplyFilter("");

            schedule.Execute(() => _searchField.Focus()).StartingIn(1);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Close()
        {
            parent?.Remove(this);
        }

        // ── Build entries ─────────────────────────────────────────────────────

        private void BuildEntries()
        {
            _allEntries.Clear();

            foreach (var type in _condTypes.AllTypes)
            {
                if (type == null) continue;
                _allEntries.Add(new RowEntry
                {
                    Type  = type,
                    Group = GetGroup(type.TypeId),
                });
            }

            _allEntries.Sort((a, b) =>
            {
                int cg = string.Compare(a.Group, b.Group, StringComparison.OrdinalIgnoreCase);
                if (cg != 0) return cg;
                return string.Compare(
                    a.Type.TypeId ?? "",
                    b.Type.TypeId ?? "",
                    StringComparison.OrdinalIgnoreCase);
            });
        }

        private void ApplyFilter(string filter)
        {
            _listContainer.Clear();
            _filtered.Clear();
            _selectedIndex = -1;

            string lastGroup = null;
            bool hasFilter   = !string.IsNullOrEmpty(filter);

            foreach (var entry in _allEntries)
            {
                if (hasFilter)
                {
                    bool idMatch = (entry.Type.TypeId ?? "")
                        .IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool nameMatch = (entry.Type.DisplayName ?? "")
                        .IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!idMatch && !nameMatch) continue;
                }

                // Group header
                if (entry.Group != lastGroup)
                {
                    lastGroup = entry.Group;
                    var groupHdr = new Label(entry.Group.ToUpperInvariant());
                    groupHdr.AddToClassList("agis-search-group-header");
                    groupHdr.style.color      = new Color(0.5f, 0.6f, 0.7f);
                    groupHdr.style.marginTop  = 4f;
                    groupHdr.style.marginLeft = 6f;
                    groupHdr.style.marginBottom = 2f;
                    groupHdr.style.fontSize   = 10f;
                    _listContainer.Add(groupHdr);
                }

                var capturedEntry = entry;
                var capturedIndex = _filtered.Count;

                var row = new VisualElement();
                row.AddToClassList("agis-search-row");
                row.style.flexDirection = FlexDirection.Row;
                row.style.paddingLeft   = 8f;
                row.style.paddingRight  = 8f;
                row.style.paddingTop    = 3f;
                row.style.paddingBottom = 3f;

                // TypeId is primary label
                var idEl = new Label(entry.Type.TypeId ?? "—");
                idEl.AddToClassList("agis-search-row__typeid");
                idEl.style.flexGrow   = 1;
                idEl.style.color      = new Color(0.8f, 0.9f, 1f);
                row.Add(idEl);

                // DisplayName is secondary
                var nameEl = new Label(entry.Type.DisplayName ?? "");
                nameEl.AddToClassList("agis-search-row__name");
                nameEl.style.color   = new Color(0.55f, 0.55f, 0.55f);
                nameEl.style.fontSize = 10f;
                row.Add(nameEl);

                row.RegisterCallback<PointerDownEvent>(evt =>
                {
                    SelectEntry(capturedIndex);
                    ConfirmSelection();
                    evt.StopPropagation();
                });

                row.RegisterCallback<PointerEnterEvent>(evt => SetHighlight(capturedIndex));

                var mutableEntry = capturedEntry;
                mutableEntry.Element = row;

                _filtered.Add(mutableEntry);
                _listContainer.Add(row);
            }

            if (_filtered.Count > 0)
                SelectEntry(0);
        }

        // ── Navigation ────────────────────────────────────────────────────────

        private void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.DownArrow:
                    SelectEntry(Mathf.Clamp(_selectedIndex + 1, 0, _filtered.Count - 1));
                    evt.StopPropagation();
                    break;
                case KeyCode.UpArrow:
                    SelectEntry(Mathf.Clamp(_selectedIndex - 1, 0, _filtered.Count - 1));
                    evt.StopPropagation();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ConfirmSelection();
                    evt.StopPropagation();
                    break;
                case KeyCode.Escape:
                    Close();
                    evt.StopPropagation();
                    break;
            }
        }

        private void SelectEntry(int index)
        {
            if (_selectedIndex >= 0 && _selectedIndex < _filtered.Count)
                _filtered[_selectedIndex].Element?.RemoveFromClassList("agis-search-row--selected");

            _selectedIndex = index;

            if (_selectedIndex >= 0 && _selectedIndex < _filtered.Count)
                _filtered[_selectedIndex].Element?.AddToClassList("agis-search-row--selected");
        }

        private void SetHighlight(int index)
        {
            SelectEntry(index);
        }

        private void ConfirmSelection()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _filtered.Count) return;
            var type = _filtered[_selectedIndex].Type;
            _onSelect?.Invoke(type);
            Close();
        }

        // ── Static helpers ────────────────────────────────────────────────────

        private static string GetGroup(string typeId)
        {
            if (string.IsNullOrEmpty(typeId)) return "Ungrouped";
            int dot = typeId.IndexOf('.');
            if (dot <= 0) return "Ungrouped";
            return typeId.Substring(0, dot);
        }
    }
}
