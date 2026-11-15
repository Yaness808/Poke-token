using System;
using System.Globalization;

namespace TokenPet.Core
{
    internal sealed class PokemonSpecies
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string English { get; set; }
        public string[] Types { get; set; }
        public int EvolutionStage { get; set; }
        public int HeightDecimeters { get; set; }

        public string Number { get { return "#" + Id.ToString("000", CultureInfo.InvariantCulture); } }
        public string TypeLabel { get { return string.Join(" / ", Types); } }

        internal bool Matches(string term)
        {
            if (term.Length == 0) return true;
            int id;
            if (int.TryParse(term.TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture, out id))
                return Id == id;
            return Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                English.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
