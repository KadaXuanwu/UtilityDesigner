using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using KadaXuanwu.UtilityDesigner.Core;
using KadaXuanwu.UtilityDesigner.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Designer = KadaXuanwu.UtilityDesigner.Runtime.UtilityDesigner;
using Node = KadaXuanwu.UtilityDesigner.Core.Node;

namespace KadaXuanwu.UtilityDesigner.Editor {
    // The Utility Designer window, for the selected GameObject's UtilityDesigner. Outside play it edits the behaviour's
    // JSON, saved whenever the window loses focus or closes and before play starts. In play it shows the running agent:
    // every state's score, the considerations' values, the nodes' states; changes made then are not saved. In play it can
    // also show an agent the game offers (OfferedAgents), read only.
    public class UtilityDesignerEditorWindow : EditorWindow {
        private static readonly Vector2 WindowMinSize = new Vector2(1350, 600);
        private const float ItemStateHeight = 80;
        private const float ItemPreconditionHeight = 130;
        private const float ItemEvaluatorHeight = 185;
        private const float ItemConsiderationSetHeight = 70;
        private const float ItemConsiderationHeight = 85;
        // Tells the object picker that adds a consideration set from any other.
        private const int PickerId = 515300;
        private static readonly Color Met = new Color(0.1f, 0.4f, 0.1f);
        private static readonly Color Unmet = new Color(0.4f, 0.1f, 0.1f);

        // The designs of components without a behaviour file yet, until the editor's code is reloaded.
        private static readonly Dictionary<Designer, Design> Unsaved = new Dictionary<Designer, Design>();
        private static string _treeClipboard;
        private static string _nodeClipboard;

        [SerializeField] private int stateSetIndex;
        [SerializeField] private int stateIndex;
        [SerializeField] private bool execution;

        private Designer _designer;
        // The agent shown in play: the component's, or the offered one's.
        private Agent _agent;
        private OfferedAgents.Offer _offer;
        private int _offersVersion = -1;
        private Design _design;
        // The file the design was read from: where it is saved.
        private TextAsset _loadedFrom;
        private State _selectedState;
        private ConsiderationSet _selectedConsiderationSet;
        private NodeView _selectedNodeView;

        private VisualElement _container;
        private VisualElement _specificsContainer;
        private VisualElement _evaluationSection;
        private VisualElement _executionSection;
        private VisualElement _stateSetTab;
        private Button _buttonEvaluation;
        private Button _buttonExecution;
        private TextField _textFieldFileName;
        private ToolbarMenu _menuStateSets;
        private Toggle _toggleStateActive;
        private TextField _textFieldStateName;
        private FloatField _floatFieldStateWeight;
        private FloatField _floatFieldStateExecutionFactor;
        private FloatField _floatFieldStateBaseScore;
        private Toggle _toggleStateSetMinScore;
        private FloatField _floatFieldStateMinScore;
        private Toggle _toggleStateSetMaxScore;
        private FloatField _floatFieldStateMaxScore;
        private Slider _sliderStateFailChance;
        private TextField _textFieldStateNotes;
        private ListView _statesListView;
        private ListView _preconditionsListView;
        private ListView _evaluatorsListView;
        private ListView _considerationSetsListView;
        private ListView _considerationsListView;
        private BehaviourTreeView _treeView;
        private Label _decisions;
        private VisualElement _inspectorContent;
        private Label _labelNodeDescription;
        private ToolbarMenu _menuAgents;

        internal SceneReferences SceneReferences { get; private set; }
        // An offered agent may tick on another thread: the window shows it, and changes nothing.
        internal bool ReadOnly => _offer != null;

        private StateSet StateSet => _design.StateSets[Mathf.Clamp(stateSetIndex, 0, _design.StateSets.Count - 1)];

        internal static void OpenWindow() {
            UtilityDesignerEditorWindow window = GetWindow<UtilityDesignerEditorWindow>();
            window.titleContent = new GUIContent("Utility Designer");
            window.minSize = WindowMinSize;
        }

        private void OnEnable() {
            // A design made here names its nodes when it is written.
            Nodes.Register();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable() {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            SaveLinked();
        }

        private void OnLostFocus() => SaveLinked();

        private void OnFocus() {
            // Another behaviour given in the inspector meanwhile.
            if (_container != null && _designer != null && !Application.isPlaying && _designer.Behaviour != _loadedFrom) {
                Target(_designer);
            }
        }

        private void OnSelectionChange() {
            Designer selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Designer>() : null;
            if (_container != null && selected != null && selected != _designer) {
                Target(selected);
            }
        }

        private void OnPlayModeChanged(PlayModeStateChange change) {
            if (change == PlayModeStateChange.ExitingEditMode) {
                SaveLinked();
            } else if (_container != null && (change == PlayModeStateChange.EnteredPlayMode || change == PlayModeStateChange.EnteredEditMode)) {
                _offersVersion = -1;
                _menuAgents.style.display = DisplayStyle.None;
                Designer selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Designer>() : null;
                Target(selected != null ? selected : _designer);
            }
        }

        private void CreateGUI() {
            VisualElement root = rootVisualElement;
            string directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
            VisualTreeAsset Template(string name) => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{directory}/UXML/{name}.uxml");
            Template("UtilityDesigner").CloneTree(root);
            // Outside the container, which hides while nothing is shown.
            _menuAgents = new ToolbarMenu { text = "Offered agents", tooltip = "The agents the game offers: pick one to show it." };
            _menuAgents.style.display = DisplayStyle.None;
            Toolbar offered = new Toolbar();
            offered.Add(_menuAgents);
            root.Insert(0, offered);

            _container = root.Q<VisualElement>("Container");
            _evaluationSection = root.Q<VisualElement>("EvaluationSection");
            _executionSection = root.Q<VisualElement>("ExecutionSection");
            _specificsContainer = root.Q<VisualElement>("SpecificsContainer");
            _stateSetTab = root.Q<VisualElement>("StateSetsTab");
            _buttonEvaluation = root.Q<Button>("ButtonEvaluation");
            _buttonExecution = root.Q<Button>("ButtonExecution");
            _textFieldFileName = root.Q<TextField>("TextFieldFileName");
            _menuStateSets = root.Q<ToolbarMenu>("MenuStateSets");
            _toggleStateActive = root.Q<Toggle>("ToggleStateActive");
            _textFieldStateName = root.Q<TextField>("TextFieldStateName");
            _floatFieldStateWeight = root.Q<FloatField>("FloatFieldStateWeight");
            _floatFieldStateExecutionFactor = root.Q<FloatField>("FloatFieldStateExecutionFactor");
            _floatFieldStateBaseScore = root.Q<FloatField>("FloatFieldStateBaseScore");
            _toggleStateSetMinScore = root.Q<Toggle>("ToggleStateSetMinScore");
            _floatFieldStateMinScore = root.Q<FloatField>("FloatFieldStateMinScore");
            _toggleStateSetMaxScore = root.Q<Toggle>("ToggleStateSetMaxScore");
            _floatFieldStateMaxScore = root.Q<FloatField>("FloatFieldStateMaxScore");
            _sliderStateFailChance = root.Q<Slider>("SliderStateFailChance");
            _textFieldStateNotes = root.Q<TextField>("TextFieldStateNotes");
            _inspectorContent = root.Q<VisualElement>("ExecutionInspectorContent");
            _labelNodeDescription = root.Q<Label>("LabelNodeDescription");
            _treeView = root.Q<BehaviourTreeView>();
            _treeView.onNodeSelected = OnNodeSelectionChanged;

            _buttonEvaluation.clicked += () => SelectTab(false);
            _buttonExecution.clicked += () => SelectTab(true);
            root.Q<Button>("ButtonSave").clicked += SaveAs;
            root.Q<Button>("ButtonLoad").clicked += Load;
            root.Q<Button>("ButtonAddStateSet").clicked += AddStateSet;
            root.Q<Button>("ButtonRemoveStateSet").clicked += RemoveStateSet;
            root.Q<Button>("ButtonRenameStateSet").clicked += ToggleStateSetRename;
            root.Q<Button>("ButtonAddState").clicked += AddState;
            root.Q<Button>("ButtonAddPrecondition").clicked += AddPrecondition;
            root.Q<Button>("ButtonAddEvaluator").clicked += AddEvaluator;
            root.Q<Button>("ButtonAddConsiderationSet").clicked += PickConsiderationSet;
            root.Q<Button>("ButtonAddNode").clicked += () => _treeView.ShowCreateMenu(Vector2.zero);
            root.Q<Button>("ButtonCopyBT").clicked += CopyGraph;
            root.Q<Button>("ButtonPasteBT").clicked += PasteGraph;

            RegisterStateInspector();
            _statesListView = StatesList(Template("TemplateItemState"));
            root.Q<VisualElement>("StatesContent").Add(_statesListView);
            _decisions = new Label { name = "LabelDecisions", tooltip = "The agent's last decisions: when it took up a state, its score, and the best state it left." };
            _decisions.style.flexShrink = 0;
            _decisions.style.whiteSpace = WhiteSpace.Normal;
            _decisions.style.marginLeft = _decisions.style.marginBottom = 5;
            root.Q<VisualElement>("StatesContainer").Add(_decisions);
            _preconditionsListView = PreconditionsList(Template("TemplateItemPrecondition"));
            root.Q<VisualElement>("PreconditionsContent").Add(_preconditionsListView);
            _evaluatorsListView = EvaluatorsList(Template("TemplateItemEvaluator"));
            root.Q<VisualElement>("EvaluatorsContent").Add(_evaluatorsListView);
            _considerationSetsListView = ConsiderationSetsList(Template("TemplateItemConsiderationSet"));
            root.Q<VisualElement>("ConsiderationSetsContent").Add(_considerationSetsListView);
            _considerationsListView = ConsiderationsList(Template("TemplateItemConsideration"));
            root.Q<VisualElement>("ConsiderationsContent").Add(_considerationsListView);

            Target(Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Designer>() : null);
        }


        // ----------------------------------------------------------------------
        // -------------------- T H E   D E S I G N -----------------------------

        // Shows a component's design: its behaviour file, or the running agent's in play. Or, in play, an offered agent's.
        private void Target(Designer designer, OfferedAgents.Offer offer = null) {
            SaveLinked();
            _designer = designer;
            _offer = offer;
            _agent = offer != null ? offer.Agent : Application.isPlaying && designer != null ? designer.Agent : null;
            _design = null;
            _loadedFrom = null;
            _selectedState = null;
            _selectedConsiderationSet = null;
            if (designer != null || offer != null) {
                LoadDesign();
            }
            _container.style.display = _design != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (_design == null) {
                return;
            }
            SceneReferences = offer != null ? Designer.Of(offer.Agent)?.SceneReferences
                : Application.isPlaying ? designer.SceneReferences : designer.FindSceneReferences();
            if (SceneReferences != null && !Application.isPlaying) {
                SceneReferences.Initialize();
            }
            _textFieldFileName.value = offer != null ? offer.Name : _loadedFrom != null ? _loadedFrom.name : _design.Name;
            rootVisualElement.Q<Button>("ButtonSave").SetEnabled(!Application.isPlaying);
            rootVisualElement.Q<Button>("ButtonLoad").SetEnabled(!Application.isPlaying);
            foreach (string button in new[] { "ButtonRenameStateSet", "ButtonAddStateSet", "ButtonRemoveStateSet", "ButtonAddState", "ButtonAddConsiderationSet" }) {
                rootVisualElement.Q<Button>(button).SetEnabled(!ReadOnly);
            }
            _specificsContainer.SetEnabled(!ReadOnly);
            _inspectorContent.SetEnabled(!ReadOnly);
            _statesListView.reorderable = !ReadOnly;
            _decisions.style.display = Application.isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
            SelectStateSet(stateSetIndex, stateIndex);
            RefreshConsiderationSets();
        }

        // The last decisions a trace kept, newest first: when the agent took up another state, that state's score, and
        // the best of the states it could have chosen instead.
        internal static string Decisions(Trace trace, int count) {
            List<string> lines = new List<string>();
            for (int i = 0; i < trace.Count; i++) {
                Trace.Entry entry = trace[i];
                // The oldest entry of a full trace is no decision: what ran before it is gone.
                if (i == 0 ? trace.Count == trace.Capacity : entry.Executing == trace[i - 1].Executing) {
                    continue;
                }
                string line = $"{entry.Time.ToString("F1", CultureInfo.InvariantCulture)} s  {(entry.Executing.Length > 0 ? entry.Executing : "nothing")}";
                Trace.Option? left = null;
                foreach (Trace.Option option in entry.Options) {
                    if (option.State == entry.Executing) {
                        line += $" {option.Score.ToString("F3", CultureInfo.InvariantCulture)}";
                    } else if (option.Available && (left == null || option.Score > left.Value.Score)) {
                        left = option;
                    }
                }
                lines.Add(left == null ? line : $"{line}  over {left.Value.State} {left.Value.Score.ToString("F3", CultureInfo.InvariantCulture)}");
            }
            lines.Reverse();
            return string.Join("\n", lines.Take(count));
        }

        private void LoadDesign() {
            if (Application.isPlaying) {
                _design = _agent?.Design;
                // The window turns the trace on, so an agent nobody looks at keeps none; several minutes of evaluations.
                if (_agent != null) {
                    lock (Gate) {
                        _agent.Trace ??= new Trace(4096);
                    }
                }
                return;
            }
            if (_designer.Behaviour == null) {
                if (!Unsaved.TryGetValue(_designer, out _design)) {
                    Unsaved[_designer] = _design = new Design();
                }
            } else {
                try {
                    _design = Nodes.Load(_designer.Behaviour.text);
                } catch (Exception e) {
                    Debug.LogError($"{AssetDatabase.GetAssetPath(_designer.Behaviour)} is not a behaviour this Utility Designer reads: {e.Message}", _designer);
                    return;
                }
                _loadedFrom = _designer.Behaviour;
                Behaviours.Sync(_design);
            }
            if (_design.StateSets.Count == 0) {
                _design.StateSets.Add(new StateSet { Name = "State Set 1" });
            }
        }

        private object Gate => _offer?.Gate ?? this;

        private void SaveLinked() {
            if (!Application.isPlaying && _design != null && _loadedFrom != null) {
                Behaviours.Write(AssetDatabase.GetAssetPath(_loadedFrom), _design);
            }
        }

        private void SaveAs() {
            string path = EditorUtility.SaveFilePanel("Save Utility Behaviour", Application.dataPath, _textFieldFileName.value, "json");
            if (path.Length == 0) {
                return;
            }
            if (!path.StartsWith(Application.dataPath)) {
                Debug.LogWarning("Invalid save location. Please choose a location within the project's Assets directory.");
                return;
            }
            path = "Assets" + path.Substring(Application.dataPath.Length);
            _design.Name = Path.GetFileNameWithoutExtension(path);
            File.WriteAllText(path, Designs.ToJson(_design));
            AssetDatabase.ImportAsset(path);
            Link(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
        }

        private void Load() {
            string path = EditorUtility.OpenFilePanel("Load Utility Behaviour", Application.dataPath, "json");
            if (path.Length == 0) {
                return;
            }
            if (!path.StartsWith(Application.dataPath)) {
                Debug.LogWarning("Invalid load location. Please choose a location within the project's Assets directory.");
                return;
            }
            if (_loadedFrom == null && !EditorUtility.DisplayDialog("Confirmation", "Are you sure you want to *OVERRIDE* everything?", "Yes", "No")) {
                return;
            }
            Link(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets" + path.Substring(Application.dataPath.Length)));
        }

        private void Link(TextAsset behaviour) {
            if (behaviour == null) {
                return;
            }
            Undo.RecordObject(_designer, "Utility Behaviour");
            _designer.Behaviour = behaviour;
            EditorUtility.SetDirty(_designer);
            Unsaved.Remove(_designer);
            // Nothing to save back: the file was just written, or is about to be read.
            _loadedFrom = null;
            Target(_designer);
        }


        // ----------------------------------------------------------------------
        // -------------------- S T A T E   S E T S -----------------------------

        private void SelectStateSet(int index, int state = 0) {
            stateSetIndex = Mathf.Clamp(index, 0, _design.StateSets.Count - 1);
            _menuStateSets.menu.ClearItems();
            for (int i = 0; i < _design.StateSets.Count; i++) {
                int at = i;
                _menuStateSets.menu.AppendAction(_design.StateSets[i].Name, _ => SelectStateSet(at));
            }
            _menuStateSets.text = StateSet.Name;
            if (_stateSetTab.childCount != 2) {
                _stateSetTab.RemoveAt(1);
            }
            _statesListView.itemsSource = StateSet.States;
            _statesListView.Rebuild();
            _statesListView.ClearSelection();
            SelectState(null);
            if (StateSet.States.Count > 0) {
                _statesListView.SetSelection(Mathf.Clamp(state, 0, StateSet.States.Count - 1));
            }
            SelectTab(execution);
        }

        private void AddStateSet() {
            _design.StateSets.Add(new StateSet {
                Name = Utils.VerifyItemName("State Set ", $"State Set {_design.StateSets.Count + 1}", _design.StateSets, set => set.Name),
            });
            SelectStateSet(_design.StateSets.Count - 1);
        }

        private void RemoveStateSet() {
            if (_design.StateSets.Count <= 1
                || !EditorUtility.DisplayDialog("Confirmation", "Are you sure you want to *DELETE* this state set?", "Yes", "No")) {
                return;
            }
            _design.StateSets.Remove(StateSet);
            SelectStateSet(_design.StateSets.Count - 1);
        }

        private void ToggleStateSetRename() {
            if (_stateSetTab.childCount != 2) {
                _stateSetTab.RemoveAt(1);
                return;
            }
            TextField rename = new TextField { label = "New name", value = StateSet.Name, maxLength = 16 };
            rename.RegisterValueChangedCallback(evt => {
                StateSet.Name = Utils.VerifyItemName("State Set ", evt.newValue, _design.StateSets, set => set.Name, StateSet.Name);
                _menuStateSets.text = StateSet.Name;
                _menuStateSets.menu.ClearItems();
                for (int i = 0; i < _design.StateSets.Count; i++) {
                    int at = i;
                    _menuStateSets.menu.AppendAction(_design.StateSets[i].Name, _ => SelectStateSet(at));
                }
            });
            _stateSetTab.Insert(1, rename);
        }


        // ----------------------------------------------------------------------
        // -------------------- S T A T E S -------------------------------------

        private ListView StatesList(VisualTreeAsset template) {
            ListView list = new ListView(null, ItemStateHeight, () => {
                VisualElement item = template.Instantiate();
                item.Q<Button>("ButtonRemoveElement").clicked += () => {
                    if (!EditorUtility.DisplayDialog("Confirmation", "Are you sure you want to *DELETE* this state?", "Yes", "No")) {
                        return;
                    }
                    int index = (int)item.userData;
                    StateSet.States.RemoveAt(index);
                    _statesListView.Rebuild();
                    _statesListView.ClearSelection();
                    SelectState(null);
                    if (StateSet.States.Count > 0) {
                        _statesListView.SetSelection(Mathf.Min(index, StateSet.States.Count - 1));
                    }
                };
                return item;
            }, (item, i) => {
                item.userData = i;
                item.Q<Label>("LabelTitle").text = StateSet.States[i].Name;
                item.Q<Button>("ButtonRemoveElement").SetEnabled(!ReadOnly);
            }) { selectionType = SelectionType.Single, reorderable = true };
            list.unbindItem = (item, _) => item.userData = -1;
            list.selectionChanged += selected => SelectState((State)selected.FirstOrDefault());
            // The states are told apart by their place: a tie goes to the upper one.
            list.itemIndexChanged += (_, to) => stateIndex = to;
            return list;
        }

        private void SelectState(State state) {
            _selectedState = state;
            _specificsContainer.style.visibility = state != null ? Visibility.Visible : Visibility.Hidden;
            _executionSection.style.visibility = state != null ? Visibility.Visible : Visibility.Hidden;
            _preconditionsListView.itemsSource = state?.Preconditions;
            _preconditionsListView.Rebuild();
            _evaluatorsListView.itemsSource = state?.Evaluators;
            _evaluatorsListView.Rebuild();
            if (state != null) {
                stateIndex = StateSet.States.IndexOf(state);
                _toggleStateActive.SetValueWithoutNotify(state.Active);
                _textFieldStateName.SetValueWithoutNotify(state.Name);
                _floatFieldStateWeight.SetValueWithoutNotify(state.Weight);
                _floatFieldStateExecutionFactor.SetValueWithoutNotify(state.ExecutionFactor);
                _floatFieldStateBaseScore.SetValueWithoutNotify(state.BaseScore);
                _toggleStateSetMinScore.SetValueWithoutNotify(state.SetMinScore);
                _floatFieldStateMinScore.SetValueWithoutNotify(state.MinScore);
                _toggleStateSetMaxScore.SetValueWithoutNotify(state.SetMaxScore);
                _floatFieldStateMaxScore.SetValueWithoutNotify(state.MaxScore);
                _sliderStateFailChance.SetValueWithoutNotify(state.FailChance);
                _textFieldStateNotes.SetValueWithoutNotify(state.Notes);
                _floatFieldStateMinScore.style.display = state.SetMinScore ? DisplayStyle.Flex : DisplayStyle.None;
                _floatFieldStateMaxScore.style.display = state.SetMaxScore ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (execution) {
                _treeView.PopulateView(state, this);
            }
        }

        private void AddState() {
            StateSet.States.Add(new State {
                Name = Utils.VerifyItemName("State ", $"State {StateSet.States.Count + 1}", StateSet.States, state => state.Name),
            });
            _statesListView.Rebuild();
            _statesListView.SetSelection(StateSet.States.Count - 1);
        }

        private void RegisterStateInspector() {
            _toggleStateActive.RegisterValueChangedCallback(evt => _selectedState.Active = evt.newValue);
            _textFieldStateName.RegisterValueChangedCallback(evt => {
                _selectedState.Name = Utils.VerifyItemName("State ", evt.newValue, StateSet.States, state => state.Name, _selectedState.Name);
                _statesListView.RefreshItems();
            });
            _floatFieldStateWeight.RegisterValueChangedCallback(evt => {
                _selectedState.Weight = Mathf.Max(0f, evt.newValue);
                _floatFieldStateWeight.SetValueWithoutNotify(_selectedState.Weight);
            });
            _floatFieldStateExecutionFactor.RegisterValueChangedCallback(evt => {
                _selectedState.ExecutionFactor = Mathf.Max(0f, evt.newValue);
                _floatFieldStateExecutionFactor.SetValueWithoutNotify(_selectedState.ExecutionFactor);
            });
            _floatFieldStateBaseScore.RegisterValueChangedCallback(evt => _selectedState.BaseScore = evt.newValue);
            _toggleStateSetMinScore.RegisterValueChangedCallback(evt => {
                _selectedState.SetMinScore = evt.newValue;
                _floatFieldStateMinScore.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            _floatFieldStateMinScore.RegisterValueChangedCallback(evt => {
                _selectedState.MinScore = Mathf.Max(0f, evt.newValue);
                _floatFieldStateMinScore.SetValueWithoutNotify(_selectedState.MinScore);
            });
            _toggleStateSetMaxScore.RegisterValueChangedCallback(evt => {
                _selectedState.SetMaxScore = evt.newValue;
                _floatFieldStateMaxScore.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            _floatFieldStateMaxScore.RegisterValueChangedCallback(evt => {
                _selectedState.MaxScore = Mathf.Max(0f, evt.newValue);
                _floatFieldStateMaxScore.SetValueWithoutNotify(_selectedState.MaxScore);
            });
            _sliderStateFailChance.RegisterValueChangedCallback(evt => _selectedState.FailChance = evt.newValue);
            _textFieldStateNotes.RegisterValueChangedCallback(evt => _selectedState.Notes = evt.newValue);
        }

        private void SelectTab(bool executionTab) {
            execution = executionTab;
            _evaluationSection.style.display = executionTab ? DisplayStyle.None : DisplayStyle.Flex;
            _executionSection.style.display = executionTab ? DisplayStyle.Flex : DisplayStyle.None;
            StyleColor chosen = new StyleColor(new Color32(0x4D, 0x4D, 0x4D, 0xFF));
            _buttonEvaluation.style.backgroundColor = executionTab ? StyleKeyword.Null : chosen;
            _buttonExecution.style.backgroundColor = executionTab ? chosen : StyleKeyword.Null;
            if (executionTab) {
                _treeView.PopulateView(_selectedState, this);
            }
        }


        // ----------------------------------------------------------------------
        // -------------------- P R E C O N D I T I O N S -----------------------

        private List<string> ConsiderationChoices() =>
            _design.ConsiderationSets.SelectMany(set => set.Considerations.Select(consideration => $"{set.Name} | {consideration.Name}")).ToList();

        // The N button of a precondition or evaluator: its title becomes a text field, and back.
        private static void RegisterRename(VisualElement item, Func<string> get, Func<string, string> rename) {
            Label title = item.Q<Label>("LabelTitle");
            TextField field = item.Q<TextField>("TextFieldRename");
            field.RegisterValueChangedCallback(evt => title.text = rename(evt.newValue));
            item.Q<Button>("ButtonRename").clicked += () => {
                bool renaming = field.style.display == DisplayStyle.Flex;
                field.SetValueWithoutNotify(get());
                field.style.display = renaming ? DisplayStyle.None : DisplayStyle.Flex;
                title.style.display = renaming ? DisplayStyle.Flex : DisplayStyle.None;
            };
        }

        private static void BindTitle(VisualElement item, string name) {
            item.Q<Label>("LabelTitle").text = name;
            item.Q<Label>("LabelTitle").style.display = DisplayStyle.Flex;
            item.Q<TextField>("TextFieldRename").style.display = DisplayStyle.None;
        }

        private ListView PreconditionsList(VisualTreeAsset template) {
            ListView list = new ListView(null, ItemPreconditionHeight, () => {
                VisualElement item = template.Instantiate();
                Precondition Bound() => _selectedState.Preconditions[(int)item.userData];
                RegisterRename(item, () => Bound().Name, name => Bound().Name =
                    Utils.VerifyItemName("Precondition ", name, _selectedState.Preconditions, precondition => precondition.Name, Bound().Name));
                item.Q<Button>("ButtonRemoveElement").clicked += () => {
                    _selectedState.Preconditions.RemoveAt((int)item.userData);
                    _preconditionsListView.Rebuild();
                };
                item.Q<DropdownField>("DropdownConsideration").RegisterValueChangedCallback(evt => Bound().Consideration = evt.newValue ?? "");
                item.Q<EnumField>("EnumFieldComparator").RegisterValueChangedCallback(evt => Bound().Comparator = (Comparator)evt.newValue);
                item.Q<FloatField>("FloatFieldValue").RegisterValueChangedCallback(evt => Bound().Value = evt.newValue);
                return item;
            }, (item, i) => {
                Precondition precondition = _selectedState.Preconditions[i];
                item.userData = i;
                BindTitle(item, precondition.Name);
                DropdownField consideration = item.Q<DropdownField>("DropdownConsideration");
                consideration.choices = ConsiderationChoices();
                consideration.SetValueWithoutNotify(consideration.choices.Contains(precondition.Consideration) ? precondition.Consideration : "");
                item.Q<EnumField>("EnumFieldComparator").Init(precondition.Comparator);
                item.Q<FloatField>("FloatFieldValue").SetValueWithoutNotify(precondition.Value);
                item.Q<VisualElement>("Container").style.backgroundColor = StyleKeyword.Null;
            }) { selectionType = SelectionType.None };
            list.unbindItem = (item, _) => item.userData = -1;
            return list;
        }

        private void AddPrecondition() {
            _selectedState.Preconditions.Add(new Precondition {
                Name = Utils.VerifyItemName("Precondition ", $"Precondition {_selectedState.Preconditions.Count + 1}", _selectedState.Preconditions,
                    precondition => precondition.Name),
            });
            _preconditionsListView.Rebuild();
        }


        // ----------------------------------------------------------------------
        // -------------------- E V A L U A T O R S -----------------------------

        private ListView EvaluatorsList(VisualTreeAsset template) {
            ListView list = new ListView(null, ItemEvaluatorHeight, () => {
                VisualElement item = template.Instantiate();
                Evaluator Bound() => _selectedState.Evaluators[(int)item.userData];
                RegisterRename(item, () => Bound().Name, name => Bound().Name =
                    Utils.VerifyItemName("Evaluator ", name, _selectedState.Evaluators, evaluator => evaluator.Name, Bound().Name));
                item.Q<Button>("ButtonRemoveElement").clicked += () => {
                    _selectedState.Evaluators.RemoveAt((int)item.userData);
                    _evaluatorsListView.Rebuild();
                };
                item.Q<DropdownField>("DropdownConsideration").RegisterValueChangedCallback(evt => Bound().Consideration = evt.newValue ?? "");
                item.Q<Slider>("SliderWeight").RegisterValueChangedCallback(evt => {
                    Bound().Weight = evt.newValue;
                    _evaluatorsListView.Query<Slider>("SliderWeight").ForEach(ShowWeightShare);
                });
                item.Q<EnumField>("EnumFieldShape").RegisterValueChangedCallback(evt => {
                    Bound().Curve.Shape = (CurveShape)evt.newValue;
                    ShowCurve(item, Bound().Curve);
                });
                item.Q<CurveField>("CurveEvaluation").RegisterValueChangedCallback(evt => {
                    if (Bound().Curve.Shape == CurveShape.Keys) {
                        Bound().Curve.Keys = Utils.ToKeys(evt.newValue);
                    }
                });
                item.Q<FloatField>("FloatFieldM").RegisterValueChangedCallback(evt => ShapeNumber(item, Bound().Curve, curve => curve.M = evt.newValue));
                item.Q<FloatField>("FloatFieldK").RegisterValueChangedCallback(evt => ShapeNumber(item, Bound().Curve, curve => curve.K = evt.newValue));
                item.Q<FloatField>("FloatFieldB").RegisterValueChangedCallback(evt => ShapeNumber(item, Bound().Curve, curve => curve.B = evt.newValue));
                item.Q<FloatField>("FloatFieldC").RegisterValueChangedCallback(evt => ShapeNumber(item, Bound().Curve, curve => curve.C = evt.newValue));
                FloatField min = item.Q<FloatField>("FloatFieldCurveMinValue");
                FloatField max = item.Q<FloatField>("FloatFieldCurveMaxValue");
                min.RegisterValueChangedCallback(evt => {
                    Bound().Min = Mathf.Min(evt.newValue, Bound().Max);
                    min.SetValueWithoutNotify(Bound().Min);
                });
                max.RegisterValueChangedCallback(evt => {
                    Bound().Max = Mathf.Max(evt.newValue, Bound().Min);
                    max.SetValueWithoutNotify(Bound().Max);
                });
                return item;
            }, (item, i) => {
                Evaluator evaluator = _selectedState.Evaluators[i];
                item.userData = i;
                BindTitle(item, evaluator.Name);
                DropdownField consideration = item.Q<DropdownField>("DropdownConsideration");
                consideration.choices = ConsiderationChoices();
                consideration.SetValueWithoutNotify(consideration.choices.Contains(evaluator.Consideration) ? evaluator.Consideration : "");
                Slider weight = item.Q<Slider>("SliderWeight");
                weight.SetValueWithoutNotify(evaluator.Weight);
                ShowWeightShare(weight);
                item.Q<EnumField>("EnumFieldShape").Init(evaluator.Curve.Shape);
                ShowCurve(item, evaluator.Curve);
                item.Q<FloatField>("FloatFieldCurveMinValue").SetValueWithoutNotify(evaluator.Min);
                item.Q<FloatField>("FloatFieldCurveMaxValue").SetValueWithoutNotify(evaluator.Max);
                item.Q<VisualElement>("Score").style.width = 0;
            }) { selectionType = SelectionType.None };
            list.unbindItem = (item, _) => item.userData = -1;
            return list;
        }

        // A Keys curve is drawn by hand in the curve field; any other shape is its four numbers, the field showing it.
        private static void ShowCurve(VisualElement item, Curve curve) {
            bool keys = curve.Shape == CurveShape.Keys;
            item.Q<VisualElement>("ShapeNumbers").style.display = keys ? DisplayStyle.None : DisplayStyle.Flex;
            item.Q<FloatField>("FloatFieldM").SetValueWithoutNotify(curve.M);
            item.Q<FloatField>("FloatFieldK").SetValueWithoutNotify(curve.K);
            item.Q<FloatField>("FloatFieldB").SetValueWithoutNotify(curve.B);
            item.Q<FloatField>("FloatFieldC").SetValueWithoutNotify(curve.C);
            CurveField field = item.Q<CurveField>("CurveEvaluation");
            field.SetValueWithoutNotify(Utils.ToAnimationCurve(curve));
            field.SetEnabled(keys);
        }

        private static void ShapeNumber(VisualElement item, Curve curve, Action<Curve> set) {
            set(curve);
            item.Q<CurveField>("CurveEvaluation").SetValueWithoutNotify(Utils.ToAnimationCurve(curve));
        }

        private void ShowWeightShare(Slider slider) {
            // Up from the slider to the row, which knows which evaluator it shows.
            VisualElement item = slider;
            while (item != null && !(item.userData is int)) {
                item = item.parent;
            }
            if (item == null || _selectedState == null || (int)item.userData < 0 || (int)item.userData >= _selectedState.Evaluators.Count) {
                return;
            }
            float total = _selectedState.Evaluators.Sum(evaluator => evaluator.Weight);
            float share = total <= 0f ? 0f : _selectedState.Evaluators[(int)item.userData].Weight / total * 100f;
            slider.label = $"Weight <b><i>[{share.ToString("F1", CultureInfo.InvariantCulture)}%]</i></b>";
        }

        private void AddEvaluator() {
            _selectedState.Evaluators.Add(new Evaluator {
                Name = Utils.VerifyItemName("Evaluator ", $"Evaluator {_selectedState.Evaluators.Count + 1}", _selectedState.Evaluators,
                    evaluator => evaluator.Name),
            });
            _evaluatorsListView.Rebuild();
        }


        // ----------------------------------------------------------------------
        // -------------------- C O N S I D E R A T I O N S ---------------------

        private ListView ConsiderationSetsList(VisualTreeAsset template) {
            ListView list = new ListView(null, ItemConsiderationSetHeight, () => {
                VisualElement item = template.Instantiate();
                item.Q<Button>("ButtonRemoveElement").clicked += () => {
                    _design.ConsiderationSets.RemoveAt((int)item.userData);
                    RefreshConsiderationSets();
                };
                item.Q<Button>("ButtonEditConsiderationSet").clicked += () => {
                    string name = _design.ConsiderationSets[(int)item.userData].Name;
                    ConsiderationSetAsset asset = Behaviours.Asset(name);
                    if (asset != null) {
                        ConsiderationSetEditorWindow.Open(asset);
                    } else {
                        EditorUtility.DisplayDialog("Consideration Set",
                            $"The project has no Consideration Set asset named '{name}': this set is only in the behaviour's file.", "OK");
                    }
                };
                return item;
            }, (item, i) => {
                item.userData = i;
                item.Q<Label>("LabelTitle").text = _design.ConsiderationSets[i].Name;
                item.Q<Button>("ButtonRemoveElement").SetEnabled(!ReadOnly);
            }) { selectionType = SelectionType.Single };
            list.selectionChanged += selected => {
                _selectedConsiderationSet = (ConsiderationSet)selected.FirstOrDefault();
                _considerationsListView.itemsSource = _selectedConsiderationSet?.Considerations;
                _considerationsListView.Rebuild();
            };
            return list;
        }

        private ListView ConsiderationsList(VisualTreeAsset template) {
            ListView list = new ListView(null, ItemConsiderationHeight, template.Instantiate, (item, i) => {
                Consideration consideration = _selectedConsiderationSet.Considerations[i];
                item.userData = i;
                item.Q<Label>("LabelDesignationValue").text = consideration.Name;
                item.Q<Label>("LabelInitialValueValue").text = consideration.InitialValue.ToString("F2", CultureInfo.InvariantCulture);
                item.Q<Label>("LabelChangePerSecondValue").text = consideration.ChangePerSecond.ToString("F2", CultureInfo.InvariantCulture);
            }) { selectionType = SelectionType.None };
            list.unbindItem = (item, _) => item.userData = -1;
            return list;
        }

        // The lists of the sets and their considerations again, and every dropdown that offers them.
        private void RefreshConsiderationSets() {
            int selected = Mathf.Max(0, _design.ConsiderationSets.IndexOf(_selectedConsiderationSet));
            _considerationSetsListView.itemsSource = _design.ConsiderationSets;
            _considerationSetsListView.Rebuild();
            _considerationSetsListView.ClearSelection();
            _selectedConsiderationSet = null;
            _considerationsListView.itemsSource = null;
            _considerationsListView.Rebuild();
            if (_design.ConsiderationSets.Count > 0) {
                _considerationSetsListView.SetSelection(Mathf.Min(selected, _design.ConsiderationSets.Count - 1));
            }
            if (_selectedState != null) {
                _preconditionsListView.RefreshItems();
                _evaluatorsListView.RefreshItems();
            }
        }

        private void PickConsiderationSet() {
            EditorGUIUtility.ShowObjectPicker<ConsiderationSetAsset>(null, false, "", PickerId);
        }

        private void OnGUI() {
            if (Event.current.commandName != "ObjectSelectorClosed" || EditorGUIUtility.GetObjectPickerControlID() != PickerId || _design == null) {
                return;
            }
            ConsiderationSetAsset picked = EditorGUIUtility.GetObjectPickerObject() as ConsiderationSetAsset;
            if (picked == null || _design.ConsiderationSets.Any(set => set.Name == picked.name)) {
                return;
            }
            _design.ConsiderationSets.Add(picked.ToCore());
            _selectedConsiderationSet = _design.ConsiderationSets[_design.ConsiderationSets.Count - 1];
            RefreshConsiderationSets();
        }

        // A consideration set asset was edited: the design's copies follow.
        internal void OnConsiderationSetEdited() {
            if (_design == null || Application.isPlaying) {
                return;
            }
            string selected = _selectedConsiderationSet?.Name;
            Behaviours.Sync(_design);
            _selectedConsiderationSet = _design.ConsiderationSets.Find(set => set.Name == selected);
            RefreshConsiderationSets();
            SaveLinked();
        }


        // ----------------------------------------------------------------------
        // -------------------- E X E C U T I O N -------------------------------

        private void CopyGraph() {
            if (_selectedState != null) {
                _treeClipboard = Utils.TreeJson(_selectedState.Tree);
            }
        }

        private void PasteGraph() {
            if (_treeClipboard == null || _selectedState == null || Application.isPlaying
                || !EditorUtility.DisplayDialog("Confirmation", "Are you sure you want to *REPLACE* the whole behaviour tree?", "Yes", "No")) {
                return;
            }
            _selectedState.Tree = Utils.TreeFromJson(_treeClipboard);
            _treeView.PopulateView(_selectedState, this);
        }

        internal void CopyNode() {
            if (_selectedNodeView != null && _selectedNodeView.node != null) {
                _nodeClipboard = Utils.TreeJson(_selectedNodeView.node);
            }
        }

        internal void PasteNode() {
            if (_nodeClipboard != null) {
                _treeView.AddCopy(Utils.TreeFromJson(_nodeClipboard));
            }
        }

        private void OnNodeSelectionChanged(NodeView nodeView) {
            _inspectorContent.Clear();
            _labelNodeDescription.text = "";
            _selectedNodeView = nodeView;
            if (nodeView == null) {
                return;
            }
            if (!string.IsNullOrEmpty(nodeView.Description)) {
                _labelNodeDescription.text = $"\"{nodeView.Description}\"";
            }
            if (nodeView.node != null) {
                DrawFields(nodeView);
            }
        }

        // The node inspector: every setting of the node, then its notes.
        private void DrawFields(NodeView view) {
            Node node = view.node;
            FieldInfo[] fields = NodeTypes.SettingsOf(node);
            if (SceneReferences == null && fields.Any(field => field.GetCustomAttribute<SceneRefAttribute>() != null)) {
                _inspectorContent.Add(new HelpBox("Scene References missing.", HelpBoxMessageType.Info));
            }
            foreach (FieldInfo field in fields) {
                string label = Utils.Readable(field.Name);
                Type type = field.FieldType;
                SceneRefAttribute sceneRef = field.GetCustomAttribute<SceneRefAttribute>();
                if (sceneRef != null && type == typeof(int)) {
                    if (SceneReferences != null) {
                        DrawSceneRef(view, field, sceneRef);
                    }
                } else if (node is ChangeStateSet && field.Name == nameof(ChangeStateSet.StateSetIndex)) {
                    DrawIndex(view, field, "State Set", _design.StateSets.Select(set => set.Name).ToList());
                } else if (type == typeof(int)) {
                    AddField(new IntegerField(label), view, field);
                } else if (type == typeof(float)) {
                    AddField(new FloatField(label), view, field);
                } else if (type == typeof(double)) {
                    AddField(new DoubleField(label), view, field);
                } else if (type == typeof(string)) {
                    AddField(new TextField(label), view, field);
                } else if (type == typeof(bool)) {
                    AddField(new Toggle(label), view, field);
                } else if (type.IsEnum) {
                    AddField(new EnumField(label, (Enum)field.GetValue(node)), view, field);
                } else if (type == typeof(Vector2)) {
                    AddField(new Vector2Field(label), view, field);
                } else if (type == typeof(Vector3)) {
                    AddField(new Vector3Field(label), view, field);
                } else if (type == typeof(Vector4)) {
                    AddField(new Vector4Field(label), view, field);
                } else if (type == typeof(Vector2Int)) {
                    AddField(new Vector2IntField(label), view, field);
                } else if (type == typeof(Vector3Int)) {
                    AddField(new Vector3IntField(label), view, field);
                } else if (type == typeof(Color)) {
                    AddField(new ColorField(label), view, field);
                } else if (type == typeof(Rect)) {
                    AddField(new RectField(label), view, field);
                } else if (type == typeof(Bounds)) {
                    AddField(new BoundsField(label), view, field);
                } else if (type == typeof(Quaternion)) {
                    Vector3Field euler = new Vector3Field($"{label} (Euler Angles)");
                    euler.SetValueWithoutNotify(((Quaternion)field.GetValue(node)).eulerAngles);
                    euler.RegisterValueChangedCallback(evt => {
                        field.SetValue(node, Quaternion.Euler(evt.newValue));
                        view.UpdateDetails();
                    });
                    _inspectorContent.Add(euler);
                } else {
                    _inspectorContent.Add(new Label($"{label}: Type '{type.Name}' is not supported"));
                }
            }
            if (_inspectorContent.childCount != 0) {
                _inspectorContent.Add(new VisualElement { style = { height = 15 } });
            }
            TextField notes = new TextField("Notes") { multiline = true, style = { height = 70, whiteSpace = WhiteSpace.Normal } };
            notes.SetValueWithoutNotify(node.Notes);
            notes.RegisterValueChangedCallback(evt => node.Notes = evt.newValue ?? "");
            _inspectorContent.Add(notes);
        }

        private void AddField<T>(BaseField<T> element, NodeView view, FieldInfo field) {
            element.SetValueWithoutNotify((T)field.GetValue(view.node));
            element.RegisterValueChangedCallback(evt => {
                field.SetValue(view.node, evt.newValue);
                view.UpdateDetails();
            });
            _inspectorContent.Add(element);
        }

        // An index into a SceneReferences list, picked by the name of what it points at.
        private void DrawSceneRef(NodeView view, FieldInfo field, SceneRefAttribute sceneRef) {
            IList list = SceneReferences.GetList(sceneRef.Type);
            if (list == null) {
                _inspectorContent.Add(new HelpBox($"Add a list of type <b>{sceneRef.Type.Name}</b> to SceneReferences", HelpBoxMessageType.Warning));
            } else if (list.Count == 0) {
                _inspectorContent.Add(new HelpBox($"The list of type <b>{sceneRef.Type.Name}</b> in SceneReferences is empty", HelpBoxMessageType.Info));
            } else {
                DrawIndex(view, field, sceneRef.Label, list.Cast<object>()
                    .Select(item => item is Component component && component != null ? component.gameObject.name : item != null ? item.ToString() : "None").ToList());
            }
        }

        private void DrawIndex(NodeView view, FieldInfo field, string label, List<string> names) {
            if (names.Count == 0) {
                return;
            }
            List<int> indices = Enumerable.Range(0, names.Count).ToList();
            PopupField<int> popup = new PopupField<int>(label, indices, Mathf.Clamp((int)field.GetValue(view.node), 0, names.Count - 1),
                index => names[index], index => names[index]);
            popup.RegisterValueChangedCallback(evt => {
                field.SetValue(view.node, evt.newValue);
                view.UpdateDetails();
            });
            _inspectorContent.Add(popup);
        }


        // ----------------------------------------------------------------------
        // -------------------- P L A Y -----------------------------------------

        private static IEnumerable<VisualElement> Rows(ListView list, int count) {
            return list.Query<VisualElement>("Container").ToList().Where(container => RowIndex(container) >= 0 && RowIndex(container) < count);
        }

        private static int RowIndex(VisualElement container) {
            for (VisualElement element = container; element != null; element = element.parent) {
                if (element.userData is int index) {
                    return index;
                }
            }
            return -1;
        }

        private void OnInspectorUpdate() {
            if (!Application.isPlaying || _container == null) {
                return;
            }
            RefreshOffers();
            if (_design == null || _agent == null || (_offer == null && _designer == null)) {
                return;
            }
            lock (Gate) {
                if (_agent.Trace != null) {
                    _decisions.text = Decisions(_agent.Trace, 8);
                }
                List<State> states = StateSet.States;
                float highest = states.Select(state => state.LastScore).Prepend(0f).Max();
                foreach (VisualElement row in Rows(_statesListView, states.Count)) {
                    State state = states[RowIndex(row)];
                    row.Q<Label>("LabelScoreTotal").style.visibility = Visibility.Visible;
                    row.Q<Label>("LabelScoreTotalValue").style.visibility = Visibility.Visible;
                    row.Q<VisualElement>("ContainerScoreDisplay").style.visibility = Visibility.Visible;
                    row.Q<Label>("LabelScoreTotalValue").text = state.LastScore.ToString("F3", CultureInfo.InvariantCulture);
                    row.Q<VisualElement>("ScoreDisplay").style.width = Length.Percent(highest > 0f ? Mathf.Clamp01(state.LastScore / highest) * 100f : 0f);
                    row.style.backgroundColor = state.Preconditions.Any(precondition => !precondition.LastConditionMet) ? Unmet : new Color(0, 0, 0, 0);
                }
                if (execution) {
                    _treeView.UpdateNodeStates();
                    return;
                }
                if (_selectedState != null) {
                    foreach (VisualElement row in Rows(_preconditionsListView, _selectedState.Preconditions.Count)) {
                        row.style.backgroundColor = _selectedState.Preconditions[RowIndex(row)].LastConditionMet ? Met : Unmet;
                    }
                    float total = _selectedState.Evaluators.Sum(evaluator => evaluator.Weight);
                    foreach (VisualElement row in Rows(_evaluatorsListView, _selectedState.Evaluators.Count)) {
                        Evaluator evaluator = _selectedState.Evaluators[RowIndex(row)];
                        row.Q<VisualElement>("Score").style.width =
                            Length.Percent(total > 0f ? Mathf.Clamp01(evaluator.LastScore * evaluator.Weight / total) * 100f : 0f);
                    }
                }
                if (_selectedConsiderationSet != null && _agent != null) {
                    foreach (VisualElement row in Rows(_considerationsListView, _selectedConsiderationSet.Considerations.Count)) {
                        string name = _selectedConsiderationSet.Considerations[RowIndex(row)].Name;
                        row.Q<Label>("LabelRuntimeValue").style.visibility = Visibility.Visible;
                        row.Q<Label>("LabelRuntimeValueValue").style.visibility = Visibility.Visible;
                        row.Q<Label>("LabelRuntimeValueValue").text =
                            _agent.GetConsideration(_selectedConsiderationSet.Name, name).ToString("F2", CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        // The menu of the offered agents again when the list changed; nothing shown when the shown one was taken back.
        private void RefreshOffers() {
            if (OfferedAgents.Version == _offersVersion) {
                return;
            }
            _offersVersion = OfferedAgents.Version;
            OfferedAgents.Offer[] offers = OfferedAgents.All();
            _menuAgents.menu.ClearItems();
            foreach (OfferedAgents.Offer offer in offers) {
                _menuAgents.menu.AppendAction(offer.Name, _ => Target(null, offer),
                    _ => offer == _offer ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            _menuAgents.style.display = offers.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_offer != null && !offers.Contains(_offer)) {
                Target(null);
            }
        }
    }
}
