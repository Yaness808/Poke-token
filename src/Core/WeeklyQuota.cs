using System;

namespace TokenPet.Core
{
    internal sealed class WeeklyQuota
    {
        internal static readonly DateTimeOffset UnixEpoch =
            new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        internal DateTimeOffset ObservedAt { get; private set; }
        internal DateTimeOffset ResetsAt { get; private set; }
        internal double RemainingPercent { get; private set; }

        internal WeeklyQuota(DateTimeOffset observedAt, DateTimeOffset resetsAt, double usedPercent)
        {
            ObservedAt = observedAt;
            ResetsAt = resetsAt;
            RemainingPercent = Math.Max(0, 100 - usedPercent);
        }

        internal bool IsCurrent(DateTimeOffset now)
        {
            return ObservedAt <= now && now < ResetsAt;
        }
    }
}
