using System;
using System.Collections.Generic;
using KadaXuanwu.UtilityDesigner.Core;

namespace KadaXuanwu.UtilityDesigner.Samples {
    // A villager in a plain C# loop, no engine: hungry, tired and bored by turns, it eats, sleeps, works or wanders, each a
    // small tree; its brain chooses among them by utility, with momentum, a minimum commitment and a little randomness
    // among near ties. The same seed lives the same day every time.
    public sealed class Villager {
        public float Hunger = 0.3f;
        public float Energy = 0.8f;
        public float Boredom = 0.2f;
        public int Food = 3;
        public readonly List<string> Diary = new List<string>();

        private readonly Choose<Villager> _day;
        private readonly Brain<Villager, int> _brain;

        public static Reasoner Design() {
            Reasoner design = new Reasoner { Name = "Villager", Momentum = 1.2f, MinCommit = 2f, Interrupt = 1.4f, Spread = 0.15f };
            design.Actions.Add(Action("Eat", 1f, Need("Hunger", Curve.Logistic(10f, 0.5f)), Need("Food", Curve.Step(0.2f), 0f, 5f)));
            design.Actions.Add(Action("Sleep", 1f, Need("Tiredness", Curve.Polynomial(2f))));
            design.Actions.Add(Action("Work", 0.6f, Need("Energy", Curve.Linear()), Need("Hunger", Curve.Linear(-1f, 1f))));
            design.Actions.Add(Action("Wander", 0.5f, Need("Boredom", Curve.Linear())));
            return design;
        }

        private static UtilityAction Action(string name, float weight, params Consideration[] considerations) {
            UtilityAction action = new UtilityAction { Name = name, Weight = weight };
            action.Considerations.AddRange(considerations);
            return action;
        }

        private static Consideration Need(string input, Curve curve, float min = 0f, float max = 1f) =>
            new Consideration { Name = input, Input = input, Curve = curve, Min = min, Max = max };

        public Villager(Reasoner design, ulong seed) {
            Bindings<Villager, int> inputs = new Bindings<Villager, int>()
                .Input("Hunger", (v, _) => v.Hunger)
                .Input("Food", (v, _) => v.Food)
                .Input("Tiredness", (v, _) => 1f - v.Energy)
                .Input("Energy", (v, _) => v.Energy)
                .Input("Boredom", (v, _) => v.Boredom);
            _brain = design.Bind(inputs, seed);
            _day = new Choose<Villager>(_brain,
                new Sequence<Villager>(new Wait<Villager>(1.0), new Do<Villager>("Eat", (v, _) => v.Eat())),
                new Do<Villager>("Sleep", (v, _) => v.Sleep()),
                new Sequence<Villager>(new Wait<Villager>(3.0), new Do<Villager>("Harvest", (v, _) => v.Harvest())),
                new Sequence<Villager>(new Wait<Villager>(2.0), new Do<Villager>("Look around", (v, _) => v.LookAround())));
        }

        public Brain<Villager, int> Brain => _brain;

        // One step of its day: a tenth of an hour.
        public void Tick(double now) {
            Hunger = Math.Min(1f, Hunger + 0.01f);
            Energy = Math.Max(0f, Energy - 0.008f);
            Boredom = Math.Min(1f, Boredom + 0.006f);
            string before = _brain.Current.Name;
            _day.Tick(this, now);
            if (_brain.Current.Name != before) {
                Diary.Add($"{now:0.0} {_brain.Current.Name}");
            }
        }

        private Status Eat() {
            Food--;
            Hunger = 0f;
            return Status.Success;
        }

        private Status Sleep() {
            Energy = Math.Min(1f, Energy + 0.05f);
            return Energy >= 1f ? Status.Success : Status.Running;
        }

        private Status Harvest() {
            Food++;
            Boredom = Math.Max(0f, Boredom - 0.1f);
            return Status.Success;
        }

        private Status LookAround() {
            Boredom = 0f;
            return Status.Success;
        }

        // A day of ticks, the diary of what it chose.
        public static List<string> Live(ulong seed, int ticks = 2000) {
            Villager villager = new Villager(Design(), seed);
            for (int i = 0; i < ticks; i++) {
                villager.Tick(i * 0.1);
            }
            return villager.Diary;
        }
    }
}
