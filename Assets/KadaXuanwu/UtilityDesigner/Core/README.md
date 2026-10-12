# Utility Designer Core

Utility Designer's engine-free core, for agents that run anywhere: in Unity, in a dedicated server, in a headless
simulation. Plain C# (netstandard2.1, no UnityEngine). It decides exactly as Utility Designer v1 does; only the
plumbing differs: the caller owns the clock and the random numbers, and a design is JSON.
[V1-CHECKLIST.md](V1-CHECKLIST.md) lists every v1 feature and where 2.0 has it.

- `Design`: consideration sets (local to an agent, or shared through `Globals`) and state sets. A `State` has
  preconditions, evaluators (a `Curve` over a consideration, normalised between a min and a max) and a behaviour tree.
- `Agent`: one agent running a design. `Evaluate(now)` moves the considerations on and chooses the state by v1's
  score, `Weight · (BaseScore + Σ Score · EvalWeight / TotalEvalWeights)`, times the execution factor while it runs,
  held to its min and max; fail chances drawn from the `Rng` the caller passes in. `Execute(now)` ticks the chosen
  state's tree.
- `Node`: v1's nodes that need no engine (`Sequence`, `Selector`, `Parallel`, `RandomSelector`, `RandomSequence`,
  `Invert`, `Repeat`, `Success`, `Failure`, `WaitFixed`, `WaitRandom`, `LockState`, `LockStateDuration`,
  `LockStateConsideration`, `UnlockState`, `ChangeStateSet`, `SetConsideration`, `ChangeConsideration`,
  `CompareConsideration`, `TimeCheck`). The game's own subclass `ActionNode` or `ConditionalNode` and register with
  `NodeTypes.Register`; their public fields are their settings.
- `Curve`: keyframes (Unity's `AnimationCurve` shapes) or four numbers (Linear, Polynomial, Logistic, Logit, Sine,
  Step).
- `Designs`: versioned JSON, every tunable number as a `Parameter` (an optimizer's vector), override layers. A
  number's id is its names (`states.Day.Eat.Hunger.weight`): keep the names in one state different.
- `Trace`: the last evaluations, every state's score, as JSON lines; `Agent.WriteLog` writes v1's log entry.

```csharp
Design design = Designs.FromJson(json);
Agent agent = new Agent(design, new Globals(), new Rng(seed));
agent.Evaluate(now);
agent.Execute(now);
```

`Samples~/HeadlessVillager` is an agent in a plain C# loop. Tests: `dotnet test` in `Tests~`.

In Unity, `Runtime` and `Editor` beside this package are the layer on top: the `UtilityDesigner` component ticks an
`Agent` on game time, and the Utility Designer window edits the designs.
