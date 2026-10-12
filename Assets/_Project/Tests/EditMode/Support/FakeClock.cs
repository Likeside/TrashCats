using TrashCats.App;

namespace TrashCats.Tests
{
    public sealed class FakeClock : IClock
    {
        public long UtcNowMs { get; set; }

        public FakeClock(long start = 1_000_000) => UtcNowMs = start;

        public void Advance(long ms) => UtcNowMs += ms;
    }

    public sealed class RecordingSaveRequests : ISaveRequests
    {
        public int Deferred;
        public int Immediate;

        public void MarkDirty(SaveUrgency urgency)
        {
            if (urgency == SaveUrgency.Immediate) Immediate++;
            else Deferred++;
        }
    }
}
