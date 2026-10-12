using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace KadaXuanwu.UtilityDesigner.Core {
    // The last Capacity evaluations of an agent, every state's score and what runs after it, so "why did it do that" has
    // an answer after the fact. Off (null) costs nothing.
    public sealed class Trace {
        public struct Option {
            public string State;
            public float Score;
            // Whether it could be chosen: active, its preconditions met, its score not below 0.
            public bool Available;
        }

        public sealed class Entry {
            public double Time;
            public string Agent;
            public string StateSet;
            // The state running after the evaluation, empty for none.
            public string Executing;
            public bool Locked;
            public readonly List<Option> Options = new List<Option>();
        }

        private readonly Entry[] _entries;
        private int _next;

        public int Count { get; private set; }
        public int Capacity => _entries.Length;

        public Trace(int capacity = 256) {
            _entries = new Entry[capacity];
            for (int i = 0; i < capacity; i++) {
                _entries[i] = new Entry();
            }
        }

        // The i-th oldest entry kept.
        public Entry this[int i] => _entries[(_next - Count + i + _entries.Length) % _entries.Length];

        internal void Record(double time, Agent agent, StateSet evaluated) {
            Entry entry = _entries[_next];
            _next = (_next + 1) % _entries.Length;
            Count = System.Math.Min(Count + 1, _entries.Length);
            entry.Time = time;
            entry.Agent = agent.Design.Name;
            entry.StateSet = evaluated.Name;
            entry.Executing = agent.ExecutingState?.Name ?? "";
            entry.Locked = agent.StateLocked;
            entry.Options.Clear();
            foreach (State state in evaluated.States) {
                entry.Options.Add(new Option { State = state.Name, Score = state.LastScore, Available = state.LastAvailable });
            }
        }

        // One JSON object a line, oldest first.
        public void Write(TextWriter writer) {
            for (int i = 0; i < Count; i++) {
                Entry entry = this[i];
                StringBuilder line = new StringBuilder();
                line.Append("{\"time\":").Append(entry.Time.ToString(CultureInfo.InvariantCulture));
                line.Append(",\"agent\":").Append(Json.Quote(entry.Agent));
                line.Append(",\"stateSet\":").Append(Json.Quote(entry.StateSet));
                line.Append(",\"executing\":").Append(Json.Quote(entry.Executing));
                line.Append(",\"locked\":").Append(entry.Locked ? "true" : "false");
                line.Append(",\"options\":[");
                for (int o = 0; o < entry.Options.Count; o++) {
                    Option option = entry.Options[o];
                    line.Append(o > 0 ? "," : "").Append("{\"state\":").Append(Json.Quote(option.State));
                    line.Append(",\"score\":").Append(option.Score.ToString(CultureInfo.InvariantCulture));
                    line.Append(",\"available\":").Append(option.Available ? "true" : "false").Append('}');
                }
                writer.WriteLine(line.Append("]}").ToString());
            }
        }
    }
}
