# The v1 checklist

Utility Designer 2.0's gate: every feature, node and editor window of v1 (`Assets/KadaXuanwu/UtilityDesigner/Scripts`,
the thesis in `Doku/chapters`), and where 2.0 has it.

- **Core**: in this package, deciding as v1 does, with a test in `Tests~/CoreTests.cs`.
- **Unity layer**: needs UnityEngine or the editor; the next ticket.
- **Plumbing**: how it is referenced, stored, serialised or run. Free to differ from v1; the Change column says how.

2.0 ships when every Unity layer row is done. v1's logic is not changed anywhere; what looks wrong in it is listed at
the end, for discussion.

## Scoring and choosing

| v1 | Where | In 2.0 |
| -- | ----- | ------ |
| `State.GetScore`: `Weight · (BaseScore + Σ Score_i · EvalWeight_i / TotalEvalWeights)` | Core | `Agent.Score`; sums in double as LINQ's. `ScoringTests.TheFormula`, `EvaluatorWeightsCountByTheirShare` |
| No evaluator weight: the base score alone, evaluators not scored | Core | `WithoutEvaluatorWeightOnlyTheBaseScoreCounts` |
| Execution factor on the running state | Core | `TheRunningStateIsMultipliedByItsExecutionFactor` |
| Min and max score when set, min first, after the execution factor | Core | `TheScoreIsHeldToItsMinAndMaxWhenSet` |
| Active toggle: an inactive state scores -1 and keeps its last score | Core | `AnInactiveStateCannotBeChosenAndKeepsItsLastScore` |
| Preconditions gate a state; the first unmet one stops the check and zeroes the last score | Core | `AnUnmetPreconditionGatesTheStateAndZeroesItsLastScore` |
| Comparators: `>`, `<`, `>=`, `<=`; `=` and `!=` on both sides rounded to whole numbers | Core | `Comparing.Holds`; `Comparators` |
| Evaluator: a curve over a consideration, normalised between its min and max, clamped to 0..1 | Core | `AnEvaluatorNormalizesBetweenItsMinAndMax` |
| Evaluator max not above its min: divided by `float.Epsilon`, a step at the min | Core | `AMaxNotAboveTheMinIsAStepAtTheMin` |
| A precondition or evaluator whose consideration is missing: not met, scores 0 | Core | `APreconditionWithoutItsConsiderationIsNotMet`, `AnEvaluatorWithoutItsConsiderationScoresZero` |
| `StateSet.DecideNextState`: scores of 0 and above, best first, ties in list order | Core | `Agent.Decide`; `TheBestStateIsChosen`, `ATieGoesToTheFirstInTheSet`, `AScoreBelowZeroCannotBeChosenUnlessTheMinLiftsIt` |
| The running state is kept as soon as the order reaches it | Core | `TheRunningStateIsKeptWhenTheBetterOnesArePassedOver`, `TheExecutionFactorKeepsTheRunningStateAhead` |
| Fail chance: `random > failChance` takes the state, else the next is checked; all passed over, the last of them is taken | Core | `AFailChancePassesOverTheBestAndTheNextIsChecked`, `WhenEveryStateIsPassedOverTheLastOfThemIsTaken`, `OneRandomNumberIsDrawnForEachStateCheckedBeforeTheRunningOne` |
| `UtilityDesigner.Evaluate`: considerations move on, then the choice; nothing to choose ends the state even when locked | Core | `Agent.Evaluate`; `WhenNothingCanBeChosenTheStateEndsEvenLocked` |
| State lock: no other state takes over; unlocking evaluates at once | Core | `Agent.StateLocked`; `ALockKeepsTheStateAndUnlockingEvaluatesAtOnce`, `ALockedStateThatCanNoLongerBeChosenStaysWhileAnotherCanBe`, `UnlockingInsideATreeChoosesTheBetterStateAtOnce` |
| A switch terminates the old state's tree | Core | `SwitchingTerminatesTheRunningNodes` |
| A finished tree is not ticked again while its state stays chosen | Core | `AFinishedTreeLeavesTheAgentIdleUntilAnotherStateHasRun` |
| State sets; the first one runs at start; switching by index | Core | `Agent.SetRunningStateSet`; `ChangeStateSetSwitchesTheSetAtTheNextEvaluation` |
| State notes | Core | `State.Notes`, saved |

## Considerations

| v1 | Where | In 2.0 |
| -- | ----- | ------ |
| Initial value, optional min and max, change per second | Core | `Consideration`; `AValueIsHeldToItsLimitsWhenSet`, `ItChangesEachSecondOfTheCallersClock` |
| Global sets (shared by every agent) and local sets (each agent's own copy) | Core | `ConsiderationSet.Local`, `Globals`; `GlobalSetsAreSharedAndLocalOnesAreEachAgentsOwn` |
| An agent starting sets the global considerations back to their initial values | Core | `AStartingAgentSetsTheSharedConsiderationsBackAsV1` |
| `ConsiderationSet.Get/Set/ChangeConsideration` for game code: 0 or false when missing | Core | On `Agent` (by slot or by names) and `Globals.Find`; `AConsiderationThatIsNotThereReadsZeroAndCannotBeSet` |
| Reached by name each tick (`"Set \| Name"`, set name and consideration name in nodes) | Plumbing | Bound once to a slot index when the agent starts (`Agent.Bind`) |
| Consideration type (only Float) | Plumbing | Dropped: every consideration is a float |
| Use Real Time, on considerations and the time nodes | Plumbing | Dropped: the caller's clock is the only one (game time in Unity) |
| Consideration sets as ScriptableObject assets, with a create menu | Unity layer | |

## Nodes

Life cycle as v1's `BaseNode`: `OnAwake` once, `OnEnable` each run, `OnUpdate` each execution tick, `OnDisable` at
the end or when terminated; a composite or decorator terminates its children first
(`NodeTests.ANodeAwakesOnceAndIsEnabledEachRun`).

| v1 node | Where | Test (`NodeTests`) |
| ------- | ----- | ------------------ |
| Root | Core | internal to `State`; every test runs through it |
| Sequence | Core | `SequenceRunsOneChildATickUntilOneFails` |
| Selector | Core | `SelectorRunsOneChildATickUntilOneSucceeds` |
| Parallel | Core | `ParallelRunsEveryChildEachTickAndFailsWithTheFirstFailure` |
| Random Selector | Core | `RandomSelectorTriesEveryChildInASeededOrder` |
| Random Sequence | Core | `RandomSequenceRunsEveryChildOnceInASeededOrder` |
| Invert, Success, Failure | Core | `TheDecorators` |
| Repeat | Core | `RepeatRunsItsChildAgainWhateverItReturns` |
| Wait Fixed | Core | `WaitFixedSucceedsOnceItsDurationHasPassed` |
| Wait Random | Core | `WaitRandomWaitsASeededDurationBetweenItsMinAndMax` |
| Lock State, Unlock State | Core | `LockStateAndUnlockState` |
| Lock State Duration | Core | `LockStateDurationLocksForItsDuration` |
| Lock State Consideration | Core | `LockStateConsiderationLocksUntilTheValueIsReached`, `...CanAddTheValueItStartsFrom`, `...FailsWithoutItsSet` |
| Change State Set | Core | `ChangeStateSetSwitchesTheSetAtTheNextEvaluation` |
| Set Consideration, Change Consideration | Core | `SetConsiderationAndChangeConsideration` |
| Compare Consideration | Core | `CompareConsiderationComparesTheValueTwo`, `CompareConsiderationWithoutItsConsideration` |
| Time Check | Core | `TimeCheckComparesTheCallersClock` |
| Move To Position, Move To Transform | Unity layer | NavMeshAgent |
| Compare Distance | Unity layer | Transforms |
| Debug Log, Debug Breakpoint | Unity layer | `Debug` |
| Custom nodes: subclass `ActionNode`, `ConditionalNode`, `CompositeNode`, `DecoratorNode` | Core | `NodeTypes.Register`; the sample's `Remember` |
| A node reaches its `UtilityDesigner` and `ThisGameObject` | Plumbing | `Node.Agent`, and `Agent.Owner` for whatever the game passes |
| `SceneRefs` (scene object lists picked by index in dropdowns) | Unity layer | |
| `[CategoryPath]` for a custom node's place in the menu | Unity layer | |
| Node description, shown in the inspector | Core | `Node.Description`, v1's texts |
| Node guid, notes, graph position | Plumbing | `Node.Id`, `Notes`, `X`, `Y`, saved |
| Node fields serialised by Unity; `RegisterSerializedVariables`, `RegisterDropdowns` | Plumbing | A node type's public fields are its settings, saved by name; how the editor shows them is the Unity layer's |

Where v1 throws, the core does not: a composite without children and a decorator without a child fail, a Lock State
Consideration without its set fails, a state without a tree is simply not ticked.

## Running it

| v1 | Where | In 2.0 |
| -- | ----- | ------ |
| The `UtilityDesigner` component on a GameObject | Unity layer | Wraps an `Agent` |
| Evaluation and execution tick rates (0.01 to 1 s), or every Update | Unity layer | Calls `Agent.Evaluate(now)` and `Agent.Execute(now)` at those rates |
| Ticked on real time (`WaitForSecondsRealtime`) | Plumbing | The caller's clock; game time in Unity, so a paused game stops deciding |
| `UnityEngine.Random` and `System.Random` | Plumbing | One seeded `Rng` the caller passes in; a run repeats (`RunTests.TheSameSeedLivesTheSameDay`) |
| Each agent runs a copy of the behaviour asset (`UnlinkUtilityBehaviour`) | Plumbing | Each `Agent` owns a `Design`; `Designs.Copy`; a second agent on one design is refused |
| `UtilityBehaviour` ScriptableObject, save and load by file name | Plumbing | Versioned JSON (`Designs.ToJson`, `FromJson`), loadable headless. The asset wrapper and the Save and Load buttons are the Unity layer's |
| `AnimationCurve` | Plumbing | `Curve` keys: the same Hermite, unclamped, stepped keys kept (`CurveTests.KeysAreAnAnimationCurve`); or a parametric shape. Weighted tangents and wrap modes other than clamp are not kept: the Unity layer converts or refuses them |
| Log To File (`Assets/UtilityDesignerLog.txt`) and its tick rate | Core, Unity layer | `Agent.WriteLog` writes v1's entry (`RunTests.TheLogIsV1s`); the file and the rate are the Unity layer's |
| | Plumbing, new | A parameter registry and override layers (`Designs.Parameters`, `Layer`, `Difference`), a decision trace (`Trace`) |

## Editor (all Unity layer)

| v1 | |
| -- | - |
| Utility Designer window: Evaluation and Execution tabs | |
| State set menu: add, remove (with confirmation), rename, switch | |
| State list: add, remove, rename, reorder, select | |
| State inspector: active, weight, execution factor, base score, min and max score toggles, fail chance slider, notes | |
| Preconditions list: add, remove, rename, consideration dropdown, comparator, value | |
| Evaluators list: add, remove, rename, consideration dropdown, weight slider with its share in percent, curve field, curve min and max | |
| Consideration sets shown beside the states, with an edit button | |
| Consideration Set window: local toggle, considerations add, remove, rename, initial value, min and max toggles, change per second | |
| Save and Load by file name | |
| Behaviour tree graph: grid, zoom, pan, drag, box select, edges between ports | |
| Create node: context menu by category, the Add Node button, the search window (Space) | |
| Copy and paste nodes (Ctrl+C, Ctrl+V), copy and paste a whole tree between states | |
| Node view: colour by kind, description, the node's values on its face, notes | |
| Node inspector: every public field, SceneRefs dropdowns | |
| Play mode: each state's score with a bar, each consideration's runtime value, nodes coloured running, success or failure | |
| The component's inspector: behaviour asset, scene refs object name, tick rates, log to file | |
| Demos (Survival), the three render pipeline packages, the manual PDF | |

## For discussion

What looks wrong in v1. The core does every one of these as v1 does.

1. **A finished tree leaves the agent idle for as long as its state stays the best**, not only until the next
   evaluation: the evaluation returns the same state, and a tree is only started when the state changes.
2. **When every state is passed over by its fail chance, the last one checked is taken**: the lowest scoring.
3. **The fail chance is rolled every evaluation**, so how long a state is passed over depends on the evaluation tick
   rate: at 10 evaluations a second, a fail chance of 0.5 delays a state by a tenth of a second on average.
4. **A lock does not hold when nothing can be chosen**: the locked state is ended. But while any other state can be
   chosen, a locked state keeps running even with its own preconditions unmet or itself inactive.
5. **A score below 0 counts as "cannot be chosen"**, the same as an unmet precondition: a negative weight or base score
   removes the state unless a min score lifts it.
6. **Every agent that starts sets the global considerations back to their initial values**, for all agents.
7. **Lock State Consideration with Add Consideration Value adds to its own setting**: the second run's target is the
   first run's target plus the value, not the setting plus the value.
8. **Unlocking evaluates in the middle of the tree's tick.** The tree may be terminated while it runs; a Parallel then
   still ticks its remaining children that tick, and they stay enabled in a tree that is not running: the next time the
   state is chosen they carry on from there (a Wait that started then is already over).
9. **Equal and Not Equal compare whole numbers** (both sides rounded, halves to even): 2.4 equals 1.6.
10. **Compare Consideration treats a consideration its set does not have as 0**, where Set and Change fail.
11. **Changing the state set takes effect at the next evaluation**, and not at all while the state is locked.
12. **Repeat has no count and ignores its child's result.**
13. **Sequence and Selector run one child per tick**, so a sequence of five instant nodes takes five execution ticks.
14. **Crashes**, which the core turns into failures (above): a composite without children, a decorator or root without
    a child being terminated, Lock State Consideration without its set.
15. **The log prints a local set's template values**, not the agent's own. The core's log prints the agent's: the one
    place it differs on purpose, and not a decision.
