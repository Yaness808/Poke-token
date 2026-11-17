using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using TokenPet.Infrastructure.Codex;

namespace TokenPet.Desktop.Views
{
    internal sealed class ExtensionPresentation : INotifyPropertyChanged
    {
        private sealed class TranslationGroup
        {
            public string Name { get; set; }
            public Dictionary<string, string> Descriptions { get; set; }
            public Dictionary<string, string> DisplayNames { get; set; }
        }

        private const string Other = "其他";
        private static readonly TranslationGroup[] Groups = Load();
        private static readonly Dictionary<string, TranslationGroup> Index = Groups
            .SelectMany(group => group.Descriptions.Keys.Select(key => new { Key = key, Group = group }))
            .ToDictionary(item => item.Key, item => item.Group, StringComparer.OrdinalIgnoreCase);

        private readonly ExtensionEntry entry;
        private bool isExpanded;
        public string Key { get { return entry.Key; } }
        public string Name { get; private set; }
        public string VersionLabel { get { return string.IsNullOrEmpty(entry.Version) ? "" : " · " + entry.Version; } }
        public string Kind { get { return entry.Kind; } }
        public string Status { get { return entry.Status; } }
        public string Source { get { return entry.Source; } }
        public string Description { get; private set; }
        public string Category { get; private set; }
        internal int CategoryOrder { get; private set; }
        internal string PluginKey { get { return entry.PluginKey; } }
        internal bool IsBundledSkill { get { return Kind == ExtensionEntry.Skill && !string.IsNullOrEmpty(PluginKey); } }
        public ExtensionPresentation[] BundledSkills { get; set; }
        public bool HasBundledSkills { get { return BundledSkills.Length > 0; } }
        public bool IsExpanded
        {
            get { return isExpanded; }
            set
            {
                if (isExpanded == value) return;
                isExpanded = value;
                var handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs("IsExpanded"));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public string SkillCountLabel { get { return HasBundledSkills ? " · " + BundledSkills.Length + " 项技能" : ""; } }

        internal static IEnumerable<string> Categories
        {
            get { return Groups.Select(group => group.Name).Concat(new[] { Other }); }
        }

        internal ExtensionPresentation(ExtensionEntry entry)
        {
            this.entry = entry;
            BundledSkills = new ExtensionPresentation[0];
            string pluginKey = (entry.PluginKey ?? "").Split('@')[0];
            string skillKey = string.IsNullOrEmpty(entry.FilePath) ? entry.Name :
                Path.GetFileName(Path.GetDirectoryName(entry.FilePath));
            string key = entry.Kind == ExtensionEntry.Plugin ? pluginKey :
                pluginKey.Length == 0 ? skillKey : pluginKey + "/" + skillKey;

            TranslationGroup group;
            if (key != null && Index.TryGetValue(key, out group))
                Description = Lookup(group.Descriptions, key);
            else
            {
                Index.TryGetValue(pluginKey, out group);
                Description = entry.Description;
                if (!string.IsNullOrEmpty(Description) && !Description.Any(ch => ch >= '\u3400' && ch <= '\u9fff'))
                    Description = "原文：" + Description;
            }
            Category = group == null ? Other : group.Name;
            CategoryOrder = group == null ? Groups.Length : Array.IndexOf(Groups, group);
            Name = Lookup(group == null ? null : group.DisplayNames, key) ?? entry.Name;
        }

        private static string Lookup(Dictionary<string, string> labels, string key)
        {
            if (labels == null || key == null) return null;
            return labels.Where(label => string.Equals(label.Key, key, StringComparison.OrdinalIgnoreCase))
                .Select(label => label.Value).FirstOrDefault();
        }

        private static TranslationGroup[] Load()
        {
            using (var stream = typeof(ExtensionPresentation).Assembly.GetManifestResourceStream("TokenPet.Skills.zh-CN.json"))
            using (var reader = new StreamReader(stream))
                return new JavaScriptSerializer().Deserialize<TranslationGroup[]>(reader.ReadToEnd());
        }
    }
}
