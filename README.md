# Utility Designer

Utility AI and behaviour trees for Unity: an agent scores its states by weighted evaluators over considerations, and
the best state's behaviour tree runs. This repository is the Unity project (Unity 6.7) of Utility Designer 2.0, with the
LaTeX of the thesis it began as in `Doku`.

2.0 has every feature of v1 and decides exactly as v1 does. What changed is underneath: an engine-free core, behaviours
as JSON files, game time, seeded random numbers. [V1-CHECKLIST.md](Assets/KadaXuanwu/UtilityDesigner/Core/V1-CHECKLIST.md)
lists every v1 feature and where 2.0 has it. v1 behaviours are not converted: rebuild them in the 2.0 editor. v1 itself
stays on the
[Unity Asset Store](https://assetstore.unity.com/packages/tools/behavior-ai/utility-designer-combining-utility-ai-and-behaviour-trees-257126);
2.0 is released here only.

## What is where

Everything is under `Assets/KadaXuanwu/UtilityDesigner`:

- `Core`: the core, plain C# with no UnityEngine, usable in a server or a headless simulation
  ([its README](Assets/KadaXuanwu/UtilityDesigner/Core/README.md)). Tests: `dotnet test` in `Core/Tests~`.
- `Runtime`: the `UtilityDesigner` component, Consideration Set assets, `SceneReferences`, and the nodes that need
  Unity (Move To Position, Move To Transform, Compare Distance, Debug Log, Debug Breakpoint).
- `Editor`: the Utility Designer window, the Consideration Set window, the component's inspector. Tests in
  `Editor/Tests`, run from Unity's Test Runner.
- `Demos`: the Survival and Daily Routine behaviours and scripts. Their scenes are in three packages, one for each
  render pipeline (`DemosBuiltIn`, `DemosURP`, `DemosHDRP`): import the one for your pipeline, and Unity's AI Navigation
  package for the pathfinding. `Assets/Scenes/Survival.unity` is the Survival scene with better looking assets, which
  come from another pack and cannot be bundled. It is the scene in the build settings. A built Survival logs
  `Failed to create agent because there is no valid NavMesh` once for each character at start: Unity makes the agents
  before the NavMesh Surface adds its NavMesh, and puts them on it before the first frame. They walk.

## Using it

1. Create the considerations: Create > Utility Designer > New Consideration Set, then open the asset. A local set is
   each agent's own; any other is shared by every agent.
2. Add the Utility Designer component to a GameObject and press Open editor. Add the consideration sets, the states
   with their preconditions and evaluators (Evaluation tab) and each state's behaviour tree (Execution tab).
3. Save: the behaviour is written as a `.json` file and given to the component. From then on it is saved whenever the
   window loses focus or closes, and before play starts.
4. Play. The window shows the selected agent's scores, consideration values and running nodes, and under the states
   its last decisions: when it took up a state, at which score, and the best state it left.

The component ticks on game time, so a paused game stops deciding. Its Seed makes a run repeat; 0 takes another seed
every run.

Game code reads and writes considerations through the Consideration Set asset, or through the agent:

```csharp
float hunger = jamesConsiderations.GetConsideration("Hunger", gameObject);   // a local set: this agent's value
environmentConsiderations.SetConsideration("Bears nearby", 1);              // a shared set
GetComponent<UtilityDesigner>().Agent.ChangeConsideration("UD James", "Food", 2);
```

## Your own nodes

Subclass `ActionNode`, `ConditionalNode`, `CompositeNode` or `DecoratorNode` (namespace `KadaXuanwu.UtilityDesigner.Core`).
The class is found by its name; its public fields are its settings, shown in the node inspector and saved with the
behaviour: numbers, booleans, text, enums, and Unity's vectors, colours, rects, bounds and quaternions.

```csharp
[CategoryPath("Combat")]                       // where the editor lists it
public class Shout : ActionNode {
    public string Text = "Hey";
    [SceneRef(typeof(Transform), "Target")]    // an index into the SceneReferences list, shown as a dropdown
    public int TargetIndex;

    protected override NodeState OnUpdate() {
        UtilityDesigner designer = UtilityDesigner.Of(Agent);   // the component: its GameObject, its SceneReferences
        Debug.Log($"{designer.name}: {Text}, {designer.SceneReferences.GetRef<Transform>(TargetIndex).name}");
        return NodeState.Success;
    }
}
```

`Agent` is the running agent: its clock (`Now`), its seeded random numbers (`Random`), its considerations and its lock.
A node class that only a behaviour names needs `[Preserve]` in builds with high code stripping.

I fully own this asset. Feel free to use and change this code for your own project or to create pull requests. Please do
not sell it without my permission.
