using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using TokenPet.Core;

namespace TokenPet.Infrastructure.Codex
{
    internal sealed class SessionUsageSource
    {
        private readonly string codexHome;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private readonly Dictionary<string, RolloutReader> files = new Dictionary<string, RolloutReader>(StringComparer.OrdinalIgnoreCase);
        private readonly byte[] buffer = new byte[64 * 1024];
        private readonly Pricing pricing;
        private DateTime currentDay;

        internal SessionUsageSource(string home, Pricing pricing)
        {
            codexHome = home;
            this.pricing = pricing;
        }

        internal Summary Scan(DateTime day, DateTimeOffset now)
        {
            if (day != currentDay)
            {
                foreach (var file in files.Values) file.Dispose();
                files.Clear();
                currentDay = day;
            }
            var result = new Summary { Unavailable = !Directory.Exists(codexHome) };
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filesToRead = new List<FileInfo>();
            DateTime oldest = day.AddDays(-7);
            foreach (string folder in new[] { "sessions", "archived_sessions" })
            {
                var dir = new DirectoryInfo(Path.Combine(codexHome, folder));
                if (!dir.Exists) continue;
                try
                {
                    filesToRead.AddRange(dir.EnumerateFiles("rollout-*.jsonl", SearchOption.AllDirectories)
                        .Where(file => file.LastWriteTime.Date >= oldest));
                }
                catch (IOException) { result.Unavailable = true; }
                catch (UnauthorizedAccessException) { result.Unavailable = true; }
            }

            WeeklyQuota latest = null;
            foreach (var file in filesToRead.OrderByDescending(file => file.LastWriteTimeUtc))
            {
                // Today's logs also supply token totals. Older logs are only needed for quota.
                if (file.LastWriteTime.Date < day && latest != null &&
                    file.LastWriteTimeUtc < latest.ObservedAt.UtcDateTime) break;

                seenPaths.Add(file.FullName);
                RolloutReader state;
                if (!files.TryGetValue(file.FullName, out state))
                    files.Add(file.FullName, state = new RolloutReader());
                try { state.Read(file, day, json, buffer); }
                catch (IOException) { result.Unavailable = true; }
                catch (UnauthorizedAccessException) { result.Unavailable = true; }

                if (state.Weekly != null && (latest == null || state.Weekly.ObservedAt > latest.ObservedAt))
                    latest = state.Weekly;
            }
            if (latest != null && latest.IsCurrent(now)) result.Weekly = latest;

            if (!result.Unavailable)
            {
                foreach (string path in files.Keys.Where(path => !seenPaths.Contains(path)).ToArray())
                {
                    files[path].Dispose();
                    files.Remove(path);
                }
            }

            UsageTotals.Accumulate(result, files.Values.SelectMany(file => file.Samples), pricing);
            return result;
        }
    }
}
