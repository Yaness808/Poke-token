using System;
using System.Globalization;
using System.Collections.Generic;
using TokenPet.Core;

namespace TokenPet.Infrastructure.Codex
{
    using Json = Dictionary<string, object>;

    internal static class QuotaParser
    {
        private const int WeeklyWindowMinutes = 7 * 24 * 60;

        internal static WeeklyQuota Read(Json snapshot, DateTimeOffset observed)
        {
            if (snapshot == null) return null;
            var limitId = Log.String(snapshot, "limit_id");
            if (limitId != "" && limitId != "codex") return null;
            return ReadWindow(Log.Object(snapshot, "primary"), observed) ??
                ReadWindow(Log.Object(snapshot, "secondary"), observed);
        }

        private static WeeklyQuota ReadWindow(Json window, DateTimeOffset observed)
        {
            if (Log.Number(window, "window_minutes") != WeeklyWindowMinutes) return null;

            double used;
            var raw = Convert.ToString(Log.Value(window, "used_percent"), CultureInfo.InvariantCulture);
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out used) ||
                double.IsNaN(used) || double.IsInfinity(used) || used < 0) return null;

            long seconds = Log.Number(window, "resets_at");
            if (seconds <= 0 || seconds > (DateTimeOffset.MaxValue - WeeklyQuota.UnixEpoch).TotalSeconds)
                return null;
            var resetsAt = WeeklyQuota.UnixEpoch.AddSeconds(seconds);
            if (resetsAt <= observed || (resetsAt - observed).TotalMinutes > WeeklyWindowMinutes + 5) return null;
            return new WeeklyQuota(observed, resetsAt, used);
        }
    }
}
