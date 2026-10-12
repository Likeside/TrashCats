using System;
using System.Collections.Generic;
using TrashCats.Domain;

namespace TrashCats.App
{
    public enum SaveSource
    {
        Main,
        Backup,
        NewGame,
    }

    public sealed class LoadOutcome
    {
        public LoadedGame Game { get; }
        public SaveSource Source { get; }

        /// <summary>Why the main (and backup) save could not be used, if so.</summary>
        public IReadOnlyList<string> Problems { get; }

        public LoadOutcome(LoadedGame game, SaveSource source, IReadOnlyList<string> problems)
        {
            Game = game;
            Source = source;
            Problems = problems;
        }
    }

    /// <summary>Loads and writes saves: main file, then backup, then a new game.</summary>
    public sealed class SaveService
    {
        private readonly ISaveStorage _storage;
        private readonly GameConfig _config;
        private readonly IClock _clock;
        private readonly bool _indented;

        public SaveService(ISaveStorage storage, GameConfig config, IClock clock, bool indented)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _indented = indented;
        }

        public LoadOutcome LoadOrCreate()
        {
            var problems = new List<string>();

            var main = _storage.ReadMain();
            if (main != null && TryLoad(main, "main", problems, out var game))
                return new LoadOutcome(game, SaveSource.Main, problems);

            var backup = _storage.ReadBackup();
            if (backup != null && TryLoad(backup, "backup", problems, out game))
                return new LoadOutcome(game, SaveSource.Backup, problems);

            return new LoadOutcome(NewGame(), SaveSource.NewGame, problems);
        }

        public LoadedGame NewGame()
        {
            var now = _clock.UtcNowMs;
            var seed = unchecked((int)(now ^ (now >> 32)) ^ Environment.TickCount);
            return new LoadedGame(GameWorld.CreateNew(_config, seed), now, new SettingsSave(), Array.Empty<string>());
        }

        /// <summary>Writes the session state. The session is caught up first so no elapsed time is lost.</summary>
        public void Save(GameSession session, SettingsSave settings)
        {
            session.Tick();
            var data = SaveMapper.ToSave(session.World, session.LastTickUtcMs, settings);
            _storage.Write(SaveJson.Serialize(data, _indented));
        }

        public void DeleteAll() => _storage.DeleteAll();

        private bool TryLoad(string json, string label, List<string> problems, out LoadedGame game)
        {
            game = null;
            try
            {
                game = SaveMapper.FromSave(SaveJson.Deserialize(json), _config);
                return true;
            }
            catch (SaveFromFutureException e)
            {
                problems.Add($"{label}: {e.Message}");
                _storage.WriteAside($"save.future.v{e.Version}.json", json);
            }
            catch (Exception e)
            {
                problems.Add($"{label}: {e.GetType().Name}: {e.Message}");
            }

            return false;
        }
    }
}
