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
        /// <summary>The cat the step is about; it also says the phrase.</summary>
        public CatId Cat { get; }
        public SkillId? Skill { get; }
        public TerritoryId? Territory { get; }
        public UpgradeId? Upgrade { get; }

        /// <summary>
        /// Soft steps only point at their target (no dimming, taps go everywhere): the player already knows the
        /// basics after the first three steps.
        /// </summary>
        public bool Soft { get; }

        public TutorialStep(string id, CatId cat, SkillId? skill = null, TerritoryId? territory = null, UpgradeId? upgrade = null, bool soft = false)
        {
            Id = id;
            Cat = cat;
            Skill = skill;
            Territory = territory;
            Upgrade = upgrade;
            Soft = soft;
        }

        public override bool Equals(object obj) =>
            obj is TutorialStep o && o.Id == Id && o.Cat.Equals(Cat) && Nullable.Equals(o.Skill, Skill) && Nullable.Equals(o.Territory, Territory)
            && Nullable.Equals(o.Upgrade, Upgrade);

        public override int GetHashCode() => Id.GetHashCode();
    }

    /// <summary>Onboarding moment for the playtest log: a step shown or done, a hint shown, the chain skipped.</summary>
    public sealed class TutorialProgress
    {
        public string Step { get; }
        public string State { get; }

        public TutorialProgress(string step, string state)
        {
            Step = step;
            State = state;
        }
    }

    /// <summary>
    /// Onboarding (Docs/GDD/UI_UX.md §11): a guided chain — scavenge (and what the bar over the station means), feed,
    /// train Strength, equip the first skill, sleep when tired, first fight — plus side steps that wait for their
    /// moment (upgrade a station once there is junk for it, set the fighting order once there are two fighters) and
    /// one-shot contextual hints. A step shows when its prerequisites are done and its situation holds, and completes on
    /// the player's matching action; progress lives in the world (saved).
    /// </summary>
    public sealed class TutorialService : IDisposable
    {
        public const string Scavenge = "t_scavenge";
        public const string Bar = "t_bar";
        public const string Feed = "t_feed";
        public const string Train = "t_train";
        public const string Rummage = "t_rummage";
        public const string Equip = "t_skill";
        public const string Sleep = "t_sleep";
        public const string Fight = "t_fight";
        public const string Upgrade = "t_upgrade";
        public const string Order = "t_order";

        /// <summary>Marker: a training cycle finished (the player saw it work; time to show the dumpster).</summary>
        public const string Trained = "m_trained";

        /// <summary>Marker: some attribute levelled up (the gang got stronger, time for the first fight).</summary>
        public const string LevelledUp = "m_levelup";

        public const string HintJunk = "h_junk";
        /// <summary>Old relay hint (replaced by the <see cref="Order"/> step; the id stays in old saves).</summary>
        public const string HintRelay = "h_relay";
        public const string HintSlots = "h_slots";
        public const string HintTired = "h_tired";
        public const string HintOffline = "h_offline";

        /// <summary>One-shot tips in the first fights (shown by the combat screen).</summary>
        public const string TipGrit = "tip_grit";
        public const string TipSkill = "tip_skill";
        public const string TipHeal = "tip_heal";

        /// <summary>Energy below which the sleep step shows (the gang can still fight above it).</summary>
        public const int TiredEnergy = 20;

        private readonly GameSession _session;
        private readonly EventBus _bus;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private IReadOnlyList<TutorialStep> _steps = Array.Empty<TutorialStep>();

        /// <summary>Steps waiting for the player, the guided chain's first; each is shown where its target is.</summary>
        public IReadOnlyList<TutorialStep> Steps => _steps;
        public TutorialStep Current => _steps.Count > 0 ? _steps[0] : null;
        public event Action<IReadOnlyList<TutorialStep>> StepsChanged;
        public event Action<string> HintRequested;

        private IReadOnlyGameWorld World => _session.World;
        private GameConfig Config => _session.Config;
        private TutorialState State => _session.World.Tutorial;

        public TutorialService(GameSession session, EventBus bus)
        {
            _session = session;
            _bus = bus;
            _subscriptions.Add(bus.Subscribe<ActivityStarted>(OnActivityStarted));
            _subscriptions.Add(bus.Subscribe<ActivityCycleCompleted>(e =>
            {
                Done(Bar);
                if (e.Kind == ActivityKind.Train) Done(Trained);
                Evaluate();
            }));
            _subscriptions.Add(bus.Subscribe<ResourcesChanged>(e =>
            {
                if (e.FoodDelta < 0) Done(Feed); // only feeding spends food
                Evaluate();
            }));
            _subscriptions.Add(bus.Subscribe<SkillEquipped>(_ => { Done(Equip); Evaluate(); }));
            _subscriptions.Add(bus.Subscribe<BattleStarted>(e =>
            {
                Done(Fight);
                if (e.Battle.Order.Count >= 2) Done(Order);
                Evaluate();
            }));
            _subscriptions.Add(bus.Subscribe<BattleCompleted>(_ => Evaluate()));
            _subscriptions.Add(bus.Subscribe<UpgradePurchased>(_ => { Done(Upgrade); Evaluate(); }));
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
            _bus.Publish(new TutorialProgress(Current?.Id ?? "", "skip"));
            Evaluate();
        }

        /// <summary>The battle preparation opened: <paramref name="tiredCats"/> cats are too tired to fight (grey cards).</summary>
        public void OnPrepOpened(int availableCats, int tiredCats = 0)
        {
            if (tiredCats > 0)
                Hint(HintTired);
        }

        /// <summary>The player reordered the fighters (arrows or the best-order button).</summary>
        public void OnOrderChanged()
        {
            Done(Order);
            Evaluate();
        }

        /// <summary>A one-shot tip for the first fights: true (and remembered) the first time it is asked for.</summary>
        public bool TakeTip(string id)
        {
            if (State.Skipped || State.IsDone(id))
                return false;
            Done(id);
            return true;
        }

        /// <summary>
        /// Re-checks which steps are active and fires hints whose situation came up. Call once after subscribing to
        /// <see cref="StepsChanged"/> and <see cref="HintRequested"/>; afterwards domain events trigger it.
        /// </summary>
        public void Evaluate()
        {
            if (World.Junk > 0) Hint(HintJunk);
            if (World.Cats.Any(c => UnlockedSkills(c).Count() > Cat.SkillSlotCount)) Hint(HintSlots);
            // after the first fight: the gang keeps working while the game is closed
            if (!State.Skipped && IsDone(Fight) && World.PendingBattle == null) Hint(HintOffline);

            var next = State.Skipped ? (IReadOnlyList<TutorialStep>)Array.Empty<TutorialStep>() : FindSteps();
            if (next.SequenceEqual(_steps))
                return;
            foreach (var step in next.Where(s => !_steps.Contains(s)))
                _bus.Publish(new TutorialProgress(step.Id, "shown"));
            _steps = next;
            StepsChanged?.Invoke(next);
        }

        private List<TutorialStep> FindSteps()
        {
            var steps = new List<TutorialStep>();
            if (World.Cats.Count == 0)
                return steps;
            var chain = ChainStep();
            if (chain != null)
                steps.Add(chain);

            // a station upgrade, once the junk for one is there (after the basics)
            if (IsDone(Train) && !IsDone(Upgrade) && AffordableUpgrade() is UpgradeId upgrade)
                steps.Add(new TutorialStep(Upgrade, World.Cats[0].Id, upgrade: upgrade, soft: true));

            // the fighting order, once a second fighter is there (the first fight is fought alone)
            if (IsDone(Fight) && !IsDone(Order))
            {
                var rested = World.Cats.Where(c => c.Energy >= Config.Needs.MinEnergyForBattle).ToList();
                var target = Config.Territories.FirstOrDefault(t => World.Territories.Get(t.Id).Status == TerritoryStatus.Available);
                if (rested.Count >= 2)
                    steps.Add(new TutorialStep(Order, rested[1].Id, territory: target?.Id, soft: true));
            }

            return steps;
        }

        private TutorialStep ChainStep()
        {
            var cats = World.Cats;
            var first = cats[0];

            if (!IsDone(Scavenge))
                return new TutorialStep(Scavenge, first.Id);

            // the first cycle runs: what the bar over the station means
            if (!IsDone(Bar))
            {
                var working = cats.FirstOrDefault(c => c.Activity.IsWorking);
                if (working != null)
                    return new TutorialStep(Bar, working.Id, soft: true);
            }

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

            // junk for the upgrades: once a training cycle has shown how training works, and the cat has the energy to
            // go (a tired one is put to sleep first, else the step points at a card whose sheet cannot send it)
            if (!IsDone(Rummage) && IsDone(Trained) && first.Energy >= TiredEnergy)
                return new TutorialStep(Rummage, first.Id, soft: true);

            if (!IsDone(Equip))
            {
                foreach (var cat in cats)
                {
                    var skill = UnlockedSkills(cat).FirstOrDefault();
                    if (skill != null && Enumerable.Range(0, Cat.SkillSlotCount).All(i => cat.GetSkillInSlot(i) == null))
                        return new TutorialStep(Equip, cat.Id, skill.Id, soft: true);
                }
            }

            if (!IsDone(Sleep))
            {
                var tired = cats.FirstOrDefault(c => c.Energy < TiredEnergy && !IsDoing(c, ActivityKind.Sleep));
                if (tired != null)
                    return new TutorialStep(Sleep, tired.Id, soft: true);
            }

            if (!IsDone(Fight))
            {
                var target = Config.Territories.FirstOrDefault(t => World.Territories.Get(t.Id).Status == TerritoryStatus.Available);
                var ready = cats.Any(Rested);
                var trained = IsDone(Sleep) || IsDone(LevelledUp);
                if (target != null && ready && trained)
                    return new TutorialStep(Fight, first.Id, territory: target.Id, soft: true);
            }

            return null;
        }

        /// <summary>An upgrade the junk is enough for: the Strength station's first (the player trained there).</summary>
        private UpgradeId? AffordableUpgrade()
        {
            var affordable = Config.Upgrades.Where(u =>
            {
                var level = World.GetUpgradeLevel(u.Id);
                return level < u.MaxLevel && u.Costs[level] <= World.Junk;
            }).ToList();
            var strength = affordable.FirstOrDefault(u => u.Effect is TrainingXpBonusEffect t && t.Attribute == AttributeType.Strength);
            return (strength ?? affordable.FirstOrDefault())?.Id;
        }

        /// <summary>
        /// Rested enough to be led to the first fight: with energy to spare (a cat that keeps working would tire on the
        /// way to the battle preparation), or idle with just enough.
        /// </summary>
        private bool Rested(IReadOnlyCat cat)
        {
            var min = Config.Needs.MinEnergyForBattle;
            return cat.Energy >= min * 2 || (cat.Energy >= min && !cat.Activity.IsWorking);
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
                if (activity.Kind == ActivityKind.Rummage) Done(Rummage);
            }

            Evaluate();
        }

        private void Done(string id)
        {
            if (State.IsDone(id))
                return;
            _session.CompleteTutorialStep(id);
            _bus.Publish(new TutorialProgress(id, "done"));
        }

        private void Hint(string id)
        {
            if (State.IsDone(id))
                return;
            _session.CompleteTutorialStep(id);
            _bus.Publish(new TutorialProgress(id, "hint"));
            HintRequested?.Invoke(id);
        }

        public void Dispose()
        {
            foreach (var s in _subscriptions)
                s.Dispose();
        }
    }
}
