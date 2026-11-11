using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
using TokenPet.Core;

namespace TokenPet.Infrastructure.Configuration
{
    using Json = Dictionary<string, object>;

    internal static class RateCatalog
    {
        internal static Pricing Load(string path)
        {
            var rates = new Dictionary<string, ModelRate>(StringComparer.Ordinal);
            Json data;
            try { data = new JavaScriptSerializer().Deserialize<Json>(File.ReadAllText(path)); }
            catch (IOException) { return new Pricing(rates); }
            catch (UnauthorizedAccessException) { return new Pricing(rates); }
            catch (ArgumentException) { return new Pricing(rates); }
            catch (InvalidOperationException) { return new Pricing(rates); }
            if (data == null) return new Pricing(rates);

            foreach (var row in data)
            {
                var values = row.Value as Json;
                var rate = new ModelRate();
                if (values != null && ReadRate(values, "input", out rate.Input) &&
                    ReadRate(values, "cached", out rate.Cached) && ReadRate(values, "output", out rate.Output))
                    rates.Add(row.Key, rate);
            }
            return new Pricing(rates);
        }

        private static bool ReadRate(Json row, string key, out decimal rate)
        {
            object value;
            rate = 0;
            return row.TryGetValue(key, out value) &&
                decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out rate) && rate >= 0 && rate < 1000000;
        }
    }
}
