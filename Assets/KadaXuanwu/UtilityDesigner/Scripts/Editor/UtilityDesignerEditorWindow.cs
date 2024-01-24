#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using KadaXuanwu.UtilityDesigner.Scripts.Evaluation;
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Editor;
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KadaXuanwu.UtilityDesigner.Scripts.Editor
{
    public class UtilityDesignerEditorWindow : EditorWindow
    {
        private static readonly Vector2 WindowMinSize = new(1350, 600);
        private const float ItemStateHeight = 80;
        private const float ItemPreconditionHeight = 130;
        private const float ItemEvaluatorHeight = 160;
        private const float ItemConsiderationSetHeight = 70;
        private const float ItemConsiderationHeight = 105;

        private static UtilityDesigner _utilityDesigner;
        private VisualElement _specificsContainer;
        private UnityEditor.Editor _editor;
        private TextField _textFieldFileName;

        // Sections
        private VisualElement _evaluationSection;
        private VisualElement _executionSection;
        private Button _buttonEvaluation;
        private Button _buttonExecution;

        // State Sets
        private VisualElement _stateSetTab;
        private ToolbarMenu _menuStateSets;
        private readonly List<int> _stateSetIdsOrdered = new();

        // States
        private VisualTreeAsset _templateItemState;
        private VisualElement _statesContent;
        private ListView _statesListView;
        private State _selectedState;
        private Toggle _toggleStateActive;
        private TextField _textFieldStateDesignation;
        private FloatField _floatFieldStateWeight;
        private FloatField _floatFieldStateExecutionFactor;
        private FloatField _floatFieldStateBaseScore;
        private Toggle _toggleStateSetMinScore;
        private FloatField _floatFieldStateMinScore;
        private Toggle _toggleStateSetMaxScore;
        private FloatField _floatFieldStateMaxScore;
        private Slider _sliderStateFailChance;
        private TextField _textFieldStateNotes;
        private readonly Dictionary<State, VisualElement> _stateContainersOrdered = new();

        // Preconditions
        private VisualTreeAsset _templateItemPrecondition;
        private VisualElement _preconditionsContent;
        private ListView _preconditionsListView;
        private Label _preconditionLastUsedLabelTitle;
        private TextField _preconditionLastUsedTextFieldRename;
        private readonly Dictionary<Precondition, VisualElement> _preconditionContainersOrdered = new();

        // Evaluators
        private VisualTreeAsset _templateItemEvaluator;
        private VisualElement _evaluatorsContent;
        private ListView _evaluatorsListView;
        private Label _evaluatorLastUsedLabelTitle;
        private TextField _evaluatorLastUsedTextFieldRename;
        private readonly Dictionary<Evaluator, VisualElement> _evaluatorContainersOrdered = new();

        // Consideration Set
        private VisualTreeAsset _templateItemConsiderationSet;
        private VisualElement _considerationSetsContent;
        private ListView _considerationSetsListView;
        private ConsiderationSet _selectedConsiderationSet;

        // Considerations
        private VisualTreeAsset _templateItemConsideration;
        private VisualElement _considerationsContent;
        private ListView _considerationsListView;
        private readonly Dictionary<Consideration, VisualElement> _considerationContainersOrdered = new();

        // Execution
        private BehaviourTreeView _treeView;
        private VisualElement _inspectorContent;
        private Label _labelNodeDescription;
        private NodeView _selectedNodeView;


        internal static void OpenWindow()
        {
            UtilityDesignerEditorWindow editorWindow = GetWindow<UtilityDesignerEditorWindow>();
            editorWindow.titleContent = new GUIContent("Utility Designer");
            editorWindow.minSize = WindowMinSize;
        }

        private void OnGUI()
        {
            CheckForNewConsiderationSetEvent();
        }

        private void OnDestroy()
        {
            if (_utilityDesigner.utilityBehaviour == null)
                return;
            
            EditorUtility.SetDirty(_utilityDesigner.utilityBehaviour);
            AssetDatabase.SaveAssets();
        }

        private void CreateGUI()
        {
            // Cache information
            VisualElement root = rootVisualElement;
            string scriptDirectory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));

            // Load UXML
            VisualElement rootFromUxml = 
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{scriptDirectory}/UXML/UtilityDesigner.uxml").Instantiate();
            root.Add(rootFromUxml);

            // Store references
            if (Selection.activeGameObject != null &&
                Selection.activeGameObject.GetComponent<UtilityDesigner>() != null)
            {
                _utilityDesigner = Selection.activeGameObject.GetComponent<UtilityDesigner>();
                _utilityDesigner.Initialize();
                if (_utilityDesigner.sceneReferences != null)
                    _utilityDesigner.sceneReferences.Initialize();
            }

            if (_utilityDesigner == null)
            {
                Close();
                return;
            }
            
            _templateItemState =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{scriptDirectory}/UXML/TemplateItemState.uxml");
            _templateItemPrecondition =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{scriptDirectory}/UXML/TemplateItemPrecondition.uxml");
            _templateItemEvaluator =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{scriptDirectory}/UXML/TemplateItemEvaluator.uxml");
            _templateItemConsiderationSet =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    $"{scriptDirectory}/UXML/TemplateItemConsiderationSet.uxml");
            _templateItemConsideration =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    $"{scriptDirectory}/UXML/TemplateItemConsideration.uxml");

            _evaluationSection = root.Q<VisualElement>("EvaluationSection");
            _executionSection = root.Q<VisualElement>("ExecutionSection");
            _specificsContainer = root.Q<VisualElement>("SpecificsContainer");
            _buttonEvaluation = root.Q<Button>("ButtonEvaluation");
            _buttonExecution = root.Q<Button>("ButtonExecution");
            _textFieldFileName = root.Q<TextField>("TextFieldFileName");
            
            _statesContent = root.Q<VisualElement>("StatesContent");
            _menuStateSets = root.Q<ToolbarMenu>("MenuStateSets");
            _stateSetTab = root.Q<VisualElement>("StateSetsTab");
            _toggleStateActive = _evaluationSection.Q<Toggle>("ToggleStateActive");
            _textFieldStateDesignation = _evaluationSection.Q<TextField>("TextFieldStateName");
            _floatFieldStateWeight = _evaluationSection.Q<FloatField>("FloatFieldStateWeight");
            _floatFieldStateExecutionFactor = _evaluationSection.Q<FloatField>("FloatFieldStateExecutionFactor");
            _floatFieldStateBaseScore = _evaluationSection.Q<FloatField>("FloatFieldStateBaseScore");
            _toggleStateSetMinScore = _evaluationSection.Q<Toggle>("ToggleStateSetMinScore");
            _floatFieldStateMinScore = _evaluationSection.Q<FloatField>("FloatFieldStateMinScore");
            _toggleStateSetMaxScore = _evaluationSection.Q<Toggle>("ToggleStateSetMaxScore");
            _floatFieldStateMaxScore = _evaluationSection.Q<FloatField>("FloatFieldStateMaxScore");
            _sliderStateFailChance = _evaluationSection.Q<Slider>("SliderStateFailChance");
            _textFieldStateNotes = _evaluationSection.Q<TextField>("TextFieldStateNotes");
            _labelNodeDescription = _executionSection.Q<Label>("LabelNodeDescription");

            _preconditionsContent = root.Q<VisualElement>("PreconditionsContent");
            _evaluatorsContent = root.Q<VisualElement>("EvaluatorsContent");
            _considerationSetsContent = root.Q<VisualElement>("ConsiderationSetsContent");
            _considerationsContent = root.Q<VisualElement>("ConsiderationsContent");
            _inspectorContent = root.Q<VisualElement>("ExecutionInspectorContent");

            _treeView = root.Q<BehaviourTreeView>();

            // Load Asset
            if (_utilityDesigner.utilityBehaviour != null)
            {
                if (!Application.isPlaying)
                    _utilityDesigner.Load(_utilityDesigner.utilityBehaviour);
                root.Q<TextField>("TextFieldFileName").value = _utilityDesigner.utilityBehaviour.name;
            }
            else
                _utilityDesigner.UnlinkUtilityBehaviour();

            // Add button events
            _buttonEvaluation.clicked += () =>
            {
                if (_utilityDesigner.lastSelectedExecution)
                    InitializeEvaluationTab();
            };
            _buttonExecution.clicked += () =>
            {
                if (!_utilityDesigner.lastSelectedExecution)
                    InitializeExecutionTab();
            };
            root.Q<Button>("ButtonSave").clicked += Save;
            root.Q<Button>("ButtonLoad").clicked += Load;
            root.Q<Button>("ButtonAddStateSet").clicked += AddStateSet;
            root.Q<Button>("ButtonRemoveStateSet").clicked += RemoveStateSet;
            root.Q<Button>("ButtonRenameStateSet").clicked += OpenStateSetRenameMenu;
            root.Q<Button>("ButtonAddState").clicked += AddState;

            // Initialize
            _utilityDesigner.considerationSets.RemoveAll(item => item == null);

            LoadStateSetDropdown();
            if (_utilityDesigner.GetStateSets().Count == 0)
                AddStateSet();
            else
                LoadStateSet(_utilityDesigner.selectedStateSetId);

            if (_utilityDesigner.lastSelectedExecution)
                InitializeExecutionTab();
            else
                InitializeEvaluationTab();
        }

        private void InitializeEvaluationTab()
        {
            DisableExecutionTab();

            // Display the correct tab
            _evaluationSection.style.display = DisplayStyle.Flex;
            _specificsContainer.style.visibility = Visibility.Hidden;
            _executionSection.style.display = DisplayStyle.None;

            _buttonEvaluation.style.backgroundColor = new StyleColor(new Color32(0x4D, 0x4D, 0x4D, 0xFF));
            _buttonExecution.style.backgroundColor = StyleKeyword.Null;

            // Cache information
            VisualElement root = rootVisualElement;

            // Add button events
            root.Q<Button>("ButtonAddPrecondition").clicked += AddPrecondition;
            root.Q<Button>("ButtonAddEvaluator").clicked += AddEvaluator;
            root.Q<Button>("ButtonAddConsiderationSet").clicked += AddConsiderationSet;

            // Register value changed callbacks
            _toggleStateActive.RegisterValueChangedCallback(evt =>
            {
                _selectedState.active = evt.newValue;
                _statesListView.Rebuild();
            });
            _textFieldStateDesignation.RegisterValueChangedCallback(evt =>
            {
                _selectedState.designation = Utils.VerifyItemName("State ",
                    evt.newValue,
                    _utilityDesigner.SelectedStateSet.states,
                    state => state.designation,
                    _selectedState.designation);
                _statesListView.Rebuild();
            });
            _floatFieldStateWeight.RegisterValueChangedCallback(evt =>
            {
                float value = evt.newValue < 0 ? 0 : evt.newValue;
                _floatFieldStateWeight.value = value;
                _selectedState.weight = value;
                _statesListView.Rebuild();
            });
            _floatFieldStateExecutionFactor.RegisterValueChangedCallback(evt =>
            {
                float value = evt.newValue < 0 ? 0 : evt.newValue;
                _floatFieldStateExecutionFactor.value = value;
                _selectedState.executionFactor = value;
                _statesListView.Rebuild();
            });
            _floatFieldStateBaseScore.RegisterValueChangedCallback(evt =>
            {
                _selectedState.baseScore = evt.newValue;
                _statesListView.Rebuild();
            });
            _toggleStateSetMinScore.RegisterValueChangedCallback(evt =>
            {
                _selectedState.setMinScore = evt.newValue;
                _floatFieldStateMinScore.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                _statesListView.Rebuild();
            });
            _floatFieldStateMinScore.RegisterValueChangedCallback(evt =>
            {
                float value = evt.newValue < 0 ? 0 : evt.newValue;
                _floatFieldStateMinScore.value = value;
                _selectedState.minScore = value;
                _statesListView.Rebuild();
            });
            _toggleStateSetMaxScore.RegisterValueChangedCallback(evt =>
            {
                _selectedState.setMaxScore = evt.newValue;
                _floatFieldStateMaxScore.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                _statesListView.Rebuild();
            });
            _floatFieldStateMaxScore.RegisterValueChangedCallback(evt =>
            {
                float value = evt.newValue < 0 ? 0 : evt.newValue;
                _floatFieldStateMaxScore.value = value;
                _selectedState.maxScore = value;
                _statesListView.Rebuild();
            });
            _sliderStateFailChance.RegisterValueChangedCallback(evt =>
            {
                _selectedState.failChance = evt.newValue;
                _statesListView.Rebuild();
            });
            _sliderStateFailChance.Q<TextField>().RegisterValueChangedCallback(evt =>
            {
                if (!float.TryParse(evt.newValue, out float newValue))
                    return;
                
                _selectedState.failChance = newValue;
                _statesListView.Rebuild();
            });
            _textFieldStateNotes.RegisterValueChangedCallback(evt =>
            {
                _selectedState.notes = evt.newValue;
                _statesListView.Rebuild();
            });

            // Initialize
            if (_selectedState != null)
                LoadStateEvaluationView();
            
            if (_utilityDesigner.considerationSets == null)
                return;
            GenerateConsiderationSetListView();

            if (_utilityDesigner.considerationSets.Count > 0)
                _considerationSetsListView.SetSelection(0);

            if (_utilityDesigner.SelectedStateSet.states.Count > _utilityDesigner.lastSelectedStateIndex)
                _statesListView.SetSelection(_utilityDesigner.lastSelectedStateIndex);
            
            _utilityDesigner.lastSelectedExecution = false;
        }

        private void DisableEvaluationTab()
        {
            // Cache information
            VisualElement root = rootVisualElement;

            // Remove button events
            root.Q<Button>("ButtonAddPrecondition").clicked -= AddPrecondition;
            root.Q<Button>("ButtonAddEvaluator").clicked -= AddEvaluator;
            root.Q<Button>("ButtonAddConsiderationSet").clicked -= AddConsiderationSet;

            // Clear Lists
            _considerationsContent.Clear();
            _considerationSetsContent.Clear();
        }

        private void InitializeExecutionTab()
        {
            DisableEvaluationTab();

            // Display the correct tab
            _evaluationSection.style.display = DisplayStyle.None;
            _executionSection.style.visibility = Visibility.Hidden;
            _executionSection.style.display = DisplayStyle.Flex;
            
            _buttonEvaluation.style.backgroundColor = StyleKeyword.Null;
            _buttonExecution.style.backgroundColor = new StyleColor(new Color32(0x4D, 0x4D, 0x4D, 0xFF));

            // Cache information
            VisualElement root = rootVisualElement;

            // Add button events
            root.Q<Button>("ButtonAddNode").clicked += AddNode;
            root.Q<Button>("ButtonCopyBT").clicked += CopyGraph;
            root.Q<Button>("ButtonPasteBT").clicked += PasteGraph;

            // Initialize
            _treeView.onNodeSelected = OnNodeSelectionChanged;
            
            if (_selectedState != null)
                LoadStateExecutionView();
            
            if (_utilityDesigner.SelectedStateSet.states.Count > _utilityDesigner.lastSelectedStateIndex)
                _statesListView.SetSelection(_utilityDesigner.lastSelectedStateIndex);
            
            _utilityDesigner.lastSelectedExecution = true;
        }

        private void DisableExecutionTab()
        {
            // Cache information
            VisualElement root = rootVisualElement;

            // Remove button events
            root.Q<Button>("ButtonAddNode").clicked -= AddNode;
            root.Q<Button>("ButtonCopyBT").clicked -= CopyGraph;
            root.Q<Button>("ButtonPasteBT").clicked -= PasteGraph;
        }

        private void LoadStateEvaluationView()
        {
            _specificsContainer.style.visibility = Visibility.Visible;

            _toggleStateActive.value = _selectedState.active;
            _textFieldStateDesignation.value = _selectedState.designation;
            _floatFieldStateWeight.value = _selectedState.weight;
            _floatFieldStateExecutionFactor.value = _selectedState.executionFactor;
            _floatFieldStateBaseScore.value = _selectedState.baseScore;
            _toggleStateSetMinScore.value = _selectedState.setMinScore;
            _floatFieldStateMinScore.value = _selectedState.minScore;
            _toggleStateSetMaxScore.value = _selectedState.setMaxScore;
            _floatFieldStateMaxScore.value = _selectedState.maxScore;
            _sliderStateFailChance.value = _selectedState.failChance;
            _textFieldStateNotes.value = _selectedState.notes;
            
            _floatFieldStateMinScore.style.display = _selectedState.setMinScore ? DisplayStyle.Flex : DisplayStyle.None;
            _floatFieldStateMaxScore.style.display = _selectedState.setMaxScore ? DisplayStyle.Flex : DisplayStyle.None;

            GeneratePreconditionListView();
            GenerateEvaluatorListView();
        }

        private void LoadStateExecutionView()
        {
            _executionSection.style.visibility = Visibility.Visible;

            _treeView.PopulateView(_selectedState.behaviourTree, _utilityDesigner, this);
        }

        private void Save()
        {
            string fileName = _textFieldFileName.value;

            string savePath = EditorUtility.SaveFilePanel(
                "Save Utility Behaviour", Application.dataPath, fileName, "asset");

            if (savePath.Length != 0)
            {
                if (savePath.StartsWith(Application.dataPath))
                {
                    savePath = "Assets" + savePath.Substring(Application.dataPath.Length);
                    _utilityDesigner.Save(savePath);
                    Close();
                    OpenWindow();
                }
                else
                    Debug.LogWarning("Invalid save location. Please choose a location within the project's Assets directory.");
            }
        }
        
        private void Load()
        {
            
            string loadPath = EditorUtility.OpenFilePanel(
                "Load Utility Behaviour", Application.dataPath, "asset");
            
            if (loadPath.Length != 0)
            {
                if (loadPath.StartsWith(Application.dataPath))
                {
                    if (_utilityDesigner.utilityBehaviour == null)
                    {
                        if (!EditorUtility.DisplayDialog("Confirmation",
                                "Are you sure you want to *OVERRIDE* everything?",
                                "Yes", "No"))
                            return;
                    }
                    
                    loadPath = "Assets" + loadPath.Substring(Application.dataPath.Length);
                    if (!_utilityDesigner.Load(loadPath))
                        return;
            
                    Close();
                    OpenWindow();
                }
                else
                    Debug.LogWarning("Invalid load location. Please choose a location within the project's Assets directory.");
            }
        }

        
        // ----------------------------------------------------------------------
        // -------------------- S T A T E - S E T -------------------------------

        private void LoadStateSetDropdown()
        {
            _menuStateSets.menu.ClearItems();
            _stateSetIdsOrdered.Clear();
            foreach (var keyValuePair in _utilityDesigner.GetStateSets())
            {
                _menuStateSets.menu.AppendAction(keyValuePair.Value.designation, a => LoadStateSet(keyValuePair.Key));
                _stateSetIdsOrdered.Add(keyValuePair.Key);
            }
        }

        private void LoadStateSet(int id)
        {
            _utilityDesigner.selectedStateSetId = id;
            _menuStateSets.text = _utilityDesigner.GetStateSets()[id].designation;
            _specificsContainer.style.visibility = Visibility.Hidden;
           _executionSection.style.visibility = Visibility.Hidden;
            _selectedState = null;

            if (_stateSetTab.childCount != 2)
                _stateSetTab.RemoveAt(1);

            GenerateStateListView();
        }

        private void AddStateSet()
        {
            int currentId = _utilityDesigner.NextStateSetId;
            string newStateSetName = Utils.VerifyItemName("State Set ",
                $"State Set {_utilityDesigner.GetStateSets().Count + 1}",
                _utilityDesigner.GetStateSets().Values,
                state => state.designation);
            _utilityDesigner.AddStateSet(currentId, new StateSet
            {
                designation = newStateSetName
            });
            _menuStateSets.menu.AppendAction(newStateSetName,
                a => LoadStateSet(currentId));
            _stateSetIdsOrdered.Add(currentId);

            LoadStateSet(currentId);
        }

        private void RemoveStateSet()
        {
            if (_menuStateSets.menu.MenuItems().Count <= 1)
                return;

            if (!EditorUtility.DisplayDialog("Confirmation",
                    "Are you sure you want to *DELETE* this state set?",
                    "Yes", "No"))
                return;
            
            _menuStateSets.menu.RemoveItemAt(_stateSetIdsOrdered.IndexOf(_utilityDesigner.selectedStateSetId));
            _utilityDesigner.RemoveStateSet(_utilityDesigner.selectedStateSetId);
            _stateSetIdsOrdered.Remove(_utilityDesigner.selectedStateSetId);

            LoadStateSet(_stateSetIdsOrdered.Last());
        }

        private void OpenStateSetRenameMenu()
        {
            if (_stateSetTab.childCount == 2)
            {
                TextField textFieldRenameStateSet = new TextField
                {
                    label = "New name",
                    value = _utilityDesigner.SelectedStateSet.designation,
                    maxLength = 16
                };
                textFieldRenameStateSet.RegisterValueChangedCallback(evt =>
                {
                    string newDesignation = Utils.VerifyItemName("State Set ",
                        evt.newValue,
                        _utilityDesigner.GetStateSets().Values,
                        state => state.designation,
                        _utilityDesigner.SelectedStateSet?.designation);
                    if (_utilityDesigner.SelectedStateSet != null)
                        _utilityDesigner.SelectedStateSet.designation = newDesignation;
                    _menuStateSets.text = newDesignation;
                    LoadStateSetDropdown();
                });

                _stateSetTab.Insert(1, textFieldRenameStateSet);
            }
            else
                _stateSetTab.RemoveAt(1);
        }


        // ----------------------------------------------------------------------
        // -------------------- S T A T E ---------------------------------------

        private void GenerateStateListView()
        {
            _statesContent.Clear();

            VisualElement MakeItem() => _templateItemState.Instantiate();

            void BindItem(VisualElement e, int i)
            {
                if (_stateContainersOrdered.ContainsKey(_utilityDesigner.SelectedStateSet.states[i]))
                    _stateContainersOrdered[_utilityDesigner.SelectedStateSet.states[i]] =
                        e.Q<VisualElement>("Container");
                else
                    _stateContainersOrdered.Add(_utilityDesigner.SelectedStateSet.states[i],
                        e.Q<VisualElement>("Container"));
                
                e.Q<Label>("LabelTitle").text = _utilityDesigner.SelectedStateSet.states[i].designation;
                e.Q<Button>("ButtonRemoveElement").clickable.clicked += () =>
                {
                    if (!EditorUtility.DisplayDialog("Confirmation",
                            "Are you sure you want to *DELETE* this state?",
                            "Yes", "No"))
                        return;
                    
                    _utilityDesigner.SelectedStateSet.states.RemoveAt(i);
                    if (_utilityDesigner.SelectedStateSet.states.Count <= 0)
                    {
                        _specificsContainer.style.visibility = Visibility.Hidden;
                        _executionSection.style.visibility = Visibility.Hidden;
                        _selectedState = null;
                    }
                    else
                    {
                        int index = _utilityDesigner.SelectedStateSet.states.Count > i ? i : i - 1;
                        LoadState(new List<State> { _utilityDesigner.SelectedStateSet.states[index] });
                        _statesListView.SetSelection(index);
                    }

                    _statesListView.Rebuild();
                };
            }

            _statesListView =
                new ListView(_utilityDesigner.SelectedStateSet.states, ItemStateHeight, MakeItem, BindItem)
                {
                    selectionType = SelectionType.Single
                };

            _statesListView.selectionChanged += LoadState;
            _statesContent.Add(_statesListView);
        }

        private void LoadState(IEnumerable<object> selectedItems)
        {
            _selectedState = (State)selectedItems.First();

            if (_evaluationSection.style.display == DisplayStyle.Flex)
                LoadStateEvaluationView();
            else
                LoadStateExecutionView();

            _utilityDesigner.lastSelectedStateIndex = _utilityDesigner.SelectedStateSet.states.IndexOf(_selectedState);
        }

        private void AddState()
        {
            _utilityDesigner.SelectedStateSet.states.Add(new State
            {
                active = true,
                designation = Utils.VerifyItemName("State ",
                    $"State {_utilityDesigner.SelectedStateSet.states.Count + 1}",
                    _utilityDesigner.SelectedStateSet.states,
                    state => state.designation),
                weight = 1f,
                executionFactor = 1f,
                baseScore = 0f,
                setMinScore = false,
                minScore = 0f,
                setMaxScore = false,
                maxScore = 0f,
                failChance = 0f,
                notes = ""
            });
            
            if (_utilityDesigner.SelectedStateSet.states.Count == 1)
            {
                if (_utilityDesigner.lastSelectedExecution)
                    _executionSection.style.visibility = Visibility.Visible;
                else
                    _specificsContainer.style.visibility = Visibility.Visible;
            }

            _statesListView.style.height = _utilityDesigner.SelectedStateSet.states.Count * ItemStateHeight;
            _statesListView.SetSelection(_utilityDesigner.SelectedStateSet.states.Count - 1);
            _statesListView.Rebuild();
        }


        // ----------------------------------------------------------------------
        // -------------------- P R E C O N D I T I O N S -----------------------

        private void GeneratePreconditionListView()
        {
            _preconditionsContent.Clear();

            VisualElement MakeItem() => _templateItemPrecondition.Instantiate();

            void BindItem(VisualElement e, int i)
            {
                if (_selectedState == null)
                    return;

                if (_preconditionContainersOrdered.ContainsKey(_selectedState.preconditions[i]))
                    _preconditionContainersOrdered[_selectedState.preconditions[i]] =
                        e.Q<VisualElement>("Container");
                else
                    _preconditionContainersOrdered.Add(_selectedState.preconditions[i],
                        e.Q<VisualElement>("Container"));

                Label labelTitle = e.Q<Label>("LabelTitle");
                labelTitle.text = _selectedState.preconditions[i].designation;
                
                TextField textFieldRename = e.Q<TextField>("TextFieldRename");
                textFieldRename.RegisterValueChangedCallback(evt =>
                {
                    _selectedState.preconditions[i].designation = Utils.VerifyItemName("Precondition ",
                        evt.newValue,
                        _selectedState.preconditions,
                        precondition => precondition.designation,
                        _selectedState.preconditions[i].designation);
                    _preconditionContainersOrdered[_selectedState.preconditions[i]].Q<Label>("LabelTitle").text =
                        _selectedState.preconditions[i].designation;
                });

                e.Q<Button>("ButtonRemoveElement").clickable.clicked += () =>
                {
                    _selectedState.preconditions.RemoveAt(i);
                    _preconditionsListView.Rebuild();
                };
                
                e.Q<Button>("ButtonRename").clickable.clicked += () =>
                {
                    if (labelTitle.style.display != DisplayStyle.None)
                    {
                        textFieldRename.value = _selectedState.preconditions[i].designation;
                        DeselectRenamingPreconditionsAndEvaluators();
                        labelTitle.style.display = DisplayStyle.None;
                        textFieldRename.style.display = DisplayStyle.Flex;
                        _preconditionLastUsedLabelTitle = labelTitle;
                        _preconditionLastUsedTextFieldRename = textFieldRename;
                    }
                    else
                    {
                        textFieldRename.style.display = DisplayStyle.None;
                        labelTitle.style.display = DisplayStyle.Flex;
                    }
                };
                InitializePreconditionDropdownForConsiderations(e.Q<DropdownField>("DropdownConsideration"), i);

                EnumField enumFieldComparator = e.Q<EnumField>("EnumFieldComparator");
                enumFieldComparator.SetValueWithoutNotify(_selectedState.preconditions[i].comparator);
                enumFieldComparator.RegisterValueChangedCallback(evt =>
                    _selectedState.preconditions[i].comparator = (Comparator)evt.newValue);

                FloatField floatFieldValue = e.Q<FloatField>("FloatFieldValue");
                floatFieldValue.SetValueWithoutNotify(_selectedState.preconditions[i].value);
                floatFieldValue.RegisterValueChangedCallback(evt =>
                    _selectedState.preconditions[i].value = evt.newValue);
            }

            _preconditionsListView =
                new ListView(_selectedState.preconditions, ItemPreconditionHeight, MakeItem, BindItem)
                {
                    selectionType = SelectionType.None
                };

            _preconditionsContent.Add(_preconditionsListView);
        }

        private void AddPrecondition()
        {
            _selectedState.preconditions.Add(new Precondition
            {
                designation = Utils.VerifyItemName("Precondition ",
                    $"Precondition {_selectedState.preconditions.Count + 1}",
                    _selectedState.preconditions,
                    precondition => precondition.designation)
            });

            _preconditionsListView.style.height = _selectedState.preconditions.Count * ItemPreconditionHeight;
            _preconditionsListView.Rebuild();
        }

        private void InitializePreconditionDropdownForConsiderations(DropdownField dropdownConsiderations, int index)
        {
            dropdownConsiderations.choices = _utilityDesigner.considerationSets
                .SelectMany(cs => cs.considerations
                    .Select(consideration => $"{cs.name} | {consideration.designation}"))
                .ToList();
            dropdownConsiderations.RegisterValueChangedCallback(evt =>
                _selectedState.preconditions[index].considerationDesignation = evt.newValue
            );
            dropdownConsiderations.SetValueWithoutNotify(
                dropdownConsiderations.choices.Contains(_selectedState.preconditions[index].considerationDesignation)
                    ? $"{_selectedState.preconditions[index].considerationDesignation}"
                    : "");
        }


        // ----------------------------------------------------------------------
        // -------------------- E V A L U A T O R S -----------------------------

        private void GenerateEvaluatorListView()
        {
            _evaluatorsContent.Clear();

            VisualElement MakeItem() => _templateItemEvaluator.Instantiate();

            void BindItem(VisualElement e, int i)
            {
                if (_selectedState == null)
                    return;

                if (_evaluatorContainersOrdered.ContainsKey(_selectedState.evaluators[i]))
                    _evaluatorContainersOrdered[_selectedState.evaluators[i]] = e.Q<VisualElement>("Container");
                else
                    _evaluatorContainersOrdered.Add(_selectedState.evaluators[i], e.Q<VisualElement>("Container"));

                Label labelTitle = e.Q<Label>("LabelTitle");
                labelTitle.text = _selectedState.evaluators[i].designation;
                
                TextField textFieldRename = e.Q<TextField>("TextFieldRename");
                textFieldRename.RegisterValueChangedCallback(evt =>
                {
                    _selectedState.evaluators[i].designation = Utils.VerifyItemName("Evaluator ",
                        evt.newValue,
                        _selectedState.evaluators,
                        evaluator => evaluator.designation,
                        _selectedState.evaluators[i].designation);
                    _evaluatorContainersOrdered[_selectedState.evaluators[i]].Q<Label>("LabelTitle").text =
                        _selectedState.evaluators[i].designation;
                });

                e.Q<Button>("ButtonRemoveElement").clickable.clicked += () =>
                {
                    _selectedState.evaluators.RemoveAt(i);
                    _evaluatorsListView.Rebuild();
                };
                
                e.Q<Button>("ButtonRename").clickable.clicked += () =>
                {
                    if (labelTitle.style.display != DisplayStyle.None)
                    {
                        textFieldRename.value = _selectedState.evaluators[i].designation;
                        DeselectRenamingPreconditionsAndEvaluators();
                        labelTitle.style.display = DisplayStyle.None;
                        textFieldRename.style.display = DisplayStyle.Flex;
                        _evaluatorLastUsedLabelTitle = labelTitle;
                        _evaluatorLastUsedTextFieldRename = textFieldRename;
                    }
                    else
                    {
                        textFieldRename.style.display = DisplayStyle.None;
                        labelTitle.style.display = DisplayStyle.Flex;
                    }
                };
                InitializeEvaluatorDropdownForConsiderations(e.Q<DropdownField>("DropdownConsideration"), i);

                CurveField curveEvaluation = e.Q<CurveField>("CurveEvaluation");
                curveEvaluation.SetValueWithoutNotify(_selectedState.evaluators[i].curve);
                curveEvaluation.RegisterValueChangedCallback(evt =>
                    _selectedState.evaluators[i].curve = evt.newValue);
                
                Slider sliderWeight = e.Q<Slider>("SliderWeight");
                sliderWeight.SetValueWithoutNotify(_selectedState.evaluators[i].weight);
                sliderWeight.RegisterValueChangedCallback(evt =>
                {
                    _selectedState.evaluators[i].weight = evt.newValue;
                    DisplayEvaluatorsWeightPercentage();
                });
                TextField inputField = sliderWeight.Q<TextField>();
                inputField.RegisterValueChangedCallback(evt =>
                {
                    if (!float.TryParse(evt.newValue, out float newValue))
                        return;
                    
                    _selectedState.evaluators[i].weight = newValue;
                    DisplayEvaluatorsWeightPercentage();
                });
                DisplayEvaluatorsWeightPercentage();

                FloatField floatFieldCurveMinValue = e.Q<FloatField>("FloatFieldCurveMinValue");
                floatFieldCurveMinValue.SetValueWithoutNotify(_selectedState.evaluators[i].curveMinValue);
                floatFieldCurveMinValue.RegisterValueChangedCallback(evt =>
                {
                    float value = Mathf.Clamp(evt.newValue, float.MinValue, _selectedState.evaluators[i].curveMaxValue);
                    floatFieldCurveMinValue.value = value;
                    _selectedState.evaluators[i].curveMinValue = value;
                });
                
                FloatField floatFieldCurveMaxValue = e.Q<FloatField>("FloatFieldCurveMaxValue");
                floatFieldCurveMaxValue.SetValueWithoutNotify(_selectedState.evaluators[i].curveMaxValue);
                floatFieldCurveMaxValue.RegisterValueChangedCallback(evt =>
                {
                    float value = Mathf.Clamp(evt.newValue, _selectedState.evaluators[i].curveMinValue, float.MaxValue);
                    floatFieldCurveMaxValue.value = value;
                    _selectedState.evaluators[i].curveMaxValue = value;
                });
            }

            _evaluatorsListView = new ListView(_selectedState.evaluators, ItemEvaluatorHeight, MakeItem, BindItem)
            {
                selectionType = SelectionType.None
            };

            _evaluatorsContent.Add(_evaluatorsListView);
        }

        private void AddEvaluator()
        {
            _selectedState.evaluators.Add(new Evaluator
            {
                designation = Utils.VerifyItemName("Evaluator ",
                    $"Evaluator {_selectedState.evaluators.Count + 1}",
                    _selectedState.evaluators,
                    evaluator => evaluator.designation),
                curveMinValue = 0,
                curveMaxValue = 1
            });

            _evaluatorsListView.style.height = _selectedState.evaluators.Count * ItemEvaluatorHeight;
            _evaluatorsListView.Rebuild();
        }

        private void DisplayEvaluatorsWeightPercentage()
        {
            float weightsTotal = _selectedState.evaluators.Sum(evaluator => evaluator.weight);
            foreach (var keyValuePair in _evaluatorContainersOrdered)
                keyValuePair.Value.Q<Slider>("SliderWeight").label = weightsTotal <= 0
                    ? "Weight <b><i>[0.0%]</i></b>"
                    : $"Weight <b><i>[{keyValuePair.Key.weight / weightsTotal * 100:F1}%]</i></b>";
        }

        private void InitializeEvaluatorDropdownForConsiderations(DropdownField dropdownConsiderations, int index)
        {
            dropdownConsiderations.choices = _utilityDesigner.considerationSets
                .SelectMany(cs => cs.considerations
                    .Select(consideration => $"{cs.name} | {consideration.designation}"))
                .ToList();
            dropdownConsiderations.RegisterValueChangedCallback(evt =>
                _selectedState.evaluators[index].considerationDesignation = evt.newValue
            );
            dropdownConsiderations.SetValueWithoutNotify(
                dropdownConsiderations.choices.Contains(_selectedState.evaluators[index].considerationDesignation)
                    ? $"{_selectedState.evaluators[index].considerationDesignation}"
                    : "");
        }


        // -------------------- H E L P E R -------------------------------------

        private void DeselectRenamingPreconditionsAndEvaluators()
        {
            if (_preconditionLastUsedLabelTitle != null)
                _preconditionLastUsedLabelTitle.style.display = DisplayStyle.Flex;
            if (_preconditionLastUsedTextFieldRename != null)
                _preconditionLastUsedTextFieldRename.style.display = DisplayStyle.None;
            if (_evaluatorLastUsedLabelTitle != null)
                _evaluatorLastUsedLabelTitle.style.display = DisplayStyle.Flex;
            if (_evaluatorLastUsedTextFieldRename != null)
                _evaluatorLastUsedTextFieldRename.style.display = DisplayStyle.None;
        }


        // ----------------------------------------------------------------------
        // -------------------- C O N S I D E R A T I O N - S E T ---------------

        private void GenerateConsiderationSetListView()
        {
            _considerationSetsContent.Clear();

            VisualElement MakeItem() => _templateItemConsiderationSet.Instantiate();

            void BindItem(VisualElement e, int i)
            {
                e.Q<Label>("LabelTitle").text = _utilityDesigner.considerationSets[i].name;
                e.Q<Button>("ButtonRemoveElement").clickable.clicked += () =>
                {
                    _utilityDesigner.considerationSets.RemoveAt(i);
                    if (_utilityDesigner.considerationSets.Count <= 0)
                        _considerationsContent.style.visibility = Visibility.Hidden;
                    else
                    {
                        int index = _utilityDesigner.considerationSets.Count > i ? i : i - 1;
                        LoadConsiderationSet(new List<ConsiderationSet> { _utilityDesigner.considerationSets[index] });
                        _considerationSetsListView.SetSelection(index);
                    }

                    _considerationSetsListView.Rebuild();
                    OnConsiderationSetEdited();
                };
                e.Q<Button>("ButtonEditConsiderationSet").clickable.clicked += () =>
                {
                    ConsiderationSetEditorWindow.OpenAsset(_utilityDesigner.considerationSets[i],
                        _selectedConsiderationSet, this);
                };
            }

            _considerationSetsListView =
                new ListView(_utilityDesigner.considerationSets, ItemConsiderationSetHeight, MakeItem, BindItem)
                {
                    selectionType = SelectionType.Single
                };

            _considerationSetsListView.selectionChanged += LoadConsiderationSet;
            _considerationSetsContent.Add(_considerationSetsListView);
        }

        private void LoadConsiderationSet(IEnumerable<object> selectedItems)
        {
            _selectedConsiderationSet = (ConsiderationSet)selectedItems.First();

            GenerateConsiderationListView();

            _considerationsContent.style.visibility = Visibility.Visible;
        }

        private void AddConsiderationSet()
        {
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.ShowObjectPicker<ConsiderationSet>(null, false, "", controlID);
        }

        private void CheckForNewConsiderationSetEvent()
        {
            if (Event.current.commandName != "ObjectSelectorClosed")
                return;

            // Only continues at the exact tick the ObjectPicker is being closed

            ConsiderationSet selectedConsiderationSet =
                EditorGUIUtility.GetObjectPickerObject() as ConsiderationSet;
            if (selectedConsiderationSet == null ||
                _utilityDesigner.considerationSets.Contains(selectedConsiderationSet))
                return;

            _utilityDesigner.considerationSets.Add(selectedConsiderationSet);
            _considerationSetsListView.SetSelection(_utilityDesigner.considerationSets.Count - 1);
            _considerationSetsListView.Rebuild();

            OnConsiderationSetEdited(selectedConsiderationSet);
        }

        internal void OnConsiderationSetEdited(ConsiderationSet considerationSet = null)
        {
            if (considerationSet != null)
                LoadConsiderationSet(new List<ConsiderationSet> { considerationSet });

            if (_selectedState == null)
                return;

            for (int i = 0; i < _selectedState.preconditions.Count; i++)
                InitializePreconditionDropdownForConsiderations(
                    _preconditionContainersOrdered[_selectedState.preconditions[i]]
                        .Q<DropdownField>("DropdownConsideration"), i);

            for (int i = 0; i < _selectedState.evaluators.Count; i++)
                InitializeEvaluatorDropdownForConsiderations(
                    _evaluatorContainersOrdered[_selectedState.evaluators[i]]
                        .Q<DropdownField>("DropdownConsideration"), i);
        }


        // ----------------------------------------------------------------------
        // -------------------- C O N S I D E R A T I O N S ---------------------

        private void GenerateConsiderationListView()
        {
            _considerationsContent.Clear();

            VisualElement MakeItem() => _templateItemConsideration.Instantiate();

            void BindItem(VisualElement e, int i)
            {
                if (_considerationContainersOrdered.ContainsKey(_selectedConsiderationSet.considerations[i]))
                    _considerationContainersOrdered[_selectedConsiderationSet.considerations[i]] = e.Q<VisualElement>("Container");
                else
                    _considerationContainersOrdered.Add(_selectedConsiderationSet.considerations[i], e.Q<VisualElement>("Container"));
                
                Consideration selectedConsideration = _selectedConsiderationSet.considerations[i];
                e.Q<Label>("LabelDesignationValue").text = selectedConsideration.designation;
                e.Q<Label>("LabelTypeValue").text = selectedConsideration.type.ToString();
                e.Q<Label>("LabelInitialValueValue").text = $"{selectedConsideration.initialValue:F2}";
                e.Q<Label>("LabelChangePerSecondValue").text = $"{selectedConsideration.changePerSecond:F2}";
            }

            _considerationsListView =
                new ListView(_selectedConsiderationSet.considerations, ItemConsiderationHeight, MakeItem, BindItem)
                {
                    selectionType = SelectionType.None
                };

            _considerationsContent.Add(_considerationsListView);
        }


        // ----------------------------------------------------------------------
        // -------------------- E X E C U T I O N -------------------------------

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void AddNode()
        {
            _treeView.CreateContextualMenu(new Vector2(0, 0));
        }

        private void CopyGraph()
        {
            _utilityDesigner.behaviourTreeRootNodeCache = (Root)_selectedState.behaviourTree.rootNode.Clone();
        }

        private void PasteGraph()
        {
            if (_utilityDesigner.behaviourTreeRootNodeCache == null)
                return;
            
            if (!EditorUtility.DisplayDialog("Confirmation",
                    "Are you sure you want to *REPLACE* the whole behaviour tree?",
                    "Yes", "No"))
                return;
            
            _selectedState.behaviourTree.rootNode = (Root)_utilityDesigner.behaviourTreeRootNodeCache.Clone();
            _treeView.PopulateView(_selectedState.behaviourTree, _utilityDesigner, this);
        }

        internal void CopyNode()
        {
            if (_selectedNodeView.node is not Root)
                _utilityDesigner.behaviourTreeNodeCache = (BaseNode)_selectedNodeView?.node.Clone();
        }

        internal void PasteNode()
        {
            if (_utilityDesigner.behaviourTreeNodeCache != null)
                _treeView.PopulateViewWithSubtree((BaseNode)_utilityDesigner.behaviourTreeNodeCache.Clone());
        }

        private void OnPlayModeChanged(PlayModeStateChange obj)
        {
            if (obj != PlayModeStateChange.EnteredEditMode)
                return;
            
            Close();
            if (Selection.activeGameObject != null &&
                Selection.activeGameObject.GetComponent<global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner>() != null)
                OpenWindow();
        }
        private void OnInspectorUpdate()
        {
            if (!Application.isPlaying)
                return;
            
            float highestScore = _utilityDesigner.SelectedStateSet.states.Select(state =>
                state.lastScore).Prepend(0f).Max();

            foreach (var state in _utilityDesigner.SelectedStateSet.states.Where(state => _stateContainersOrdered.ContainsKey(state)))
            {
                _stateContainersOrdered[state].Q<Label>("LabelScoreTotal").style.visibility =
                    Visibility.Visible;
                _stateContainersOrdered[state].Q<Label>("LabelScoreTotalValue").style.visibility =
                    Visibility.Visible;
                _stateContainersOrdered[state].Q<VisualElement>("ContainerScoreDisplay").style.visibility =
                    Visibility.Visible;

                _stateContainersOrdered[state].Q<Label>("LabelScoreTotalValue").text =
                    state.lastScore.ToString("F3", CultureInfo.InvariantCulture);
                _stateContainersOrdered[state].Q<VisualElement>("ScoreDisplay").style.width =
                    Length.Percent(state.lastScore / highestScore * 100f);

                _stateContainersOrdered[state].style.backgroundColor =
                    state.preconditions.Any(precondition => !precondition.lastConditionMet)
                        ? new Color(0.4f, 0.1f, 0.1f)
                        : new Color(0, 0, 0, 0);
            }
            
            switch (_utilityDesigner.lastSelectedExecution)
            {
                case false:
                {
                    if (_selectedState != null)
                    {
                        foreach (var precondition in _selectedState.preconditions.Where(precondition =>
                                     _preconditionContainersOrdered.ContainsKey(precondition)))
                            _preconditionContainersOrdered[precondition].style.backgroundColor =
                                precondition.lastConditionMet
                                    ? new Color(0.1f, 0.4f, 0.1f)
                                    : new Color(0.4f, 0.1f, 0.1f);
                        
                        float totalWeights = _selectedState.evaluators.Sum(evaluator => evaluator.weight);
                        foreach (var evaluator in _selectedState.evaluators.Where(evaluator =>
                                     _evaluatorContainersOrdered.ContainsKey(evaluator)))
                        {
                            _evaluatorContainersOrdered[evaluator].Q<VisualElement>("Score").style.width =
                                Length.Percent(evaluator.lastScore * evaluator.weight / totalWeights * 100);
                        }
                    }

                    if (_selectedConsiderationSet != null)
                    {
                        for (int i = 0; i < _selectedConsiderationSet.considerations.Count; i++)
                        {
                            Consideration consideration = _selectedConsiderationSet.considerations[i];
                            if (_considerationContainersOrdered.ContainsKey(consideration))
                            {
                                _considerationContainersOrdered[consideration].Q<Label>("LabelRuntimeValue")
                                    .style.visibility = Visibility.Visible;
                                _considerationContainersOrdered[consideration].Q<Label>("LabelRuntimeValueValue")
                                    .style.visibility = Visibility.Visible;

                                if (_selectedConsiderationSet.local)
                                {
                                    _considerationContainersOrdered[consideration].Q<Label>("LabelRuntimeValueValue").text =
                                        _utilityDesigner.localConsiderationSets[_selectedConsiderationSet].considerations[i]
                                            .Value.ToString("F2", CultureInfo.InvariantCulture);
                                }
                                else
                                {
                                    _considerationContainersOrdered[consideration].Q<Label>("LabelRuntimeValueValue").text =
                                        consideration.Value.ToString("F2", CultureInfo.InvariantCulture);
                                }
                            }
                        }

                        if (_selectedConsiderationSet.local)
                        {
                            var localConsiderations = _utilityDesigner.localConsiderationSets[_selectedConsiderationSet]
                                .considerations;
                            for (int i = 0; i < localConsiderations.Count; i++)
                            {
                                Consideration consideration = localConsiderations[i];
                                if (_considerationContainersOrdered.ContainsKey(consideration))
                                {
                                    _considerationContainersOrdered[consideration].Q<Label>("LabelRuntimeValueValue")
                                            .text = consideration.Value.ToString("F2", CultureInfo.InvariantCulture);
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < _selectedConsiderationSet.considerations.Count; i++)
                            {
                                Consideration consideration = _selectedConsiderationSet.considerations[i];
                                if (_considerationContainersOrdered.ContainsKey(consideration))
                                {
                                    _considerationContainersOrdered[consideration].Q<Label>("LabelRuntimeValueValue")
                                            .text = consideration.Value.ToString("F2", CultureInfo.InvariantCulture);
                                }
                            }
                        }
                    }

                    break;
                }
                case true:
                    _treeView.UpdateNodeStates();
                    break;
            }
        }

        private void OnNodeSelectionChanged(NodeView nodeView)
        {
            _inspectorContent.Clear();
            _labelNodeDescription.text = "";
            _selectedNodeView = nodeView;

            if (nodeView == null)
                return;

            if (nodeView.node.Description != null)
                _labelNodeDescription.text = $"\"{nodeView.node.Description}\"";
            DrawFields(nodeView.node);
        }
        
        private void DrawFields(object obj)
        {
            if (obj == null)
            {
                var helpBox = new HelpBox("Action is not assigned.", HelpBoxMessageType.Warning);
                _inspectorContent.Add(helpBox);
                return;
            }

            if (obj is BaseNode node)
                node.InitializedDropdowns(_inspectorContent);

            Type objType = obj.GetType();
            FieldInfo[] fields = objType.GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                if (obj is BaseNode && field.Name == "sceneReferences")
                    continue;

                object fieldValue = field.GetValue(obj);
                Type fieldType = field.FieldType;
                string displayName = Utils.CamelCaseToReadable(field.Name);

                if (fieldType == typeof(int))
                {
                    var fieldElement = new IntegerField(displayName);
                    fieldElement.SetValueWithoutNotify((int)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);
                }
                else if (fieldType == typeof(float))
                {
                    var fieldElement = new FloatField(displayName);
                    fieldElement.SetValueWithoutNotify((float)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);
                }
                else if (fieldType == typeof(double))
                {
                    var fieldElement = new DoubleField(displayName);
                    fieldElement.SetValueWithoutNotify((double)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);
                }
                else if (fieldType == typeof(string))
                {
                    bool isDescriptionFieldInBaseNode = obj is BaseNode && field.Name == "notes";
                    TextField fieldElement;

                    if (isDescriptionFieldInBaseNode)
                    {
                        if (_inspectorContent.childCount != 0)
                        {
                            var spacer = new VisualElement
                            {
                                style =
                                {
                                    height = 15
                                }
                            };
                            _inspectorContent.Add(spacer);
                        }

                        fieldElement = new TextField(displayName)
                        {
                            multiline = true,
                            style =
                            {
                                height = 70,
                                whiteSpace = WhiteSpace.Normal
                            }
                        };
                    }
                    else
                        fieldElement = new TextField(displayName);

                    fieldElement.SetValueWithoutNotify((string)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);

                }
                else if (fieldType == typeof(bool))
                {
                    var fieldElement = new Toggle(displayName);
                    fieldElement.SetValueWithoutNotify((bool)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);
                }
                else if (fieldType.IsEnum)
                {
                    var fieldElement = new EnumField(displayName, (Enum)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);
                }
                else if (fieldType == typeof(Vector2))
                {
                    var fieldElement = new Vector2Field(displayName);
                    fieldElement.SetValueWithoutNotify((Vector2)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);
                }
                else if (fieldType == typeof(Vector3))
                {
                    var fieldElement = new Vector3Field(displayName);
                    fieldElement.SetValueWithoutNotify((Vector3)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);
                }
                else if (fieldType == typeof(Vector4))
                {
                    var fieldElement = new Vector4Field(displayName);
                    fieldElement.SetValueWithoutNotify((Vector4)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);
                }
                else if (fieldType == typeof(Vector2Int))
                {
                    var fieldElement = new Vector2IntField(displayName);
                    fieldElement.SetValueWithoutNotify((Vector2Int)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);
                }
                else if (fieldType == typeof(Vector3Int))
                {
                    var fieldElement = new Vector3IntField(displayName);
                    fieldElement.SetValueWithoutNotify((Vector3Int)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);
                }
                else if (fieldType == typeof(Color))
                {
                    var fieldElement = new ColorField(displayName);
                    fieldElement.SetValueWithoutNotify((Color)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);
                }
                else if (fieldType == typeof(Rect))
                {
                    var fieldElement = new RectField(displayName);
                    fieldElement.SetValueWithoutNotify((Rect)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);
                }
                else if (fieldType == typeof(Bounds))
                {
                    var fieldElement = new BoundsField(displayName);
                    fieldElement.SetValueWithoutNotify((Bounds)fieldValue);
                    fieldElement.RegisterValueChangedCallback(evt => field.SetValue(obj, evt.newValue));
                    _inspectorContent.Add(fieldElement);
                }
                else if (fieldType == typeof(Quaternion))
                {
                    var fieldElement = new Vector3Field($"{displayName} (Euler Angles)");
                    Quaternion quaternionValue = (Quaternion)fieldValue;
                    Vector3 eulerAngles = quaternionValue.eulerAngles;
                    fieldElement.SetValueWithoutNotify(eulerAngles);
                    fieldElement.RegisterValueChangedCallback(evt =>
                    {
                        Quaternion newValue = Quaternion.Euler(evt.newValue);
                        field.SetValue(obj, newValue);
                    });
                    _inspectorContent.Add(fieldElement);
                }
                else if (!fieldType.IsPrimitive && !fieldType.IsEnum &&
                         !typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
                {
                    var foldout = new Foldout { text = $"{displayName} ({fieldType.Name})" };
                    var container = new VisualElement();
                    foldout.Add(container);
                    foldout.RegisterValueChangedCallback(evt =>
                    {
                        if (evt.newValue)
                            DrawFields(fieldValue);
                        else
                            container.Clear();
                    });
                    _inspectorContent.Add(foldout);
                }
                else
                {
                    var unsupportedLabel = new Label($"{displayName}: Type '{fieldType.Name}' is not supported");
                    _inspectorContent.Add(unsupportedLabel);
                }
            }
        }
    }
}
#endif