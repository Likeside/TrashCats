using System;

namespace TrashCats.App
{
    /// <summary>
    /// Decides when to save: immediately for important changes, otherwise at most once per throttle interval
    /// while there are unsaved changes. Saving errors are reported, never thrown into gameplay.
    /// </summary>
    public sealed class SaveScheduler : ISaveRequests
    {
        private readonly SaveService _service;
        private readonly IClock _clock;
        private readonly long _throttleMs;
        private GameSession _session;
        private Func<SettingsSave> _settings;
        private bool _dirty;
        private bool _saving;
        private long _lastSaveUtcMs;

        public event Action<Exception> SaveFailed;

        public bool IsDirty => _dirty;

        public SaveScheduler(SaveService service, IClock clock, long throttleMs = 5_000)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _throttleMs = throttleMs;
        }

        public void Attach(GameSession session, Func<SettingsSave> settings)
        {
            _session = session;
            _settings = settings;
            _lastSaveUtcMs = _clock.UtcNowMs;
        }

        public void MarkDirty(SaveUrgency urgency)
        {
            _dirty = true;
            if (urgency == SaveUrgency.Immediate)
                SaveNow();
        }

        /// <summary>Call every frame.</summary>
        public void Update()
        {
            if (_dirty && _clock.UtcNowMs - _lastSaveUtcMs >= _throttleMs)
                SaveNow();
        }

        public void SaveNow()
        {
            if (_session == null || _saving)
                return;

            _saving = true;
            try
            {
                _service.Save(_session, _settings?.Invoke());
                _dirty = false;
            }
            catch (Exception e)
            {
                SaveFailed?.Invoke(e);
            }
            finally
            {
                _lastSaveUtcMs = _clock.UtcNowMs;
                _saving = false;
            }
        }
    }
}
