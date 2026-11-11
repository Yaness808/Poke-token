using System.Collections.Generic;

namespace TokenPet.Core
{
    internal sealed class ModelRate
    {
        internal decimal Input, Cached, Output;
    }

    internal sealed class Pricing
    {
        private const decimal TokensPerMillion = 1000000m;
        private const decimal CreditsPerStandardDollar = 25m;
        private readonly IDictionary<string, ModelRate> rates;

        internal Pricing(IDictionary<string, ModelRate> rates) { this.rates = rates; }

        internal void AddEstimate(Summary result, Sample sample)
        {
            var tokens = sample.Tokens;
            ModelRate rate;
            if ((sample.Provider != "" && sample.Provider != "openai") || !rates.TryGetValue(sample.Model, out rate))
            {
                result.Unpriced += tokens.Total;
                return;
            }

            decimal inputValue = (tokens.Input - tokens.Cached) * rate.Input + tokens.Cached * rate.Cached;
            decimal outputValue = tokens.Output * rate.Output;
            decimal multiplier = sample.Tier == "priority" || sample.Tier == "fast" ? 2m :
                sample.Tier == "ultrafast" ? 6m : 1m;
            // The bundled model rates share this relationship for standard, short-context usage.
            result.Credits += (inputValue + outputValue) * multiplier * CreditsPerStandardDollar / TokensPerMillion;
            if (tokens.Input > 272000)
            {
                inputValue *= 2m;
                outputValue *= 1.5m;
            }
            result.Dollars += (inputValue + outputValue) * multiplier / TokensPerMillion;
        }
    }
}
