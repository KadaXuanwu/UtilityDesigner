using System;
using System.Collections.Generic;
using System.Linq;
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Evaluation
{
    [Serializable]
    public class State
    {
        [SerializeField] internal List<Precondition> preconditions = new();
        [SerializeField] internal List<Evaluator> evaluators = new();
        [SerializeField] internal BehaviourTree behaviourTree = new();
        [SerializeField] internal bool active;
        [SerializeField] internal string designation;
        [SerializeField] internal float weight;
        [SerializeField] internal float executionFactor;
        [SerializeField] internal float baseScore;
        [SerializeField] internal bool setMinScore;
        [SerializeField] internal float minScore;
        [SerializeField] internal bool setMaxScore;
        [SerializeField] internal float maxScore;
        [SerializeField] internal float failChance;
        [SerializeField] internal string notes;

        internal float lastScore;
        
        
        internal float GetScore(bool currentState)
        {
            if (!active)
                return -1;
            
            if (preconditions.Any(precondition => !precondition.ConditionMet()))
            {
                lastScore = 0;
                return -1;
            }

            float score = baseScore;
            float evaluatorWeightsTotal = evaluators.Sum(evaluator => evaluator.weight);
            if (evaluatorWeightsTotal > 0)
                score += evaluators.Sum(evaluator => evaluator.ScoreConsideration() * (evaluator.weight / evaluatorWeightsTotal));

            score *= weight;
            
            if (currentState)
                score *= executionFactor;
            
            if (setMinScore && score < minScore)
                score = minScore;
            
            if (setMaxScore && score > maxScore)
                score = maxScore;

            lastScore = score;
            return score;
        }

        internal void Initialize(global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner utilityDesigner, GameObject thisGameObject)
        {
            preconditions.ForEach(precondition => precondition.Initialize(utilityDesigner));
            evaluators.ForEach(evaluator => evaluator.Initialize(utilityDesigner));
            lastScore = 0;

            if (behaviourTree == null)
                return;
            
            behaviourTree.InitializeNodeList();
            
            foreach (var node in behaviourTree.nodes)
                node.InitializeNode(utilityDesigner, thisGameObject);
        }

        internal void TickExecution()
        {
            behaviourTree.Update();
        }
    }
}
