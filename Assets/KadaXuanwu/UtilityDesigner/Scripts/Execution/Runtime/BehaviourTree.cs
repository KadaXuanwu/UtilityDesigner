using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime
{
    [Serializable]
    public class BehaviourTree
    {
        [SerializeReference] internal Root rootNode;

        internal List<BaseNode> nodes = new();
        internal BaseNode.NodeState treeNodeState = BaseNode.NodeState.Running;

        
        internal BaseNode.NodeState Update()
        {
            if (rootNode.nodeState == BaseNode.NodeState.Running && rootNode.child != null)
                treeNodeState = rootNode.Update();

            return treeNodeState;
        }
        
        internal void Terminate()
        {
            rootNode.Terminate();
        }
        
        internal void InitializeNodeList()
        {
            RemoveMissingScriptNodes(rootNode);
            nodes = GetAllNodes(rootNode);
        }
        
        private void RemoveMissingScriptNodes(BaseNode node)
        {
            switch (node)
            {
                case null:
                    return;
                case Root root when root.child == null:
                    root.child = null;
                    break;
                case Root root:
                    RemoveMissingScriptNodes(root.child);
                    break;
                case CompositeNode compositeNode:
                {
                    for (int i = compositeNode.children.Count - 1; i >= 0; i--)
                    {
                        if (compositeNode.children[i] == null)
                            compositeNode.children.RemoveAt(i);
                        else
                            RemoveMissingScriptNodes(compositeNode.children[i]);
                    }

                    break;
                }
                case DecoratorNode decoratorNode when decoratorNode.child == null:
                    decoratorNode.child = null;
                    break;
                case DecoratorNode decoratorNode:
                    RemoveMissingScriptNodes(decoratorNode.child);
                    break;
            }
        }
        
        internal List<BaseNode> GetAllNodes(BaseNode startNode)
        {
            List<BaseNode> nodeList = new();

            if (startNode != null)
                TraverseNodesDfs(startNode, nodeList);

            return nodeList;
        }

        private void TraverseNodesDfs(BaseNode currentNode, ICollection<BaseNode> nodeList)
        {
            nodeList.Add(currentNode);

            switch (currentNode)
            {
                case Root root:
                    if (root.child != null)
                        TraverseNodesDfs(root.child, nodeList);
                    break;
                case CompositeNode compositeNode:
                    foreach (BaseNode childNode in compositeNode.children)
                        TraverseNodesDfs(childNode, nodeList);
                    break;
                case DecoratorNode decoratorNode:
                    if (decoratorNode.child != null)
                        TraverseNodesDfs(decoratorNode.child, nodeList);
                    break;
            }
        }

#if UNITY_EDITOR
        private BaseNode CreateNodeInstance(Type type)
        {
            if (Activator.CreateInstance(type) is not BaseNode node)
                return null;
            
            node.guid = GUID.Generate().ToString();
            return node;

        }
        
        internal BaseNode CreateNode(Type type, Vector2 pos)
        {
            BaseNode node = CreateNodeInstance(type);
            node.position = pos;
            nodes.Add(node);
            return node;
        }

        internal void DeleteNode(BaseNode node)
        {
            nodes.Remove(node);
            AssetDatabase.SaveAssets();
        }

        internal void AddChild(BaseNode parent, BaseNode child)
        {
            switch (parent)
            {
                case DecoratorNode decorator:
                    decorator.child = child;
                    break;
                case CompositeNode composite:
                    composite.children.Add(child);
                    break;
                case Root root:
                    root.child = child;
                    break;
            }
        }
        
        internal void RemoveChild(BaseNode parent, BaseNode child = null)
        {
            switch (parent)
            {
                case DecoratorNode decorator:
                    decorator.child = null;
                    break;
                case CompositeNode composite when composite.children.Contains(child):
                    composite.children.Remove(child);
                    break;
                case Root root:
                    root.child = null;
                    break;
            }
        }

        internal List<BaseNode> GetChildren(BaseNode parent)
        {
            List<BaseNode> children = new();

            switch (parent)
            {
                case DecoratorNode decorator when decorator.child != null:
                    children.Add(decorator.child);
                    break;
                case CompositeNode composite:
                    return composite.children;
                case Root root when root.child != null:
                    children.Add(root.child);
                    break;
            }

            return children;
        }
#endif
    }
}
