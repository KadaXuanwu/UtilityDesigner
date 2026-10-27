using System.Collections.Generic;

namespace KadaXuanwu.UtilityDesigner.Core {
    // The agents a game offers to a viewer (the Utility Designer window) by a name, for agents that no component runs: on
    // a worker thread, in a simulation. Safe from any thread. The viewer never changes an offered agent.
    public static class OfferedAgents {
        public sealed class Offer {
            public readonly string Name;
            public readonly Agent Agent;
            // The lock the game holds while it ticks the agent; the viewer reads the agent only while it holds it too.
            // Null: the agent ticks on the viewer's thread (Unity's main thread).
            public readonly object Gate;

            internal Offer(string name, Agent agent, object gate) {
                Name = name;
                Agent = agent;
                Gate = gate;
            }
        }

        private static readonly List<Offer> Offers = new List<Offer>();

        // Goes up at every Add and Remove, so a viewer can tell the list changed.
        public static int Version { get; private set; }

        // Offering an agent again replaces its name and gate.
        public static void Add(string name, Agent agent, object gate = null) {
            lock (Offers) {
                Offers.RemoveAll(offer => offer.Agent == agent);
                Offers.Add(new Offer(name, agent, gate));
                Version++;
            }
        }

        // Call it when the agent ends: a viewer showing it then shows nothing.
        public static void Remove(Agent agent) {
            lock (Offers) {
                if (Offers.RemoveAll(offer => offer.Agent == agent) > 0) {
                    Version++;
                }
            }
        }

        public static Offer[] All() {
            lock (Offers) {
                return Offers.ToArray();
            }
        }
    }
}
