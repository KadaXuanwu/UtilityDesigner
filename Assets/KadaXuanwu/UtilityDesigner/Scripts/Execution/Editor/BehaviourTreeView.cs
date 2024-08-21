#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using KadaXuanwu.UtilityDesigner.Scripts.Editor;
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Editor
{
    public class BehaviourTreeView : GraphView
    {
        internal Action<NodeView> onNodeSelected;
        internal new class UxmlFactory : UxmlFactory<BehaviourTreeView, UxmlTraits> { }

        private BehaviourTree _behaviourTree;
        private UtilityDesigner _utilityDesigner;
        private UtilityDesignerEditorWindow _utilityDesignerEditorWindow;
        

        public BehaviourTreeView()
        {
            styleSheets.Add(Resources.Load<StyleSheet>("BehaviourTreeView"));
            
            Insert(0, new GridBackground());
            
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            if (e.ctrlKey)
                switch (e.keyCode)
                {
                    case KeyCode.C:
                        _utilityDesignerEditorWindow.CopyNode();
                        e.StopPropagation();
                        break;
                    case KeyCode.V:
                        _utilityDesignerEditorWindow.PasteNode();
                        e.StopPropagation();
                        break;
                }
        }

        private NodeView FindNodeView(BaseNode node)
        {
            return GetNodeByGuid(node.guid) as NodeView;
        }
        
        internal void PopulateView(BehaviourTree tree, UtilityDesigner utilityDesigner, UtilityDesignerEditorWindow utilityDesignerEditorWindow)
        {
            if (tree == null || utilityDesigner == null)
                return;

            _behaviourTree = tree;
            _utilityDesigner = utilityDesigner;
            _utilityDesignerEditorWindow = utilityDesignerEditorWindow;

            graphViewChanged -= OnGraphViewChanged;
            DeleteElements(graphElements);
            graphViewChanged += OnGraphViewChanged;

            tree.rootNode ??= tree.CreateNode(typeof(Root), new Vector2(375, 10)) as Root;

            _behaviourTree.InitializeNodeList();

            _behaviourTree.nodes.ForEach(CreateNodeView);
            
            _behaviourTree.nodes.ForEach(node =>
            {
                var children = tree.GetChildren(node);
                children.ForEach(child =>
                {
                    NodeView parentView = FindNodeView(node);
                    NodeView childView = FindNodeView(child);
                    
                    AddElement(parentView.output.ConnectTo(childView.input));
                });
            });
        }
        
        internal void PopulateViewWithSubtree(BaseNode startNode)
        {
            List<BaseNode> allNodes = _behaviourTree.GetAllNodes(startNode);

            if (allNodes == null || allNodes.Count == 0)
                return;
            
            allNodes.ForEach(node => node.guid = GUID.Generate().ToString());

            Vector2 positionDifference = this.WorldToLocal(
                this.ChangeCoordinatesTo(contentViewContainer, Event.current.mousePosition)) - allNodes[0].position;
            allNodes.ForEach(node =>
            {
                _behaviourTree.nodes.Add(node);
                node.position += positionDifference;
                CreateNodeView(node);
            });

            allNodes.ForEach(node =>
            {
                var children = _behaviourTree.GetChildren(node);
                children.ForEach(child =>
                {
                    NodeView parentView = FindNodeView(node);
                    NodeView childView = FindNodeView(child);

                    AddElement(parentView.output.ConnectTo(childView.input));
                });
            });
        }
        
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(
                endPort => endPort.direction != startPort.direction && endPort.node != startPort.node).ToList();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            graphViewChange.elementsToRemove?.ForEach(element =>
            {
                switch (element)
                {
                    case NodeView nodeView:
                        _behaviourTree.DeleteNode(nodeView.node);
                        break;
                    case Edge edge:
                    {
                        if (edge.output.node is NodeView parentView && edge.input.node is NodeView childView)
                            _behaviourTree.RemoveChild(parentView.node, childView.node);
                        break;
                    }
                }
            });

            graphViewChange.edgesToCreate?.ForEach(edge =>
            {
                if (edge.output.node is NodeView parentView && edge.input.node is NodeView childView)
                    _behaviourTree.AddChild(parentView.node, childView.node);
            });

            if (graphViewChange.movedElements != null)
            {
                nodes.ForEach(n =>
                {
                    NodeView nodeView = n as NodeView;
                    
                    nodeView?.SortChildren();
                });
            }

            return graphViewChange;
        }
        
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            Vector2 nodePos = this.ChangeCoordinatesTo(contentViewContainer, evt.localMousePosition);
            AddItems<ActionNode>(evt.menu, nodePos, "Actions");
            AddItems<ConditionalNode>(evt.menu, nodePos, "Conditionals");
            AddItems<CompositeNode>(evt.menu, nodePos, "Composites");
            AddItems<DecoratorNode>(evt.menu, nodePos, "Decorators");
        }

        internal void CreateContextualMenu(Vector2 position)
        {
            GenericMenu menu = new GenericMenu();
            AddItems<ActionNode>(menu, position, "Actions");
            AddItems<ConditionalNode>(menu, position, "Conditionals");
            AddItems<CompositeNode>(menu, position, "Composites");
            AddItems<DecoratorNode>(menu, position, "Decorators");
            menu.ShowAsContext();
        }
        
        private void AddItems<T>(DropdownMenu menu, Vector2 position, string menuCategory) where T : class
        {
            var types = TypeCache.GetTypesDerivedFrom<T>();
            foreach (var type in types)
            {
                if (type.IsAbstract) continue;

                var isUtilityDesignerAssembly = type.Assembly.GetName().Name == "UtilityDesigner";
                var path = isUtilityDesignerAssembly
                    ? $"{menuCategory}/{Utils.AddSpacesBeforeUppercase(type.Name)}"
                    : $"{menuCategory} (custom)/{Utils.AddSpacesBeforeUppercase(type.Name)}";
                menu.AppendAction(path, (a) => CreateNode(type, position));
            }
        }
        
        private void AddItems<T>(GenericMenu menu, Vector2 position, string menuCategory) where T : class
        {
            var types = TypeCache.GetTypesDerivedFrom<T>();
            foreach (var type in types)
            {
                if (type.IsAbstract) continue;

                var isUtilityDesignerAssembly = type.Assembly.GetName().Name == "UtilityDesigner";
                var path = isUtilityDesignerAssembly
                    ? $"{menuCategory}/{Utils.AddSpacesBeforeUppercase(type.Name)}"
                    : $"{menuCategory} (custom)/{Utils.AddSpacesBeforeUppercase(type.Name)}";
                menu.AddItem(new GUIContent(path), false, () => CreateNode(type, position));
            }
        }
        
        private void CreateNode(Type type, Vector2 pos)
        {
            BaseNode node = _behaviourTree.CreateNode(type, pos);
            CreateNodeView(node);
        }

        private void CreateNodeView(BaseNode node)
        {
            if (!Application.isPlaying)
                node.InitializeNode(_utilityDesigner);
            
            NodeView nodeView = new NodeView(node)
            {
                onNodeSelected = onNodeSelected
            };
            AddElement(nodeView);
        }

        internal void UpdateNodeStates() {
            nodes.ForEach(n =>
            {
                NodeView view = n as NodeView;
                view?.UpdateState();
            });
        }
    }
}
#endif