using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace TokenPet.Infrastructure.Codex
{
    internal sealed class ExtensionEntry
    {
        internal const string Enabled = "已启用";
        internal const string Disabled = "已停用";
        internal const string Removed = "已卸载";
        internal const string Unknown = "待确认";
        internal const string Plugin = "插件";
        internal const string Skill = "技能";

        public string Key { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public string Status { get; set; }
        public string PluginKey { get; set; }
        public string FilePath { get; set; }
        public bool Remote { get; set; }

        internal ExtensionEntry WithStatus(string status)
        {
            var entry = (ExtensionEntry)MemberwiseClone();
            entry.Status = status;
            return entry;
        }
    }

    internal sealed class ExtensionSnapshot
    {
        internal ExtensionEntry[] Entries;
        internal bool Complete;
        internal string Signature;
    }

    internal sealed class ExtensionInventory
    {
        private readonly string cache;
        private readonly string historyPath;
        private readonly CodexInventoryClient client;
        private readonly object snapshotLock = new object();
        private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
        private Dictionary<string, ExtensionEntry> history;
        private string savedHistory;

        internal ExtensionInventory(string codexHome, string userProfile, string historyPath)
        {
            this.historyPath = historyPath;
            cache = Path.GetFullPath(Path.Combine(codexHome, "plugins", "cache"));
            client = new CodexInventoryClient(codexHome, userProfile);
        }

        internal async Task<ExtensionSnapshot> RefreshAsync(CancellationToken cancellation)
        {
            var replies = await client.ReadAsync(cancellation).ConfigureAwait(false);
            lock (snapshotLock)
            {
                cancellation.ThrowIfCancellationRequested();
                return ReadSnapshot(replies);
            }
        }

        internal ExtensionSnapshot ReadSnapshot(Dictionary<int, Dictionary<string, object>> replies)
        {
            if (history == null) LoadHistory();
            var entries = new Dictionary<string, ExtensionEntry>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, object> plugins, skills, skillConfig;
            replies.TryGetValue(2, out plugins);
            replies.TryGetValue(3, out skills);
            replies.TryGetValue(4, out skillConfig);
            bool pluginsComplete = Get(plugins, "marketplaces") is IEnumerable &&
                !Objects(plugins, "marketplaceLoadErrors").Any();
            var skillScopes = Objects(skills, "data").ToArray();
            bool skillsComplete = skillScopes.Length > 0 && skillScopes.All(scope =>
                Get(scope, "skills") is IEnumerable && !Objects(scope, "errors").Any());

            foreach (var market in Objects(plugins, "marketplaces"))
                foreach (var plugin in Objects(market, "plugins"))
                    AddPlugin(entries, Text(market, "name"), plugin);
            ApplyBundledSkillRules(entries, Objects(skillConfig, "config"));
            foreach (var skill in skillScopes.SelectMany(scope => Objects(scope, "skills")))
                AddRuntimeSkill(entries, skill);

            bool remoteListed = entries.Values.Any(entry => entry.Kind == ExtensionEntry.Plugin && entry.Remote);
            foreach (var prior in history.Values.ToArray())
            {
                if (entries.ContainsKey(prior.Key)) continue;
                bool removed = prior.Status == ExtensionEntry.Removed ||
                    CanConfirmRemoval(prior, entries, pluginsComplete, skillsComplete, remoteListed);
                entries.Add(prior.Key, prior.WithStatus(removed ? ExtensionEntry.Removed : ExtensionEntry.Unknown));
            }
            foreach (var entry in entries.Values)
                if (entry.Status != ExtensionEntry.Unknown) history[entry.Key] = entry;
            SaveHistory();
            var ordered = entries.Values
                .OrderBy(entry => entry.Status == ExtensionEntry.Removed)
                .ThenBy(entry => entry.Kind == ExtensionEntry.Skill)
                .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).ToArray();
            return new ExtensionSnapshot {
                Entries = ordered,
                Complete = pluginsComplete && skillsComplete && !ordered.Any(entry => entry.Status == ExtensionEntry.Unknown),
                Signature = json.Serialize(ordered)
            };
        }

        private bool CanConfirmRemoval(ExtensionEntry prior, Dictionary<string, ExtensionEntry> current,
            bool pluginsComplete, bool skillsComplete, bool remoteListed)
        {
            if (prior.Kind == ExtensionEntry.Plugin)
                return pluginsComplete && (!prior.Remote || remoteListed || FindPluginRoot(prior.PluginKey, "") == null);
            if (string.IsNullOrEmpty(prior.PluginKey))
                return skillsComplete && (string.IsNullOrEmpty(prior.FilePath) || !File.Exists(prior.FilePath));
            ExtensionEntry plugin;
            if (current.TryGetValue(prior.PluginKey, out plugin))
                return !string.IsNullOrEmpty(plugin.FilePath) && !string.IsNullOrEmpty(prior.FilePath) &&
                    (!prior.FilePath.StartsWith(plugin.FilePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        || !File.Exists(prior.FilePath));
            return pluginsComplete && (!prior.Remote || remoteListed || FindPluginRoot(prior.PluginKey, "") == null);
        }

        private void AddPlugin(Dictionary<string, ExtensionEntry> entries, string market, Dictionary<string, object> plugin)
        {
            string name = Text(plugin, "name");
            string key = Prefer(Text(plugin, "id"), name + "@" + market);
            if (string.IsNullOrWhiteSpace(name) || !Flag(plugin, "installed")) return;
            var display = AsObject(Get(plugin, "interface"));
            var source = AsObject(Get(plugin, "source"));
            string root = FindPluginRoot(key, Prefer(Text(plugin, "localVersion"), Text(plugin, "version")));
            var manifest = ReadManifest(root);
            var manifestDisplay = AsObject(Get(manifest, "interface"));
            bool remote = Text(source, "type") == "remote" || !string.IsNullOrEmpty(Text(plugin, "remotePluginId"));
            var entry = new ExtensionEntry {
                Key = key, PluginKey = key, Kind = ExtensionEntry.Plugin,
                Name = Prefer(Text(display, "displayName"), Text(manifestDisplay, "displayName"), name),
                Version = Prefer(Text(plugin, "localVersion"), Text(plugin, "version"), Text(manifest, "version")),
                Description = Describe(Text(display, "shortDescription"), Text(manifest, "description"),
                    Text(display, "longDescription")),
                Source = remote ? "Codex 账户" : market,
                Status = Flag(plugin, "enabled") ? ExtensionEntry.Enabled : ExtensionEntry.Disabled,
                FilePath = root, Remote = remote
            };
            entries[key] = entry;
            if (root == null) return;

            object declared = Get(manifest, "skills");
            var roots = declared is string ? new[] { (string)declared } :
                (declared as IEnumerable ?? new object[0]).OfType<string>();
            foreach (string directory in roots.Concat(new[] { "skills" }))
                foreach (var file in BundledSkills(ChildPath(root, directory), 5))
                    AddBundledSkill(entries, entry, root, file);
        }

        private void AddBundledSkill(Dictionary<string, ExtensionEntry> entries, ExtensionEntry plugin, string root, string file)
        {
            string relative = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var entry = new ExtensionEntry {
                Key = plugin.Key + "/" + relative, PluginKey = plugin.Key, Kind = ExtensionEntry.Skill,
                Name = Prefer(ReadFrontmatter(file, "name"), Path.GetFileName(Path.GetDirectoryName(file))),
                Version = ReadSkillVersion(file),
                Description = Describe(ReadFrontmatter(file, "description")),
                Source = plugin.Name, Status = plugin.Status, FilePath = file, Remote = plugin.Remote
            };
            entries[entry.Key] = entry;
        }

        private void AddRuntimeSkill(Dictionary<string, ExtensionEntry> entries, Dictionary<string, object> skill)
        {
            string name = Text(skill, "name");
            string path = Text(skill, "path");
            string scope = Text(skill, "scope");
            if (string.IsNullOrWhiteSpace(name)) return;
            var display = AsObject(Get(skill, "interface"));
            string pluginKey;
            string key = GetCachedSkillKey(path, out pluginKey) ?? ("skill:" + Prefer(path, scope + ":" + name));
            ExtensionEntry entry;
            if (!entries.TryGetValue(key, out entry))
            {
                ExtensionEntry parent = null;
                if (pluginKey != null && !entries.TryGetValue(pluginKey, out parent)) history.TryGetValue(pluginKey, out parent);
                entry = new ExtensionEntry {
                    Key = key, Kind = ExtensionEntry.Skill, FilePath = path, PluginKey = pluginKey,
                    Source = parent != null ? parent.Name : pluginKey != null ? pluginKey.Split('@')[0] :
                        scope == "system" ? "Codex 内置" : scope == "repo" ? "项目目录" : "本机技能",
                    Status = parent == null ? null : parent.Status,
                    Remote = parent != null && parent.Remote
                };
                entries[key] = entry;
            }
            entry.Name = Prefer(Text(display, "displayName"), entry.Name, name);
            if (string.IsNullOrEmpty(entry.Version)) entry.Version = Prefer(Text(skill, "version"), ReadSkillVersion(path));
            entry.Description = Describe(Text(display, "shortDescription"), Text(skill, "description"), entry.Description);
            entry.Status = entry.Status == ExtensionEntry.Disabled || !Flag(skill, "enabled")
                ? ExtensionEntry.Disabled : ExtensionEntry.Enabled;
        }

        private string GetCachedSkillKey(string file, out string pluginKey)
        {
            pluginKey = null;
            string prefix = cache + Path.DirectorySeparatorChar;
            if (string.IsNullOrEmpty(file) || !file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            var parts = file.Substring(prefix.Length).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // The cache is organized as marketplace / plugin / version / skill path.
            if (parts.Length < 4) return null;
            pluginKey = parts[1] + "@" + parts[0];
            return pluginKey + "/" + string.Join(Path.DirectorySeparatorChar.ToString(), parts.Skip(3));
        }

        private static void ApplyBundledSkillRules(Dictionary<string, ExtensionEntry> entries,
            IEnumerable<Dictionary<string, object>> rules)
        {
            var configured = rules.ToArray();
            if (configured.Length == 0) return;
            foreach (var entry in entries.Values.Where(item => item.Kind == ExtensionEntry.Skill))
            {
                string qualifiedName = entry.PluginKey.Split('@')[0] + ":" + entry.Name;
                var rule = configured.LastOrDefault(candidate =>
                    string.Equals(Text(candidate, "path"), entry.FilePath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Text(candidate, "name"), entry.Name, StringComparison.Ordinal) ||
                    string.Equals(Text(candidate, "name"), qualifiedName, StringComparison.Ordinal));
                if (Equals(Get(rule, "enabled"), false)) entry.Status = ExtensionEntry.Disabled;
            }
        }

        private string FindPluginRoot(string key, string version)
        {
            int separator = key.LastIndexOf('@');
            if (separator <= 0) return null;
            string market = ChildPath(cache, key.Substring(separator + 1));
            string installed = ChildPath(market, key.Substring(0, separator));
            string exact = ChildPath(installed, version);
            if (exact != null && Directory.Exists(exact)) return exact;
            return Directories(installed).OrderByDescending(Directory.GetLastWriteTimeUtc).FirstOrDefault();
        }

        private Dictionary<string, object> ReadManifest(string directory)
        {
            if (directory == null) return null;
            foreach (var relative in new[] { ".codex-plugin/plugin.json", "plugin.json", ".claude-plugin/plugin.json" })
            {
                string data = ReadFile(Path.Combine(directory, relative));
                if (data == null) continue;
                try { return json.Deserialize<Dictionary<string, object>>(data); }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
            }
            return null;
        }

        private static IEnumerable<string> BundledSkills(string directory, int remainingDepth)
        {
            if (directory == null || !Directory.Exists(directory)) yield break;
            string skill = Path.Combine(directory, "SKILL.md");
            if (File.Exists(skill)) yield return skill;
            else if (remainingDepth > 0)
                foreach (var child in Directories(directory))
                    foreach (var file in BundledSkills(child, remainingDepth - 1))
                        yield return file;
        }

        private static string[] Directories(string path)
        {
            if (path == null) return new string[0];
            try { return Directory.GetDirectories(path); }
            catch (IOException) { return new string[0]; }
            catch (UnauthorizedAccessException) { return new string[0]; }
        }

        private static string ChildPath(string parent, string relative)
        {
            if (parent == null || string.IsNullOrWhiteSpace(relative)) return null;
            try
            {
                string root = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string path = Path.GetFullPath(Path.Combine(root, relative));
                return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path : null;
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (IOException) { return null; }
        }

        private static string ReadSkillVersion(string file)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return "";
            return ReadFrontmatter(file, "version", true);
        }

        private static string ReadFrontmatter(string file, string field, bool allowIndent = false)
        {
            string data = ReadFile(file);
            if (data == null) return "";
            using (var reader = new StringReader(data))
            {
                if (reader.ReadLine() != "---") return "";
                string line;
                while ((line = reader.ReadLine()) != null && line.TrimEnd() != "---")
                {
                    string property = allowIndent ? line.TrimStart() : line;
                    if (!property.StartsWith(field + ":", StringComparison.Ordinal)) continue;
                    string value = property.Substring(field.Length + 1).Trim();
                    if (allowIndent) return value.Split('#')[0].Trim().Trim('"', '\'');
                    if (value.StartsWith(">") || value.StartsWith("|")) value = "";
                    while ((line = reader.ReadLine()) != null && (line.Length == 0 || char.IsWhiteSpace(line[0])))
                        value += " " + line.Trim();
                    return value.Trim().Trim('"', '\'');
                }
            }
            return "";
        }

        private static string ReadFile(string path)
        {
            try
            {
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream))
                    return stream.Length <= 16 * 1024 * 1024 ? reader.ReadToEnd() : null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private void LoadHistory()
        {
            history = new Dictionary<string, ExtensionEntry>(StringComparer.OrdinalIgnoreCase);
            savedHistory = ReadFile(historyPath);
            if (savedHistory == null) return;
            try
            {
                var saved = json.Deserialize<ExtensionEntry[]>(savedHistory);
                if (saved == null) return;
                foreach (var entry in saved)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Key)) continue;
                    if (entry.Kind == ExtensionEntry.Skill && string.IsNullOrEmpty(entry.PluginKey))
                    {
                        string pluginKey;
                        string key = GetCachedSkillKey(entry.FilePath, out pluginKey);
                        if (key != null)
                        {
                            if (history.ContainsKey(key)) continue;
                            entry.Key = key;
                            entry.PluginKey = pluginKey;
                            entry.Source = pluginKey.Split('@')[0];
                        }
                    }
                    history[entry.Key] = entry;
                }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
        }

        private void SaveHistory()
        {
            if (history.Count == 0) return;
            string next = json.Serialize(history.Values.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).ToArray());
            if (next == savedHistory) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(historyPath));
                string staging = historyPath + ".tmp";
                File.WriteAllText(staging, next);
                if (File.Exists(historyPath)) File.Replace(staging, historyPath, null);
                else File.Move(staging, historyPath);
                savedHistory = next;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static Dictionary<string, object> AsObject(object value) { return value as Dictionary<string, object>; }
        private static object Get(Dictionary<string, object> obj, string key)
        {
            object value;
            return obj != null && obj.TryGetValue(key, out value) ? value : null;
        }
        private static string Text(Dictionary<string, object> obj, string key) { return Get(obj, key) as string ?? ""; }
        private static bool Flag(Dictionary<string, object> obj, string key) { return Equals(Get(obj, key), true); }
        private static IEnumerable<Dictionary<string, object>> Objects(Dictionary<string, object> obj, string key)
        {
            return (Get(obj, key) as IEnumerable ?? new object[0]).OfType<Dictionary<string, object>>();
        }
        private static string Prefer(params string[] values) { return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? ""; }
        private static string Describe(params string[] values)
        {
            string description = Regex.Replace(Prefer(values), @"\s+", " ").Trim();
            if (description.Length == 0) return "未提供功能介绍。";
            return description.Length <= 1400 ? description : description.Substring(0, 1399) + "…";
        }
    }
}
