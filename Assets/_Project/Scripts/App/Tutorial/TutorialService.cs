using System;
using System.Collections.Generic;
using System.Linq;
using TrashCats.Domain;

namespace TrashCats.App
{
    /// <summary>Active onboarding step; texts and highlighted widgets are chosen by the presentation from the id.</summary>
    public sealed class TutorialStep
    {
        public string Id { get; }
        public CatId Cat { get; }
        public SkillId? Skill { get; }
        public TerritoryId? Territory { get; }

        public TutorialStep(string id, CatId cat, SkillId? skill = null, TerritoryId? territory = null)
        {
            Id = id;
            Cat = cat;
            Skill = skill;
            Territory = territory;
        }

        public override bool Equals(object obj) =>
            obj is TutorialStep o && o.Id == Id && o.Cat.Equals(Cat) && Nullable.Equals(o.Skill, Skill) && Nullable.Equals(o.Territory, Territory);

        public override int GetHashCode() => Id.GetHashCode();
    }

    /// <summary>
    /// Onboarding (Docs/GDD/UI_UX.md §11): a guided chain — scavenge, feed, train Strength, equip the first skill,
    /// sleep when tired, first fight — plus one-shot contextual hints. A step shows when its prerequisites are done and
    /// its situation holds, and completes on the player's matching action; progress lives in the world (saved).
    /// </summary>
    public sealed class TutorialService : IDisposable
    {
        public const string Scavenge = "t_scavenge";
        public const string Feed = "t_feed";
        public const string Train = "t_train";
        public const string Equip = "t_skill";
        public const string Sleep = "t_sleep";
        public const string Fight = "t_fight";

        /// <summary>Marker: some attribute levelled up (the gang got stronger, time for the first fight).</summary>
        public const string LevelledUp = "m_levelup";

        public const string HintJunk = "h_junk";
        public const string HintRelay = "h_relay";
        public const string HintSlots = "h_slots";

        /// <summary>Energy below which the sleep step shows (the gang can still fight above it).</summary>
        public const int TiredEnergy = 20;

        private readonly GameSession _session;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        public TutorialStep Current { get; private set; }
        public event Action<TutorialStep> StepChanged;
        public event Action<string> HintRequested;

        private IReadOnlyGameWorld World => _session.World;
        private GameConfig Config => _session.Config;
        private TutorialState State => _session.World.Tutorial;

        public TutorialService(GameSession session, EventBus bus)
        {
            _session = session;
            _subscriptions.Add(bus.Subscribe<ActivityStarted>(OnActivityStarted));
            _subscriptions.Add(bus.Subscribe<ResourcesChanged>(e =>
            {
                if (e.FoodDelta < 0) Done(Feed); // only feeding spends food
                Evaluate();
            }));
            _subscriptions.Add(bus.Subscribe<SkillEquipped>(_ => { Done(Equip); Evaluate(); }));
            _subscriptions.Add(bus.Subscribe<BattleStarted>(_ => { Done(Fight); Evaluate(); }));
            _subscriptions.Add(bus.Subscribe<SkillUnlocked>(_ => Evaluate()));
            _subscriptions.Add(bus.Subscribe<AttributeLevelUp>(_ => { Done(LevelledUp); Evaluate(); }));
            _subscriptions.Add(bus.Subscribe<NeedsChanged>(_ => Evaluate()));
            _subscriptions.Add(bus.Subscribe<CatRecruited>(_ => Evaluate()));
            _subscriptions.Add(bus.Subscribe<ActivityStopped>(_ => Evaluate()));

            // saves from before the tutorial existed: a player who is already past the start does not need it
            if (!State.Skipped && !State.Completed.Any() && (World.Cats.Count > 1 || Config.Territories.Any(t => World.Territories.Get(t.Id).Status == TerritoryStatus.Captured)))
                _session.SkipTutorial();
        }

        public bool IsDone(string id) => State.IsDone(id);

        public void Skip()
        {
            _session.SkipTutorial();
            Evaluate();
        }

        /// <summary>The battle preparation opened with this many cats available (relay hint).</summary>
        public void OnPrepOpened(int availableCats)
        {
            if (availableCats >= 2)
                Hint(HintRelay);
        }

        /// <summary>
        /// Re-checks which step is active and fires hints whose situation came up. Call once after subscribing to
        /// <see cref="StepChanged"/> and <see cref="HintRequested"/>; afterwards domain events trigger it.
        /// </summary>
        public void Evaluate()
        {
            if (World.Junk > 0) Hint(HintJunk);
            if (World.Cats.Any(c => UnlockedSkills(c).Count() > Cat.SkillSlotCount)) Hint(HintSlots);

            var next = State.Skipped ? null : FindStep();
            if (Equals(next, Current))
                return;
            Current = next;
            StepChanged?.Invoke(next);
        }

        private TutorialStep FindStep()
        {
            var cats = World.Cats;
            if (cats.Count == 0)
                return null;
            var first = cats[0];

            if (!IsDone(Scavenge))
                return new TutorialStep(Scavenge, first.Id);

            if (!IsDone(Feed))
            {
                if (World.Food <= Config.Start.Food)
                    return null; // wait for the first haul
                if (_session.CanFeed(first.Id) == CommandError.None)
                    return new TutorialStep(Feed, first.Id);
                Done(Feed); // the cat is full: nothing to teach
            }

            if (!IsDone(Train))
                return new TutorialStep(Train, first.Id);

            if (!IsDone(Equip))
            {
                foreach (var cat in cats)
                {
                    var skill = UnlockedSkills(cat).FirstOrDefault();
                    if (skill != null && Enumerable.Range(0, Cat.SkillSlotCount).All(i => cat.GetSkillInSlot(i) == null))
                        return new TutorialStep(Equip, cat.Id, skill.Id);
                }
            }

            if (!IsDone(Sleep))
            {
                var tired = cats.FirstOrDefault(c => c.Energy < TiredEnergy && !IsDoing(c, ActivityKind.Sleep));
                if (tired != null)
                    return new TutorialStep(Sleep, tired.Id);
            }

            if (!IsDone(Fight))
            {
                var target = Config.Territories.FirstOrDefault(t => World.Territories.Get(t.Id).Status == TerritoryStatus.Available);
                var ready = cats.Any(c => c.Energy >= Config.Needs.MinEnergyForBattle);
                var trained = IsDone(Sleep) || IsDone(LevelledUp);
                if (target != null && ready && trained)
                    return new TutorialStep(Fight, first.Id, territory: target.Id);
            }

            return null;
        }

        private IEnumerable<SkillDefinition> UnlockedSkills(IReadOnlyCat cat) =>
            Config.Skills.Where(s => !s.IsInnate && CatRules.IsSkillUnlocked(cat, s));

        private bool IsDoing(IReadOnlyCat cat, ActivityKind kind) =>
            cat.Activity.IsWorking && Config.GetActivity(cat.Activity.ActivityId).Kind == kind;

        private void OnActivityStarted(ActivityStarted e)
        {
            if (!e.IsRepeat)
            {
                var activity = Config.GetActivity(e.ActivityId);
                if (activity.Kind == ActivityKind.Scavenge) Done(Scavenge);
                if (activity.Kind == ActivityKind.Train && activity.TrainedAttribute == AttributeType.Strength) Done(Train);
                if (activity.Kind == ActivityKind.Sleep) Done(Sleep);
            }

            Evaluate();
        }

        private void Done(string id) => _session.CompleteTutorialStep(id);

        private void Hint(string id)
        {
            if (State.IsDone(id))
                return;
            _session.CompleteTutorialStep(id);
            HintRequested?.Invoke(id);
        }

        public void Dispose()
        {
            foreach (var s in _subscriptions)
                s.Dispose();
        }
    }
}
