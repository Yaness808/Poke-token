using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using TokenPet.Infrastructure.Pokemon;

namespace TokenPet.Infrastructure.Configuration
{
    internal sealed class Preferences
    {
        internal const double MinScale = 0.65;
        internal const double MaxScale = 1.60;
        internal const double DefaultInset = 16;

        public double RightInset { get; set; }
        public double BottomInset { get; set; }
        public double Scale { get; set; }
        public string MascotPath { get; set; }
        public int PokemonId { get; set; }
        public int[] FavoritePokemonIds { get; set; }
        public double SkillsOverviewWidth { get; set; }
        public double SkillsOverviewHeight { get; set; }
        public int SkillsOverviewZoom { get; set; }
        public bool SkillsOverviewMaximized { get; set; }

        public Preferences()
        {
            ResetPlacement();
            PokemonId = PokemonCatalog.DefaultId;
            FavoritePokemonIds = new int[0];
            SkillsOverviewWidth = 660;
            SkillsOverviewHeight = 560;
            SkillsOverviewZoom = 100;
        }

        internal void ResetPlacement()
        {
            RightInset = DefaultInset;
            BottomInset = DefaultInset;
            Scale = 1;
        }

        internal static double ClampScale(double scale)
        {
            return Math.Max(MinScale, Math.Min(MaxScale, scale));
        }

        internal static Preferences Load(string path)
        {
            try
            {
                var settings = new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(path));
                if (settings == null) return new Preferences();
                if (double.IsNaN(settings.Scale) || double.IsInfinity(settings.Scale)) settings.Scale = 1;
                settings.Scale = ClampScale(settings.Scale);
                settings.RightInset = ReadInset(settings.RightInset);
                settings.BottomInset = ReadInset(settings.BottomInset);
                settings.FavoritePokemonIds = (settings.FavoritePokemonIds ?? new int[0])
                    .Where(id => id >= 1 && id <= PokemonCatalog.Count).Distinct().OrderBy(id => id).ToArray();
                return settings;
            }
            catch (IOException) { return new Preferences(); }
            catch (UnauthorizedAccessException) { return new Preferences(); }
            catch (ArgumentException) { return new Preferences(); }
            catch (InvalidOperationException) { return new Preferences(); }
        }

        internal void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(this));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        private static double ReadInset(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? DefaultInset : Math.Max(0, value);
        }
    }
}
