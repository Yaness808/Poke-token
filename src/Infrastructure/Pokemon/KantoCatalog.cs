using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using TokenPet.Core;

namespace TokenPet.Infrastructure.Pokemon
{
    internal sealed class KantoCatalog
    {
        internal const int DefaultId = 7;
        internal const int Count = 151;
        private readonly PokemonSpecies[] entries;

        internal KantoCatalog()
        {
            using (var stream = typeof(KantoCatalog).Assembly.GetManifestResourceStream("TokenPet.Kanto.json"))
            using (var reader = new StreamReader(stream))
                entries = new JavaScriptSerializer().Deserialize<PokemonSpecies[]>(reader.ReadToEnd());
            if (entries == null || entries.Length != Count)
                throw new InvalidDataException("The Kanto catalog must contain 151 species.");
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Id != i + 1 || string.IsNullOrWhiteSpace(entry.Name) ||
                    string.IsNullOrWhiteSpace(entry.English) || entry.Types == null || entry.Types.Length == 0 ||
                    entry.EvolutionStage < 1 || entry.EvolutionStage > 3 || entry.HeightDecimeters < 1)
                    throw new InvalidDataException("Invalid Kanto catalog entry #" + (i + 1));
            }
        }

        internal PokemonSpecies Get(int id)
        {
            return entries[id >= 1 && id <= Count ? id - 1 : DefaultId - 1];
        }

        internal IEnumerable<PokemonSpecies> Search(string query)
        {
            string term = query.Trim();
            foreach (var entry in entries)
                if (entry.Matches(term)) yield return entry;
        }
    }
}
