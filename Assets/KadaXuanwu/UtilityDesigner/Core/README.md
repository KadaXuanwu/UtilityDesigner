# Utility Designer Core

Utility Designer's engine-free core, for agents that run anywhere: in Unity, in a dedicated server, in a headless
simulation. Plain C# (netstandard2.1, no UnityEngine), ticked by the caller with the time, deterministic for a seed.

- `Curve`: response curves, four numbers each (Linear, Polynomial, Logistic, Logit, Sine, Step) or keyframes.
- `Reasoner`, `Bindings`, `Brain`: actions scored by their considerations (multiplied, compensated for their count, a
  zero vetoes), once per target if the action has a target provider; dual utility ranks; commitment by momentum, a
  minimum commitment, an interrupt threshold and sunk cost; cooldowns; a weighted random pick among near ties.
- `Node`, `Sequence`, `Selector`, `Parallel`, `While` (conditional abort), `Repeat`, `Invert`, `Condition`, `Do`,
  `Wait`, `Choose` (a brain choosing a child), `Blackboard` with typed keys.
- `Compound`, `Method`, `Primitive`, `Planner`, `PlanRunner`: hierarchical task networks, methods picked by utility,
  plans kept unless broken or beaten by a margin worth switching for.
- `TaskPool`: jobs shared by a team of agents: tiers, greedy assignment, commitment, crews.
- `Designs`: versioned JSON, every tunable number as a `Parameter` (an optimizer's vector), override layers.
- `Trace`: the last decisions, every option scored, as JSON lines.

`Samples~/HeadlessVillager` is an agent in a plain C# loop. Tests: `dotnet test` in `Tests~`.
