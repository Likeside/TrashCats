using System;
using TrashCats.App;

namespace TrashCats.Infrastructure
{
    public sealed class SystemClock : IClock
    {
        public long UtcNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
