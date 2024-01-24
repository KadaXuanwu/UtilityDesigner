#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Editor
{
    public class NodeView : Node
    {
        internal Action<NodeView> onNodeSelected;
        internal readonly BaseNode node;
        internal Port input;
        internal Port output;

        private readonly Label _labelDetails;
        private readonly VisualElement _titleContainer;
        
        
        internal NodeView(BaseNode baseNode) : base(LoadUxml())
        {
            node = baseNode;
            title = Utils.AddSpacesBeforeUppercase(node.GetType().Name);
            viewDataKey = node.guid;
            
            style.left = node.position.x;
            style.top = node.position.y;

            CreateInputPorts();
            CreateOutputPorts();
            SetupClasses();
            
            if (node is Root)
                capabilities &= ~Capabilities.Deletable;

            _labelDetails = this.Q<Label>("details-label");
            _titleContainer = this.Q<VisualElement>("title");
            UpdateNodeDetails();
        }

        private static string LoadUxml()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            MonoScript scriptAsset = MonoImporter.GetAllRuntimeMonoScripts()
                .Where(script => script != null && script.GetClass() != null)
                .FirstOrDefault(script => script.GetClass().Assembly == assembly);

            string scriptPath = AssetDatabase.GetAssetPath(scriptAsset);
            string scriptsDirectory = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)));
            
            return scriptsDirectory != null ? Path.Combine(scriptsDirectory, "Editor/UXML/NodeView.uxml") : null;
        }

        private void UpdateNodeDetails()
        {
            string text = node.GetSerializedValue();
            if (text == null)
            {
                _labelDetails.parent.style.display = DisplayStyle.None;
                _titleContainer.style.paddingTop = 4;
                _titleContainer.style.paddingBottom = 4;
            }
            else
                _labelDetails.text = node.GetSerializedValue();
        }
        
        public sealed override string title
        {
            get => base.title;
            set => base.title = value;
        }

        private void SetupClasses()
        {
            switch (node)
            {
                case ActionNode:
                    AddToClassList("action");
                    break;
                case ConditionalNode:
                    AddToClassList("conditional");
                    break;
                case CompositeNode:
                    AddToClassList("composite");
                    break;
                case DecoratorNode:
                    AddToClassList("decorator");
                    break;
                case Root:
                    AddToClassList("root");
                    break;
            }
        }
        
        private void CreateInputPorts()
        {
            switch (node)
            {
                case ActionNode:
                case ConditionalNode:
                case CompositeNode:
                case DecoratorNode:
                    input = new NodePort(Direction.Input, Port.Capacity.Single);
                    break;
                case Root:
                    break;
            }

            if (input == null)
                return;
            
            input.portName = "";
            input.style.flexDirection = FlexDirection.Column;
            inputContainer.Add(input);

            inputContainer.style.height = 14f;
            inputContainer.style.flexDirection = FlexDirection.Row;
        }

        private void CreateOutputPorts()
        {
            switch (node)
            {
                case ActionNode:
                case ConditionalNode:
                    break;
                case CompositeNode:
                    output = new NodePort(Direction.Output, Port.Capacity.Multi);
                    break;
                case DecoratorNode:
                case Root:
                    output = new NodePort(Direction.Output, Port.Capacity.Single);
                    break;
            }

            if (output == null)
                return;
            
            output.portName = "";
            output.style.flexDirection = FlexDirection.ColumnReverse;
            outputContainer.Add(output);

            outputContainer.style.height = 14f;
            outputContainer.style.flexDirection = FlexDirection.Row;
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            
            node.position.x = newPos.xMin;
            node.position.y = newPos.yMin;
        }

        public override void OnSelected()
        {
            base.OnSelected();
            onNodeSelected?.Invoke(this);
            UpdateNodeDetails();
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            onNodeSelected?.Invoke(null);
            UpdateNodeDetails();
        }

        internal void SortChildren()
        {
            if (node is CompositeNode composite)
                composite.children.Sort(SortByHorizontalPosition);
        }

        private int SortByHorizontalPosition(BaseNode left, BaseNode right)
        {
            return left.position.x < right.position.x ? -1 : 1;
        }

        internal void UpdateState()
        {
            RemoveFromClassList("running");
            RemoveFromClassList("failure");
            RemoveFromClassList("success");

            switch (node.nodeState)
            {
                case BaseNode.NodeState.Running:
                    if (node.enabled)
                        AddToClassList("running");
                    break;
                case BaseNode.NodeState.Failure:
                    AddToClassList("failure");
                    break;
                case BaseNode.NodeState.Success:
                    AddToClassList("success");
                    break;
            }
        }
    }
}
#endif