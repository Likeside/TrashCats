using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrashCats.App;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class PlaytestLogTests
    {
        private sealed class ListSink : ILineSink
        {
            public readonly List<string> Lines = new List<string>();
            public void Append(string line) => Lines.Add(line);
        }

        [Test]
        public void Logs_Activities_Skills_And_BattleOrderChanges()
        {
            var config = TestConfigs.Default();
            var clock = new FakeClock();
            var bus = new EventBus();
            var world = new GameWorld(config, 0, 1, new[] { TestCats.Make(TestIds.Ryzhy, str: 4) }, new Wallet(20, 0), new UpgradeLevels());
            world.Territories.Get(TestIds.Backyard).Status = TerritoryStatus.Available;
            var session = new GameSession(world, clock, bus, new RecordingSaveRequests(), clock.UtcNowMs);
            var sink = new ListSink();
            using var log = new PlaytestLog(session, bus, clock, sink);

            session.AssignActivity(TestIds.Ryzhy, TestIds.TrainStr);
            session.EquipSkill(TestIds.Ryzhy, 0, TestIds.Bite);
            session.StartBattle(TestIds.Backyard, new[] { TestIds.Ryzhy });
            session.CompleteBattle();

            var types = sink.Lines.Select(l => l.Split("\"type\":\"")[1].Split('"')[0]).ToList();
            Assert.That(types, Is.SupersetOf(new[] { "session", "activity", "skill", "battle", "battle_end" }));
            Assert.That(sink.Lines.Single(l => l.Contains("\"type\":\"battle\"")), Does.Contain("\"order\":\"cat_ryzhy\"").And.Contain("skill_bite"));
            Assert.That(sink.Lines.All(l => l.StartsWith("{") && l.EndsWith("}")));
        }
    }
}
