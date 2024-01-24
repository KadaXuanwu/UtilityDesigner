using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KadaXuanwu.UtilityDesigner.Scripts.Evaluation;
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEditor;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts
{
    [DisallowMultipleComponent]
    public class UtilityDesigner : MonoBehaviour
    {
        [SerializeField] internal UtilityBehaviour utilityBehaviour;
        [SerializeField] internal string sceneReferencesObjName;
        [SerializeField] internal float tickRateEvaluation = 0.1f;
        [SerializeField] internal bool useUpdateAsTickRateForEvaluation;
        [SerializeField] internal float tickRateExecution = 0.1f;
        [SerializeField] internal bool useUpdateAsTickRateForExecution;
        [SerializeField] internal bool logEvaluationToFile;
        [SerializeField] internal float tickRateLog = 1f;
        
        private int _nextStateSetId;
        internal int NextStateSetId => _nextStateSetId++;

        private bool _stateLocked;
        internal bool StateLocked
        {
            get => _stateLocked;
            set
            {
                _stateLocked = value;
                if (!value)
                    Evaluate();
            }
        }
        
        internal List<ConsiderationSet> considerationSets = new();
        internal readonly Dictionary<ConsiderationSet, LocalConsiderationSet> localConsiderationSets = new();
        internal SceneReferences sceneReferences;
        internal int selectedStateSetId;
        internal bool lastSelectedExecution;
        internal int lastSelectedStateIndex;
        internal Root behaviourTreeRootNodeCache;
        internal BaseNode behaviourTreeNodeCache;
        
        private Dictionary<int, StateSet> _stateSets = new();
        private StateSet _runningStateSet;
        private State _executingState;
        private bool _utilityBehaviourMissing;
        
        
        internal StateSet SelectedStateSet
        {
            get
            {
                _stateSets.TryGetValue(selectedStateSetId, out StateSet stateSet);
                return stateSet;
            }
        }

        private void Awake()
        {
            Initialize();
            if (utilityBehaviour != null)
            {
                Load(utilityBehaviour);
                UnlinkUtilityBehaviour();
            }
            else
                _utilityBehaviourMissing = true;
            
            CreateLocalConsiderationSets();
        }

        private void Start()
        {
            if (utilityBehaviour == null)
                return;
            
            foreach (var state in _stateSets.Values.SelectMany(stateSet => stateSet.states))
                state.Initialize(this, gameObject);
            
            foreach (var considerationSet in considerationSets)
            {
                if (!considerationSet.local)
                    foreach (var consideration in considerationSet.considerations)
                        consideration.Initialize();
                else
                    foreach (var consideration in localConsiderationSets[considerationSet].considerations)
                        consideration.Initialize();
            }
            
            _runningStateSet = _stateSets[0];
            
            if (!useUpdateAsTickRateForEvaluation)
                StartCoroutine(HeartbeatEvaluation());
            
            if (!useUpdateAsTickRateForExecution)
                StartCoroutine(HeartbeatExecution());
            
#if UNITY_EDITOR
            if (logEvaluationToFile)
            {
                LogStatsToFile(true);
                StartCoroutine(HeartbeatLog());
            }
#endif
        }

        private void Update()
        {
            if (useUpdateAsTickRateForEvaluation)
                Evaluate();
            
            if (useUpdateAsTickRateForExecution)
                Execute();
        }

        internal void Initialize()
        {
            sceneReferences = GameObject.Find(sceneReferencesObjName)?.GetComponent<SceneReferences>();
        }

        internal Dictionary<int, StateSet> GetStateSets()
        {
            return _stateSets;
        }

        internal void AddStateSet(int id, StateSet stateSet)
        {
            _stateSets.Add(id, stateSet);
            
            if (utilityBehaviour != null)
                utilityBehaviour.stateSets.Add(stateSet);
        }

        internal void RemoveStateSet(int id)
        {
            _stateSets.TryGetValue(id, out StateSet oldStateSet);
            if (oldStateSet == null)
                return;

            _stateSets.Remove(id);

            if (utilityBehaviour != null)
                utilityBehaviour.stateSets.Remove(oldStateSet);
        }

        internal bool SetRunningStateSet(int index)
        {
            if (_stateSets.Count > index)
            {
                _runningStateSet = _stateSets[index];
                return true;
            }

            return false;
        }
        
#if UNITY_EDITOR
        internal void Save(string savePath)
        {
            UnlinkUtilityBehaviour();

            var saveCache = ScriptableObject.CreateInstance<UtilityBehaviour>();

            saveCache.stateSets = _stateSets.Values.ToList();
            saveCache.considerationSets = considerationSets;

            AssetDatabase.DeleteAsset(savePath);
            AssetDatabase.CreateAsset(saveCache, savePath);
            AssetDatabase.SaveAssets();

            Load(savePath);
        }

        internal bool Load(string loadPath)
        {
            UtilityBehaviour loadedUtilityBehaviour = AssetDatabase.LoadAssetAtPath<UtilityBehaviour>(loadPath);
            if (loadedUtilityBehaviour == null)
                return false;
            
            _stateSets.Clear();

            _nextStateSetId = 0;
            foreach (var stateSet in loadedUtilityBehaviour.stateSets)
                _stateSets.Add(NextStateSetId, stateSet);

            considerationSets = loadedUtilityBehaviour.considerationSets;
            
            utilityBehaviour = loadedUtilityBehaviour;

            return true;
        }
#endif
        
        internal void Load(UtilityBehaviour utilityBehaviourToLoad)
        {
            if (utilityBehaviourToLoad == null)
                return;
            
            _stateSets.Clear();

            _nextStateSetId = 0;
            foreach (var stateSet in utilityBehaviourToLoad.stateSets)
                _stateSets.Add(NextStateSetId, stateSet);

            considerationSets = utilityBehaviourToLoad.considerationSets;
            
            utilityBehaviour = utilityBehaviourToLoad;

            return;
        }
        
        internal void UnlinkUtilityBehaviour()
        {
            _nextStateSetId = 0;
            
            // Copy constructor
            Dictionary<int, StateSet> newStateSets = new();
            foreach (var stateSet in _stateSets.Values)
            {
                List<State> newStates = new();
                foreach (var state in stateSet.states)
                {
                    List<Precondition> newPreconditions = new();
                    foreach (var precondition in state.preconditions)
                    {
                        newPreconditions.Add(new Precondition
                        {
                            designation = precondition.designation,
                            considerationDesignation = precondition.considerationDesignation,
                            comparator = precondition.comparator,
                            value = precondition.value
                        });
                    }

                    List<Evaluator> newEvaluators = new();
                    foreach (var evaluator in state.evaluators)
                    {
                        newEvaluators.Add(new Evaluator
                        {
                            designation = evaluator.designation,
                            considerationDesignation = evaluator.considerationDesignation,
                            weight = evaluator.weight,
                            curve = evaluator.curve,
                            curveMinValue = evaluator.curveMinValue,
                            curveMaxValue = evaluator.curveMaxValue
                        });
                    }
                    
                    BehaviourTree newBehaviourTree = new();
                    if (state.behaviourTree != null && state.behaviourTree.rootNode != null)
                    {
                        newBehaviourTree.treeNodeState = state.behaviourTree.treeNodeState;
                        newBehaviourTree.rootNode = (Root)state.behaviourTree.rootNode.Clone();
                    }

                    newStates.Add(new State
                    {
                        preconditions = newPreconditions,
                        evaluators = newEvaluators,
                        behaviourTree = newBehaviourTree,
                        active = state.active,
                        designation = state.designation,
                        weight = state.weight,
                        executionFactor = state.executionFactor,
                        baseScore = state.baseScore,
                        setMinScore = state.setMinScore,
                        minScore = state.minScore,
                        setMaxScore = state.setMaxScore,
                        maxScore = state.maxScore,
                        failChance = state.failChance,
                        notes = state.notes
                    });
                }
                
                newStateSets.Add(NextStateSetId, new StateSet
                {
                    states = newStates,
                    designation = stateSet.designation
                });
            }

            _stateSets = newStateSets;
        }
        
        private void CreateLocalConsiderationSets()
        {
            foreach (var considerationSet in considerationSets)
            {
                if (!considerationSet.local)
                    continue;
                
                List<Consideration> newConsiderations = new();
                foreach (Consideration consideration in considerationSet.considerations)
                {
                    newConsiderations.Add(new Consideration
                    {
                        designation = consideration.designation,
                        type = consideration.type,
                        initialValue = consideration.initialValue,
                        setMinValue = consideration.setMinValue,
                        minValue = consideration.minValue,
                        setMaxValue = consideration.setMaxValue,
                        maxValue = consideration.maxValue,
                        changePerSecond = consideration.changePerSecond,
                        useRealTime = consideration.useRealTime,
                    });
                }
                
                localConsiderationSets.Add(considerationSet, new LocalConsiderationSet
                {
                    considerations = newConsiderations
                });
            }
        }


        private IEnumerator HeartbeatEvaluation()
        {
            float tickRate = Mathf.Clamp(tickRateEvaluation, 0.01f, 1f);
            while (true)
            {
                Evaluate();

                yield return new WaitForSecondsRealtime(tickRate);
            }
        }

        private void Evaluate()
        {
            if (_utilityBehaviourMissing)
                return;
            
            foreach (var considerationSet in considerationSets)
            {
                if (!considerationSet.local)
                    foreach (var consideration in considerationSet.considerations)
                        consideration.Update();
                else
                    foreach (var consideration in localConsiderationSets[considerationSet].considerations)
                        consideration.Update();
            }

            State nextState = _runningStateSet.DecideNextState(_executingState);
            if (nextState == null)
            {
                _executingState?.behaviourTree.Terminate();
                _executingState = null;
            }
            else if (!StateLocked && nextState != _executingState)
            {
                _executingState?.behaviourTree.Terminate();
                _executingState = nextState;
                    
                if (_executingState.behaviourTree.rootNode != null)
                    _executingState.behaviourTree.rootNode.nodeState = BaseNode.NodeState.Running;
            }
        }
        
        private IEnumerator HeartbeatExecution()
        {
            float tickRate = Mathf.Clamp(tickRateExecution, 0.01f, 1f);
            while (true)
            {
                Execute();
                
                yield return new WaitForSecondsRealtime(tickRate);
            }
        }

        private void Execute()
        {
            if (_utilityBehaviourMissing)
                return;
            
            if (_executingState?.behaviourTree.rootNode != null)
                _executingState.TickExecution(); // Only expensive when action nodes are expensive
        }

        private IEnumerator HeartbeatLog()
        {
            float tickRate = Mathf.Clamp(tickRateLog, 0.1f, 10f);
            while (true)
            {
                LogStatsToFile();
                
                yield return new WaitForSecondsRealtime(tickRate);
            }
        }
    
        private void LogStatsToFile(bool reset = false)
        {
            using StreamWriter sw = new StreamWriter("Assets/UtilityDesignerLog.txt", !reset);
            
            if (reset)
                return;
            
            sw.WriteLine($"- - - Game time: {Time.time} - - -");
            
            foreach (var state in _stateSets.Values.SelectMany(stateSet => stateSet.states))
            {
                sw.WriteLine(_executingState == state
                    ? $"STATE '{state.designation}' SCORED: {state.lastScore} >>> EXECUTING <<<"
                    : $"STATE '{state.designation}' SCORED: {state.lastScore}");

                foreach (var precondition in state.preconditions)
                    sw.WriteLine($"    Precondition '{precondition.designation}' met: {precondition.lastConditionMet}");

                float totalWeights = state.evaluators.Sum(evaluator => evaluator.weight);
                foreach (var evaluator in state.evaluators)
                    sw.WriteLine($"    Evaluator '{evaluator.designation}' scored: {evaluator.lastScore * evaluator.weight / totalWeights}");
            }
            
            sw.WriteLine("* * * * * * * * * * * * * * * *");
            
            foreach (ConsiderationSet considerationSet in considerationSets)
            {
                string setName = considerationSet.name;
                foreach (Consideration consideration in considerationSet.considerations)
                    sw.WriteLine($"Consideration '{setName}' '{consideration.designation}' value: {consideration.Value}");
            }
            
            sw.WriteLine();
            sw.WriteLine();
        }
    }
}
