using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows;
using TokenPet.Core;
using TokenPet.Desktop.Views;
using TokenPet.Infrastructure.Codex;
using TokenPet.Infrastructure.Configuration;
using TokenPet.Infrastructure.Windows;

namespace TokenPet.Tests
{
    internal static class SmokeTests
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        private static int Main()
        {
            string directory = Path.Combine(Path.GetTempPath(), "jieni-token-fixture-" + Guid.NewGuid().ToString("N"));
            string sessions = Path.Combine(directory, "sessions");
            string log = Path.Combine(sessions, "rollout-test.jsonl");
            string settings = Path.Combine(directory, "settings.json");
            Directory.CreateDirectory(sessions);
            try
            {
                TestUsage(directory, log);
                TestPlacement(settings);
                Console.WriteLine("PASS: weekly quota, midnight rollover, incremental tokens, drag, scale, and saved settings.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            finally
            {
                File.Delete(log);
                File.Delete(settings);
                File.Delete(settings + ".tmp");
                Directory.Delete(sessions);
                Directory.Delete(directory);
            }
        }

        private static void TestUsage(string directory, string log)
        {
            var midnight = new DateTimeOffset(DateTime.Today);
            var previous = midnight.AddSeconds(-10);
            var reset = midnight.AddDays(4);
            var week = new { used_percent = 30, window_minutes = 10080, resets_at = UnixSeconds(reset) };
            File.WriteAllText(log, Row(previous, new {
                type = "token_count",
                info = new { total_token_usage = new { input_tokens = 1000, cached_input_tokens = 800, output_tokens = 10 } },
                rate_limits = new { limit_id = "codex", primary = week }
            }));
            File.SetLastWriteTimeUtc(log, previous.UtcDateTime);
            var source = new SessionUsageSource(directory, new Pricing(new Dictionary<string, ModelRate>()));
            var beforeMidnight = source.Scan(previous.Date, previous.AddSeconds(1));
            Require(beforeMidnight.Tokens.Total == 1010, "Previous day's token count.");
            Require(beforeMidnight.Weekly != null && beforeMidnight.Weekly.RemainingPercent == 70, "Weekly primary.");

            var nextDay = source.Scan(midnight.Date, midnight);
            Require(nextDay.Tokens.Total == 0 && nextDay.Weekly != null, "Midnight resets daily totals and preserves the active weekly window.");
            var second = midnight.AddSeconds(5);
            var updatedWeek = new { used_percent = 31.5, window_minutes = 10080, resets_at = UnixSeconds(reset) };
            var fiveHours = new { used_percent = 60, window_minutes = 300, resets_at = UnixSeconds(midnight.AddHours(5)) };
            File.AppendAllText(log, Row(second, new {
                type = "token_count",
                info = new { total_token_usage = new { input_tokens = 1200, cached_input_tokens = 950, output_tokens = 25 } }
            }) + Row(second.AddSeconds(1), new {
                type = "token_count",
                rate_limits = new { limit_id = "codex", primary = fiveHours, secondary = updatedWeek }
            }) + Row(second.AddSeconds(2), new {
                type = "token_count",
                rate_limits = new { limit_id = "another_bucket", primary = week }
            }));
            File.SetLastWriteTimeUtc(log, second.AddSeconds(2).UtcDateTime);
            var after = source.Scan(midnight.Date, second.AddSeconds(3));
            Require(after.Tokens.Total == 215 && after.Tokens.Cached == 150, "Same conversation after midnight.");
            Require(after.Weekly != null && after.Weekly.RemainingPercent == 68.5, "Quota-only event in secondary, ignoring other buckets.");
            Require(source.Scan(midnight.Date, reset).Weekly == null, "Expired quota.");
        }

        private static void TestPlacement(string path)
        {
            var preferences = new Preferences();
            var area = new DesktopWindows.Rect { Left = 0, Top = 0, Right = 1500, Bottom = 900 };
            var bounds = new DesktopWindows.Rect { Left = 100, Top = 100, Right = 589, Bottom = 250 };
            var origin = new DesktopWindows.ScreenPoint { X = 150, Y = 125 };
            var move = new WindowGesture(origin, bounds, 1.5, 1, false);
            Require(!move.HasMoved(origin), "Click stays distinct from dragging.");
            var end = new DesktopWindows.ScreenPoint { X = 90, Y = 155 };
            Require(move.HasMoved(end), "Recognizes a drag at 150% DPI.");
            move.Place(end, area, new Size(326, 100), preferences);
            Require(Math.Abs(preferences.RightInset - (1460 / 1.5 - 326)) < 0.001 &&
                Math.Abs(preferences.BottomInset - (770 / 1.5 - 100)) < 0.001, "Position uses DPI-adjusted insets.");

            var resize = new WindowGesture(origin, bounds, 1.5, 1, true);
            var smaller = new DesktopWindows.ScreenPoint { X = 101, Y = 110 };
            preferences.Scale = resize.ResizeTo(smaller);
            Require(Math.Abs(preferences.Scale - 0.9) < 0.001, "Proportional resize.");
            var farAway = new DesktopWindows.ScreenPoint { X = 2000, Y = 2000 };
            Require(resize.ResizeTo(farAway) == Preferences.MaxScale, "Maximum size.");
            preferences.Save(path);
            preferences.MascotPath = "example.png";
            preferences.Save(path);
            var restored = Preferences.Load(path);
            Require(restored.Scale == preferences.Scale && restored.RightInset == preferences.RightInset &&
                restored.BottomInset == preferences.BottomInset && restored.MascotPath == "example.png", "Reload saved settings.");
        }

        private static string Row(DateTimeOffset timestamp, object payload)
        {
            return Json.Serialize(new { type = "event_msg", timestamp = timestamp.ToString("o"), payload }) + "\n";
        }

        private static long UnixSeconds(DateTimeOffset time)
        {
            return (long)(time - WeeklyQuota.UnixEpoch).TotalSeconds;
        }

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }
    }
}
