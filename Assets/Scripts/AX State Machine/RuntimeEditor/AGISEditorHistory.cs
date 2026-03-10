// File: AGISEditorHistory.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/
// Purpose: Undo/redo command stack for the AGIS visual graph editor.
//          No UnityEditor dependency — pure C# command pattern.

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.RuntimeEditor
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Core interface
    // ─────────────────────────────────────────────────────────────────────────────

    public interface IEditorCommand
    {
        string DisplayName { get; }
        void Do();
        void Undo();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // History stack
    // ─────────────────────────────────────────────────────────────────────────────

    public sealed class AGISEditorHistory
    {
        private const int Capacity = 100;

        private readonly List<IEditorCommand> _undoStack = new List<IEditorCommand>(Capacity + 1);
        private readonly List<IEditorCommand> _redoStack = new List<IEditorCommand>(Capacity);

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public string NextUndoName => CanUndo ? _undoStack[_undoStack.Count - 1].DisplayName : null;
        public string NextRedoName => CanRedo ? _redoStack[_redoStack.Count - 1].DisplayName : null;

        /// <summary>
        /// Execute the command immediately and push it onto the undo stack.
        /// Clears the redo stack.
        /// </summary>
        public void Push(IEditorCommand cmd)
        {
            if (cmd == null) throw new ArgumentNullException(nameof(cmd));

            cmd.Do();

            _undoStack.Add(cmd);

            // Trim to capacity
            if (_undoStack.Count > Capacity)
                _undoStack.RemoveAt(0);

            // Pushing always discards the redo stack
            _redoStack.Clear();
        }

        public void Undo()
        {
            if (!CanUndo) return;

            var cmd = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            cmd.Undo();

            _redoStack.Add(cmd);
        }

        public void Redo()
        {
            if (!CanRedo) return;

            var cmd = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);

            cmd.Do();

            _undoStack.Add(cmd);
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Command implementations
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Moves one or more nodes by a delta in canvas space.
    /// </summary>
    public sealed class MoveNodesCommand : IEditorCommand
    {
        public string DisplayName => "Move Node(s)";

        private readonly AGISStateMachineGraph _graph;
        private readonly List<AGISGuid> _nodeIds;
        private readonly UnityEngine.Vector2 _delta;

        public MoveNodesCommand(AGISStateMachineGraph graph, IEnumerable<AGISGuid> nodeIds, UnityEngine.Vector2 delta)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _nodeIds = new List<AGISGuid>(nodeIds);
            _delta = delta;
        }

        public void Do()   => ApplyDelta(_delta);
        public void Undo() => ApplyDelta(-_delta);

        private void ApplyDelta(UnityEngine.Vector2 delta)
        {
            if (_graph.nodes == null) return;
            foreach (var node in _graph.nodes)
            {
                if (node == null) continue;
                foreach (var id in _nodeIds)
                {
                    if (node.nodeId == id)
                    {
                        node.visual ??= new AGISNodeVisualDef();
                        node.visual.position += delta;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    public sealed class AddNodeCommand : IEditorCommand
    {
        public string DisplayName => "Add Node";

        private readonly AGISStateMachineGraph _graph;
        private readonly AGISNodeInstanceDef _nodeDef;

        public AddNodeCommand(AGISStateMachineGraph graph, AGISNodeInstanceDef nodeDef)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _nodeDef = nodeDef ?? throw new ArgumentNullException(nameof(nodeDef));
        }

        public void Do()
        {
            if (_graph.nodes == null) _graph.nodes = new System.Collections.Generic.List<AGISNodeInstanceDef>();
            _graph.nodes.Add(_nodeDef);
        }

        public void Undo()
        {
            _graph.nodes?.Remove(_nodeDef);
        }
    }

    /// <summary>
    /// Removes a node and all edges connected to it.
    /// </summary>
    public sealed class RemoveNodeCommand : IEditorCommand
    {
        public string DisplayName => "Delete Node";

        private readonly AGISStateMachineGraph _graph;
        private readonly AGISGuid _nodeId;

        // Captured for undo
        private AGISNodeInstanceDef _removedNode;
        private readonly List<AGISTransitionEdgeDef> _removedEdges = new List<AGISTransitionEdgeDef>();

        public RemoveNodeCommand(AGISStateMachineGraph graph, AGISGuid nodeId)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _nodeId = nodeId;
        }

        public void Do()
        {
            _removedNode = null;
            _removedEdges.Clear();

            if (_graph.nodes != null)
            {
                for (int i = 0; i < _graph.nodes.Count; i++)
                {
                    if (_graph.nodes[i] != null && _graph.nodes[i].nodeId == _nodeId)
                    {
                        _removedNode = _graph.nodes[i];
                        _graph.nodes.RemoveAt(i);
                        break;
                    }
                }
            }

            if (_graph.edges != null)
            {
                for (int i = _graph.edges.Count - 1; i >= 0; i--)
                {
                    var edge = _graph.edges[i];
                    if (edge != null && (edge.fromNodeId == _nodeId || edge.toNodeId == _nodeId))
                    {
                        _removedEdges.Add(edge);
                        _graph.edges.RemoveAt(i);
                    }
                }
            }
        }

        public void Undo()
        {
            if (_removedNode != null)
            {
                _graph.nodes ??= new List<AGISNodeInstanceDef>();
                _graph.nodes.Add(_removedNode);
            }

            if (_removedEdges.Count > 0)
            {
                _graph.edges ??= new List<AGISTransitionEdgeDef>();
                foreach (var edge in _removedEdges)
                    _graph.edges.Add(edge);
            }
        }
    }

    /// <summary>
    /// Changes a single parameter value on a node.
    /// </summary>
    public sealed class ChangeNodeParamCommand : IEditorCommand
    {
        public string DisplayName => $"Change \"{_key}\"";

        private readonly AGISStateMachineGraph _graph;
        private readonly AGISGuid _nodeId;
        private readonly string _key;
        private readonly AGISValue _oldValue;
        private readonly AGISValue _newValue;
        private readonly bool _hadOldValue;

        public ChangeNodeParamCommand(AGISStateMachineGraph graph, AGISGuid nodeId,
            string key, AGISValue oldValue, AGISValue newValue, bool hadOldValue = true)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _nodeId = nodeId;
            _key = key;
            _oldValue = oldValue;
            _newValue = newValue;
            _hadOldValue = hadOldValue;
        }

        public void Do()   => SetParam(_newValue, setIt: true);
        public void Undo() => SetParam(_oldValue, setIt: _hadOldValue);

        private void SetParam(AGISValue value, bool setIt)
        {
            var node = FindNode();
            if (node == null) return;
            node.@params ??= new AGISParamTable();

            if (setIt)
                node.@params.Set(_key, value);
            else
                node.@params.Remove(_key);
        }

        private AGISNodeInstanceDef FindNode()
        {
            if (_graph.nodes == null) return null;
            foreach (var n in _graph.nodes)
                if (n != null && n.nodeId == _nodeId) return n;
            return null;
        }
    }

    /// <summary>
    /// Sets the graph's entry node.
    /// </summary>
    public sealed class SetEntryNodeCommand : IEditorCommand
    {
        public string DisplayName => "Set Entry Node";

        private readonly AGISStateMachineGraph _graph;
        private readonly AGISGuid _oldId;
        private readonly AGISGuid _newId;

        public SetEntryNodeCommand(AGISStateMachineGraph graph, AGISGuid oldId, AGISGuid newId)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _oldId = oldId;
            _newId = newId;
        }

        public void Do()   => _graph.entryNodeId = _newId;
        public void Undo() => _graph.entryNodeId = _oldId;
    }

    /// <summary>
    /// Adds a transition edge to the graph.
    /// </summary>
    public sealed class AddEdgeCommand : IEditorCommand
    {
        public string DisplayName => "Add Transition";

        private readonly AGISStateMachineGraph _graph;
        private readonly AGISTransitionEdgeDef _edgeDef;

        public AddEdgeCommand(AGISStateMachineGraph graph, AGISTransitionEdgeDef edgeDef)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _edgeDef = edgeDef ?? throw new ArgumentNullException(nameof(edgeDef));
        }

        public void Do()
        {
            _graph.edges ??= new List<AGISTransitionEdgeDef>();
            _graph.edges.Add(_edgeDef);
        }

        public void Undo() => _graph.edges?.Remove(_edgeDef);
    }

    /// <summary>
    /// Removes a transition edge from the graph.
    /// </summary>
    public sealed class RemoveEdgeCommand : IEditorCommand
    {
        public string DisplayName => "Delete Transition";

        private readonly AGISStateMachineGraph _graph;
        private readonly AGISGuid _edgeId;
        private AGISTransitionEdgeDef _removedEdge;

        public RemoveEdgeCommand(AGISStateMachineGraph graph, AGISGuid edgeId)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _edgeId = edgeId;
        }

        public void Do()
        {
            _removedEdge = null;
            if (_graph.edges == null) return;

            for (int i = 0; i < _graph.edges.Count; i++)
            {
                if (_graph.edges[i] != null && _graph.edges[i].edgeId == _edgeId)
                {
                    _removedEdge = _graph.edges[i];
                    _graph.edges.RemoveAt(i);
                    return;
                }
            }
        }

        public void Undo()
        {
            if (_removedEdge == null) return;
            _graph.edges ??= new List<AGISTransitionEdgeDef>();
            _graph.edges.Add(_removedEdge);
        }
    }

    /// <summary>
    /// Changes the priority of a transition edge.
    /// </summary>
    public sealed class ChangeEdgePriorityCommand : IEditorCommand
    {
        public string DisplayName => "Change Edge Priority";

        private readonly AGISStateMachineGraph _graph;
        private readonly AGISGuid _edgeId;
        private readonly int _oldPriority;
        private readonly int _newPriority;

        public ChangeEdgePriorityCommand(AGISStateMachineGraph graph, AGISGuid edgeId,
            int oldPriority, int newPriority)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _edgeId = edgeId;
            _oldPriority = oldPriority;
            _newPriority = newPriority;
        }

        public void Do()   => SetPriority(_newPriority);
        public void Undo() => SetPriority(_oldPriority);

        private void SetPriority(int priority)
        {
            var edge = FindEdge();
            if (edge != null) edge.priority = priority;
        }

        private AGISTransitionEdgeDef FindEdge()
        {
            if (_graph.edges == null) return null;
            foreach (var e in _graph.edges)
                if (e != null && e.edgeId == _edgeId) return e;
            return null;
        }
    }

    /// <summary>
    /// Changes the condition expression tree on a transition edge.
    /// </summary>
    public sealed class ChangeEdgeConditionCommand : IEditorCommand
    {
        public string DisplayName => "Change Edge Condition";

        private readonly AGISStateMachineGraph _graph;
        private readonly AGISGuid _edgeId;
        private readonly AGISConditionExprDef _oldCond;
        private readonly AGISConditionExprDef _newCond;

        public ChangeEdgeConditionCommand(AGISStateMachineGraph graph, AGISGuid edgeId,
            AGISConditionExprDef oldCond, AGISConditionExprDef newCond)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _edgeId = edgeId;
            _oldCond = oldCond;
            _newCond = newCond;
        }

        public void Do()   => SetCondition(_newCond);
        public void Undo() => SetCondition(_oldCond);

        private void SetCondition(AGISConditionExprDef cond)
        {
            var edge = FindEdge();
            if (edge != null) edge.condition = cond;
        }

        private AGISTransitionEdgeDef FindEdge()
        {
            if (_graph.edges == null) return null;
            foreach (var e in _graph.edges)
                if (e != null && e.edgeId == _edgeId) return e;
            return null;
        }
    }

    /// <summary>
    /// Changes the transition policy (interruptible, cooldown) on an edge.
    /// </summary>
    public sealed class ChangeEdgePolicyCommand : IEditorCommand
    {
        public string DisplayName => "Change Edge Policy";

        private readonly AGISStateMachineGraph _graph;
        private readonly AGISGuid _edgeId;
        private readonly AGISTransitionPolicy _oldPolicy;
        private readonly AGISTransitionPolicy _newPolicy;

        public ChangeEdgePolicyCommand(AGISStateMachineGraph graph, AGISGuid edgeId,
            AGISTransitionPolicy oldPolicy, AGISTransitionPolicy newPolicy)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _edgeId = edgeId;
            _oldPolicy = oldPolicy;
            _newPolicy = newPolicy;
        }

        public void Do()   => SetPolicy(_newPolicy);
        public void Undo() => SetPolicy(_oldPolicy);

        private void SetPolicy(AGISTransitionPolicy policy)
        {
            var edge = FindEdge();
            if (edge != null) edge.policy = policy;
        }

        private AGISTransitionEdgeDef FindEdge()
        {
            if (_graph.edges == null) return null;
            foreach (var e in _graph.edges)
                if (e != null && e.edgeId == _edgeId) return e;
            return null;
        }
    }
}
