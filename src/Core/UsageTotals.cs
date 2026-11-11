using System.Collections.Generic;

namespace TokenPet.Core
{
    internal static class UsageTotals
    {
        internal static void Accumulate(Summary result, IEnumerable<Sample> samples, Pricing pricing)
        {
            var seenEvents = new HashSet<string>();
            foreach (var sample in samples)
            {
                if (!seenEvents.Add(sample.Key)) continue;
                result.Tokens += sample.Tokens;
                if (result.Latest == null || sample.Time > result.Latest.Time) result.Latest = sample;
                pricing.AddEstimate(result, sample);
            }
        }
    }
}
