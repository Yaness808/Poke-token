using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace TokenPet.Infrastructure.Codex
{
    internal sealed class CodexInventoryClient
    {
        private readonly string codexHome;
        private readonly string userProfile;

        internal CodexInventoryClient(string codexHome, string userProfile)
        {
            this.codexHome = codexHome;
            this.userProfile = userProfile;
        }

        internal async Task<Dictionary<int, Dictionary<string, object>>> ReadAsync(CancellationToken cancellation)
        {
            var results = new Dictionary<int, Dictionary<string, object>>();
            var executable = FindExecutable();
            if (executable == null) return results;
            var options = new ProcessStartInfo {
                FileName = executable, Arguments = "app-server --listen stdio://",
                WorkingDirectory = userProfile, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            options.EnvironmentVariables["CODEX_HOME"] = codexHome;
            var json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };

            using (var process = new Process { StartInfo = options })
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                try
                {
                    if (!process.Start()) return results;
                    deadline.CancelAfter(TimeSpan.FromSeconds(12));
                    using (deadline.Token.Register(() => Stop(process)))
                    {
                        process.ErrorDataReceived += (s, e) => { };
                        process.BeginErrorReadLine();
                        var input = process.StandardInput;
                        await input.WriteLineAsync(json.Serialize(new { method = "initialize", id = 1, @params = new {
                            clientInfo = new { name = "token-pet", version = "1.0" },
                            capabilities = new { experimentalApi = true }
                        }})).ConfigureAwait(false);
                        await input.FlushAsync().ConfigureAwait(false);
                        await ReadReplies(process.StandardOutput, json, results, 1).ConfigureAwait(false);
                        if (!results.ContainsKey(1)) return results;

                        await input.WriteLineAsync("{\"method\":\"initialized\",\"params\":{}}").ConfigureAwait(false);
                        await input.WriteLineAsync("{\"method\":\"plugin/installed\",\"id\":2,\"params\":{}}").ConfigureAwait(false);
                        await input.WriteLineAsync("{\"method\":\"skills/list\",\"id\":3,\"params\":{\"forceReload\":true}}").ConfigureAwait(false);
                        await input.WriteLineAsync("{\"method\":\"config/read\",\"id\":4,\"params\":{\"includeLayers\":false}}").ConfigureAwait(false);
                        await input.FlushAsync().ConfigureAwait(false);
                        await ReadReplies(process.StandardOutput, json, results, 2, 3, 4).ConfigureAwait(false);
                    }
                }
                catch (IOException) { }
                catch (Win32Exception) { }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
                finally { Stop(process); }
            }
            cancellation.ThrowIfCancellationRequested();
            return results;
        }

        private static async Task ReadReplies(StreamReader output, JavaScriptSerializer json,
            Dictionary<int, Dictionary<string, object>> results, params int[] requestIds)
        {
            var pending = new HashSet<int>(requestIds);
            while (pending.Count > 0)
            {
                string line = await output.ReadLineAsync().ConfigureAwait(false);
                if (line == null) return;
                var message = json.Deserialize<Dictionary<string, object>>(line);
                object id, result;
                if (message != null && message.TryGetValue("id", out id) && id is int && pending.Remove((int)id) &&
                    message.TryGetValue("result", out result) && result is Dictionary<string, object>)
                {
                    var payload = (Dictionary<string, object>)result;
                    if ((int)id == 4)
                    {
                        object config, skills;
                        payload = payload.TryGetValue("config", out config) && config is Dictionary<string, object> &&
                            ((Dictionary<string, object>)config).TryGetValue("skills", out skills)
                            ? skills as Dictionary<string, object> : null;
                    }
                    results.Add((int)id, payload);
                }
            }
        }

        private string FindExecutable()
        {
            foreach (var app in Process.GetProcessesByName("ChatGPT"))
            {
                using (app)
                {
                    try
                    {
                        var path = Path.Combine(Path.GetDirectoryName(app.MainModule.FileName), "resources", "codex.exe");
                        if (File.Exists(path)) return path;
                    }
                    catch (Win32Exception) { }
                    catch (InvalidOperationException) { }
                }
            }
            var installed = Path.Combine(codexHome, "plugins", ".plugin-appserver", "codex.exe");
            if (File.Exists(installed)) return installed;
            return (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                .Select(folder => Path.Combine(folder.Trim('"'), "codex.exe"))
                .FirstOrDefault(path => Path.IsPathRooted(path) && File.Exists(path));
        }

        private static void Stop(Process process)
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
        }
    }
}
