using TrashCats.App;
using TrashCats.Domain;

namespace TrashCats.Presentation.Bootstrap
{
    /// <summary>Everything the Game scene needs, created once by <see cref="GameBootstrap"/>.</summary>
    public sealed class GameServices
    {
        public GameConfig Config { get; }
        public IClock Clock { get; }
        public EventBus Bus { get; }
        public GameSession Session { get; }
        public SaveScheduler SaveScheduler { get; }
        public SettingsSave Settings { get; }
        public DebugCommands Debug { get; }

        public GameServices(GameConfig config, IClock clock, EventBus bus, GameSession session, SaveScheduler saveScheduler,
            SettingsSave settings, DebugCommands debug)
        {
            Config = config;
            Clock = clock;
            Bus = bus;
            Session = session;
            SaveScheduler = saveScheduler;
            Settings = settings;
            Debug = debug;
        }
    }
}
