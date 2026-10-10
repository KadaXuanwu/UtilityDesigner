using System;
using System.Collections.Generic;

namespace KadaXuanwu.UtilityDesigner.Core {
    // Jobs the world advertises and the agents who do them, for a team of agents sharing work: the highest tier with an
    // open job is worked first, within it each agent takes the job it scores best, greedily, the best pair first. Commitment:
    // a busy agent changes job only for a higher tier, or, MinCommit seconds in, for a job scoring Interrupt times its own
    // (its own counted with what is done of it, SunkCost). A crew job needs Crew agents at once; a crew not whole within
    // CrewTimeout seconds is released.
    public sealed class TaskPool<TAgent, TTask> where TTask : class {
        private sealed class Work {
            public TTask Task;
            public double Since;
            public float Progress;
        }

        private readonly List<TTask> _tasks = new List<TTask>();
        private readonly Dictionary<TTask, List<TAgent>> _crews = new Dictionary<TTask, List<TAgent>>();
        private readonly Dictionary<TTask, double> _crewSince = new Dictionary<TTask, double>();
        private readonly Dictionary<TAgent, Work> _work = new Dictionary<TAgent, Work>();
        private readonly List<(TAgent Agent, TTask Task, float Score, int Order)> _pairs = new List<(TAgent, TTask, float, int)>();

        // How much an agent wants a job: urgency, value, how near; 0 or less for never.
        public Func<TAgent, TTask, float> Score;
        // Its tier: a higher one is worked first and interrupts a lower.
        public Func<TTask, int> Tier = _ => 0;
        // How many agents it needs at once.
        public Func<TTask, int> Crew = _ => 1;
        public float MinCommit = 2f;
        public float Interrupt = 1.5f;
        public float SunkCost = 1f;
        public float CrewTimeout = 20f;

        public IReadOnlyList<TTask> Tasks => _tasks;

        public TaskPool(Func<TAgent, TTask, float> score) {
            Score = score;
        }

        public void Advertise(TTask task) {
            if (!_crews.ContainsKey(task)) {
                _tasks.Add(task);
                _crews.Add(task, new List<TAgent>());
            }
        }

        // Gone or done: its agents are free.
        public void Withdraw(TTask task) {
            if (!_crews.TryGetValue(task, out List<TAgent> crew)) {
                return;
            }
            foreach (TAgent agent in crew) {
                _work.Remove(agent);
            }
            _crews.Remove(task);
            _crewSince.Remove(task);
            _tasks.Remove(task);
        }

        public TTask TaskOf(TAgent agent) => _work.TryGetValue(agent, out Work work) ? work.Task : null;

        public IReadOnlyList<TAgent> CrewOf(TTask task) => _crews.TryGetValue(task, out List<TAgent> crew) ? crew : (IReadOnlyList<TAgent>)Array.Empty<TAgent>();

        // Whether a crew job has its whole crew.
        public bool Manned(TTask task) => _crews.TryGetValue(task, out List<TAgent> crew) && crew.Count >= Math.Max(1, Crew(task));

        public void Progress(TAgent agent, float progress) {
            if (_work.TryGetValue(agent, out Work work)) {
                work.Progress = Math.Clamp(progress, 0f, 1f);
            }
        }

        // The agent stops its job (died, gave up); the job stays open for others.
        public void Leave(TAgent agent) {
            if (_work.TryGetValue(agent, out Work work)) {
                _crews[work.Task].Remove(agent);
                _work.Remove(agent);
            }
        }

        // Hands out jobs to the agents.
        public void Assign(IReadOnlyList<TAgent> agents, double now) {
            ReleaseStaleCrews(now);
            int tier = int.MinValue;
            foreach (TTask task in _tasks) {
                if (Open(task)) {
                    tier = Math.Max(tier, Tier(task));
                }
            }
            if (tier == int.MinValue) {
                return;
            }
            _pairs.Clear();
            for (int a = 0; a < agents.Count; a++) {
                TAgent agent = agents[a];
                Work current = _work.TryGetValue(agent, out Work work) ? work : null;
                for (int t = 0; t < _tasks.Count; t++) {
                    TTask task = _tasks[t];
                    if (!Open(task) || Tier(task) != tier || (current != null && current.Task == task)) {
                        continue;
                    }
                    float score = Score(agent, task);
                    if (score > 0f && Beats(agent, current, task, score, now)) {
                        _pairs.Add((agent, task, score, a * _tasks.Count + t));
                    }
                }
            }
            // Ties broken by agent, then job order, so every runtime hands out the same.
            _pairs.Sort((a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score) : a.Order.CompareTo(b.Order));
            HashSet<TAgent> placed = new HashSet<TAgent>();
            foreach ((TAgent agent, TTask task, _, _) in _pairs) {
                if (placed.Contains(agent) || !Open(task)) {
                    continue;
                }
                Leave(agent);
                _crews[task].Add(agent);
                if (_crews[task].Count == 1) {
                    _crewSince[task] = now;
                }
                _work[agent] = new Work { Task = task, Since = now };
                placed.Add(agent);
            }
        }

        // Room for another agent.
        private bool Open(TTask task) => _crews[task].Count < Math.Max(1, Crew(task));

        // Whether a free agent, or a busy one by the commitment rules, takes the job.
        private bool Beats(TAgent agent, Work current, TTask task, float score, double now) {
            if (current == null) {
                return true;
            }
            int was = Tier(current.Task);
            int would = Tier(task);
            if (would != was) {
                return would > was;
            }
            if (now - current.Since < MinCommit) {
                return false;
            }
            float kept = Score(agent, current.Task) * (1f + SunkCost * current.Progress);
            return score > kept * Interrupt;
        }

        private void ReleaseStaleCrews(double now) {
            foreach (TTask task in _tasks) {
                List<TAgent> crew = _crews[task];
                if (crew.Count > 0 && crew.Count < Crew(task) && _crewSince.TryGetValue(task, out double since) && now - since > CrewTimeout) {
                    foreach (TAgent agent in crew) {
                        _work.Remove(agent);
                    }
                    crew.Clear();
                    _crewSince.Remove(task);
                }
            }
        }
    }
}
