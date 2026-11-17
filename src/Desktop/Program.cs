using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using TokenPet.Desktop.Views;
using TokenPet.Infrastructure.Codex;
using TokenPet.Infrastructure.Configuration;
using TokenPet.Infrastructure.Windows;

namespace TokenPet.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;
            if (args.Contains("--preview"))
            {
                int id;
                if (args.Length < 2 || !int.TryParse(args[1], out id)) id = 4;
                PetView.Preview(Path.Combine(folder, "浮窗预览.png"), id, args.Contains("details"));
                return 0;
            }

            string codexDirectory = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (string.IsNullOrWhiteSpace(codexDirectory))
                codexDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            var usage = new SessionUsageSource(codexDirectory, RateCatalog.Load(Path.Combine(folder, "config", "rates.json")));
            if (args.Contains("--report")) return WriteReport(args, usage);

            bool created;
            using (var instance = new Mutex(true, @"Local\CodexTokenPet-1", out created))
            {
                if (!created) return 0;
                string preferencesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "JieniToken", "settings.json");
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var window = new OverlayWindow(usage, preferencesPath, codexDirectory);
                app.Dispatcher.BeginInvoke(new Action(window.Start));
                return app.Run();
            }
        }

        private static int WriteReport(string[] args, SessionUsageSource usage)
        {
            var now = DateTimeOffset.Now;
            var summary = usage.Scan(now.Date, now);
            string path = args.Length == 2 ? args[1] : Path.Combine(Path.GetTempPath(), "codex-token-pet-report.json");
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(new {
                input = summary.Tokens.Input,
                cached = summary.Tokens.Cached,
                output = summary.Tokens.Output,
                usd = summary.Dollars,
                credits = summary.Credits,
                unpriced = summary.Unpriced,
                unavailable = summary.Unavailable,
                latestModel = summary.Latest == null ? null : summary.Latest.Model,
                weeklyRemaining = summary.Weekly == null ? (double?)null : summary.Weekly.RemainingPercent,
                weeklyReset = summary.Weekly == null ? null : summary.Weekly.ResetsAt.ToString("o"),
                desktop = DesktopWindows.Report()
            }));
            return summary.Unavailable ? 1 : 0;
        }
    }
}
