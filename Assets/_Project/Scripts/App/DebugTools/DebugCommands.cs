using System.Collections.Generic;
using TrashCats.Domain;

namespace TrashCats.App
{
    /// <summary>Development-only cheats for playtesting balance. Not reachable from release UI.</summary>
    public sealed class DebugCommands
    {
        private readonly GameSession _session;
        private readonly EventBus _bus;
        private readonly ISaveRequests _save;

        public DebugCommands(GameSession session, EventBus bus, ISaveRequests save)
        {
            _session = session;
            _bus = bus;
            _save = save ?? NoSaveRequests.Instance;
        }

        public void AddResources(int food, int junk)
        {
            var events = new List<DomainEvent>();
            var wallet = _session.MutableWorld.Wallet;
            wallet.Add(ResourceType.Food, food, events);
            wallet.Add(ResourceType.Junk, junk, events);
            Publish(events);
        }

        /// <summary>Simulates game time instantly, as if it had passed.</summary>
        public void SkipGameTime(long ms)
        {
            _session.Tick();
            var events = new List<DomainEvent>();
            Simulation.AdvanceBy(_session.MutableWorld, ms, events);
            Publish(events);
        }

        private void Publish(List<DomainEvent> events)
        {
            _bus.PublishAll(events);
            _save.MarkDirty(SaveUrgency.Deferred);
        }
    }
}
