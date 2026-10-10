using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace KadaXuanwu.UtilityDesigner.Core {
    // The last Capacity decisions of a brain, every option it scored and what it chose, so "why did it do that" has an
    // answer after the fact. Off (null) costs nothing; on, it keeps strings of the targets.
    public sealed class Trace {
        public struct Option {
            public string Action;
            public string Target;
            public float Score;
            public int Rank;
        }

        public sealed class Entry {
            public double Time;
            public string Brain;
            public string Chosen;
            public string ChosenTarget;
            public float ChosenScore;
            public readonly List<Option> Options = new List<Option>();
        }

        private readonly Entry[] _entries;
        private int _next;

        public int Count { get; private set; }

        public Trace(int capacity = 256) {
            _entries = new Entry[capacity];
            for (int i = 0; i < capacity; i++) {
                _entries[i] = new Entry();
            }
        }

        // The i-th oldest entry kept.
        public Entry this[int i] => _entries[(_next - Count + i + _entries.Length) % _entries.Length];

        public void Record<TTarget>(double time, string brain, List<Choice<TTarget>> options, Choice<TTarget> chosen) {
            Entry entry = _entries[_next];
            _next = (_next + 1) % _entries.Length;
            Count = System.Math.Min(Count + 1, _entries.Length);
            entry.Time = time;
            entry.Brain = brain;
            entry.Chosen = chosen.Valid ? chosen.Name : "";
            entry.ChosenTarget = chosen.Valid ? chosen.Target?.ToString() ?? "" : "";
            entry.ChosenScore = chosen.Score;
            entry.Options.Clear();
            foreach (Choice<TTarget> option in options) {
                entry.Options.Add(new Option { Action = option.Name, Target = option.Target?.ToString() ?? "", Score = option.Score, Rank = option.Rank });
            }
        }

        // One JSON object a line, oldest first.
        public void Write(TextWriter writer) {
            for (int i = 0; i < Count; i++) {
                Entry entry = this[i];
                StringBuilder line = new StringBuilder();
                line.Append("{\"time\":").Append(entry.Time.ToString(CultureInfo.InvariantCulture));
                line.Append(",\"brain\":").Append(Json.Quote(entry.Brain));
                line.Append(",\"chosen\":").Append(Json.Quote(entry.Chosen));
                line.Append(",\"target\":").Append(Json.Quote(entry.ChosenTarget));
                line.Append(",\"score\":").Append(entry.ChosenScore.ToString(CultureInfo.InvariantCulture));
                line.Append(",\"options\":[");
                for (int o = 0; o < entry.Options.Count; o++) {
                    Option option = entry.Options[o];
                    line.Append(o > 0 ? "," : "").Append("{\"action\":").Append(Json.Quote(option.Action));
                    line.Append(",\"target\":").Append(Json.Quote(option.Target));
                    line.Append(",\"score\":").Append(option.Score.ToString(CultureInfo.InvariantCulture));
                    line.Append(",\"rank\":").Append(option.Rank).Append('}');
                }
                writer.WriteLine(line.Append("]}").ToString());
            }
        }
    }
}
