using System;
using System.Globalization;
using System.Collections.Generic;

namespace TokenPet.Infrastructure.Codex
{
    using Json = Dictionary<string, object>;

    internal static class Log
    {
        internal static object Value(Json json, string key)
        {
            object value;
            return json != null && json.TryGetValue(key, out value) ? value : null;
        }

        internal static Json Object(Json json, string key) { return Value(json, key) as Json; }
        internal static string String(Json json, string key) { return Value(json, key) as string ?? ""; }
        internal static long Number(Json json, string key)
        {
            long number;
            return long.TryParse(Convert.ToString(Value(json, key), CultureInfo.InvariantCulture), out number)
                ? Math.Max(0, number) : 0;
        }
    }

}
