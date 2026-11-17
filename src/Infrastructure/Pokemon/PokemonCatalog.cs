using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using TokenPet.Core;

namespace TokenPet.Infrastructure.Pokemon
{
    internal sealed class PokemonCatalog
    {
        internal const int DefaultId = 7;
        private const int KantoLastId = 151;
        private const int JohtoLastId = 251;
        private const int HoennLastId = 386;
        private const int SinnohLastId = 493;
        private const int UnovaLastId = 649;
        private const int KalosLastId = 721;
        internal const int Count = 809;
        private readonly PokemonSpecies[] entries;

        internal PokemonCatalog()
        {
            entries = ReadCatalog("TokenPet.Kanto.json", KantoLastId)
                .Concat(ReadCatalog("TokenPet.Johto.json", JohtoLastId - KantoLastId))
                .Concat(ReadCatalog("TokenPet.Hoenn.json", HoennLastId - JohtoLastId))
                .Concat(ReadCatalog("TokenPet.Sinnoh.json", SinnohLastId - HoennLastId))
                .Concat(ReadCatalog("TokenPet.Unova.json", UnovaLastId - SinnohLastId))
                .Concat(ReadCatalog("TokenPet.Kalos.json", KalosLastId - UnovaLastId))
                .Concat(ReadCatalog("TokenPet.Alola.json", Count - KalosLastId))
                .ToArray();
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Id != i + 1 || string.IsNullOrWhiteSpace(entry.Name) ||
                    string.IsNullOrWhiteSpace(entry.English) || entry.Types == null || entry.Types.Length == 0 ||
                    entry.EvolutionStage < 1 || entry.EvolutionStage > 3 || entry.HeightDecimeters < 1)
                    throw new InvalidDataException("Invalid Pokémon catalog entry #" + (i + 1));
            }
        }

        private static PokemonSpecies[] ReadCatalog(string resource, int expectedCount)
        {
            using (var stream = typeof(PokemonCatalog).Assembly.GetManifestResourceStream(resource))
            using (var reader = new StreamReader(stream))
            {
                var species = new JavaScriptSerializer().Deserialize<PokemonSpecies[]>(reader.ReadToEnd());
                if (species == null || species.Length != expectedCount)
                    throw new InvalidDataException("Invalid Pokémon catalog size: " + resource);
                return species;
            }
        }

        internal PokemonSpecies Get(int id)
        {
            return entries[id >= 1 && id <= Count ? id - 1 : DefaultId - 1];
        }

        internal IEnumerable<PokemonSpecies> Search(string query, int generation = 0)
        {
            string term = query.Trim();
            foreach (var entry in entries)
                if ((generation == 0 || GenerationOf(entry.Id) == generation) && entry.Matches(term))
                    yield return entry;
        }

        private static int GenerationOf(int id)
        {
            if (id <= KantoLastId) return 1;
            if (id <= JohtoLastId) return 2;
            if (id <= HoennLastId) return 3;
            if (id <= SinnohLastId) return 4;
            if (id <= UnovaLastId) return 5;
            if (id <= KalosLastId) return 6;
            return 7;
        }
    }
}
