using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TrashCats.Domain;

namespace TrashCats.App
{
    /// <summary>Where telemetry lines go (a local file in the game, a list in tests).</summary>
    public interface ILineSink
    {
        void Append(string line);
    }

    /// <summary>
    /// Local playtest telemetry (M6-09): one JSON object per line with the real time, the game time and what the
    /// player did — enough for the success criteria of GAME_OVERVIEW §9: time to the boss and getting stuck (1),
    /// changes of skills and fighting order between battles (3), how cats are split between food, junk and training (4).
    /// Nothing leaves the device.
    /// </summary>
    public sealed class PlaytestLog : IDisposable
    {
        private readonly GameSession _session;
        private readonly IClock _clock;
        private readonly ILineSink _sink;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private string _lastOrder;

        public PlaytestLog(GameSession session, EventBus bus, IClock clock, ILineSink sink)
        {
            _session = session;
            _clock = clock;
            _sink = sink;
            _subscriptions.Add(bus.Subscribe<ActivityStarted>(e =>
            {
                if (!e.IsRepeat) Write("activity", ("cat", e.CatId.Value), ("activity", e.ActivityId.Value));
            }));
            _subscriptions.Add(bus.Subscribe<ActivityStopped>(e => Write("stopped", ("cat", e.CatId.Value), ("reason", e.Reason.ToString()))));
            _subscriptions.Add(bus.Subscribe<SkillEquipped>(e => Write("skill", ("cat", e.CatId.Value), ("slot", e.Slot), ("skill", e.SkillId?.Value ?? ""))));
            _subscriptions.Add(bus.Subscribe<UpgradePurchased>(e => Write("upgrade", ("upgrade", e.UpgradeId.Value), ("level", e.NewLevel))));
            _subscriptions.Add(bus.Subscribe<BattleStarted>(e =>
            {
                var order = string.Join(",", e.Battle.Order.Select(c => c.Value));
                var skills = string.Join(";", e.Battle.Setup.PlayerQueue.Select(f => f.Id + ":" + string.Join(",", f.Skills.Select(s => s.Value))));
                Write("battle", ("territory", e.Battle.Territory.Value), ("order", order), ("skills", skills), ("orderChanged", _lastOrder != null && _lastOrder != order));
                _lastOrder = order;
            }));
            _subscriptions.Add(bus.Subscribe<BattleCompleted>(e => Write("battle_end", ("territory", e.Territory.Value), ("outcome", e.Outcome.ToString()))));
            _subscriptions.Add(bus.Subscribe<CatRecruited>(e => Write("recruit", ("cat", e.CatId.Value))));
            _subscriptions.Add(bus.Subscribe<FinalReached>(_ => Write("final")));
            Write("session", ("cats", session.World.Cats.Count));
        }

        /// <summary>Records a moment without a domain event (app paused, tutorial skipped).</summary>
        public void Mark(string type) => Write(type);

        private void Write(string type, params (string Key, object Value)[] fields)
        {
            var sb = new StringBuilder();
            sb.Append("{\"t\":").Append(_clock.UtcNowMs.ToString(CultureInfo.InvariantCulture))
              .Append(",\"game\":").Append(_session.World.GameTimeMs.ToString(CultureInfo.InvariantCulture))
              .Append(",\"type\":\"").Append(type).Append('"');
            foreach (var (key, value) in fields)
            {
                sb.Append(",\"").Append(key).Append("\":");
                switch (value)
                {
                    case bool b: sb.Append(b ? "true" : "false"); break;
                    case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                    default: sb.Append('"').Append(Escape(value?.ToString() ?? "")).Append('"'); break;
                }
            }

            _sink.Append(sb.Append('}').ToString());
        }

        private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        public void Dispose()
        {
            foreach (var s in _subscriptions) s.Dispose();
        }
    }
}
