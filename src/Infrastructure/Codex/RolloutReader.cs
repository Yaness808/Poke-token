using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using TokenPet.Core;

namespace TokenPet.Infrastructure.Codex
{
    using Json = Dictionary<string, object>;

    internal sealed class RolloutReader : IDisposable
    {
        private const int MaxLineBytes = 1024 * 1024;
        private readonly MemoryStream line = new MemoryStream();
        private bool skippingLine;
        private long offset;
        private DateTime creationTime;
        private string model = "", tier = "", provider = "";
        private Tokens total;
        private bool hasTotal;
        internal WeeklyQuota Weekly { get; private set; }
        internal readonly List<Sample> Samples = new List<Sample>();

        internal void Read(FileInfo file, DateTime day, JavaScriptSerializer json, byte[] buffer)
        {
            using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length < offset || creationTime != file.CreationTimeUtc)
                {
                    Samples.Clear();
                    Weekly = null;
                    offset = 0;
                    line.SetLength(0);
                    skippingLine = hasTotal = false;
                    model = tier = provider = "";
                }
                creationTime = file.CreationTimeUtc;
                stream.Position = offset;
                long remaining = stream.Length - offset;
                while (remaining > 0)
                {
                    int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                    if (read == 0) break;
                    Append(buffer, read, day, json);
                    offset += read;
                    remaining -= read;
                }
            }
        }

        private void Append(byte[] buffer, int count, DateTime day, JavaScriptSerializer json)
        {
            int start = 0;
            while (start < count)
            {
                int end = Array.IndexOf(buffer, (byte)'\n', start, count - start);
                int length = (end < 0 ? count : end) - start;
                skippingLine |= line.Length + length > MaxLineBytes;
                if (!skippingLine) line.Write(buffer, start, length);
                if (end < 0) break;
                if (!skippingLine && line.Length != 0)
                    ReadLine(Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length), day, json);
                line.SetLength(0);
                skippingLine = false;
                start = end + 1;
            }
        }

        private void ReadLine(string lineText, DateTime day, JavaScriptSerializer json)
        {
            if (!lineText.Contains("\"session_meta\"") && !lineText.Contains("\"turn_context\"") &&
                !lineText.Contains("\"token_count\"")) return;

            Json record;
            try { record = json.Deserialize<Json>(lineText.TrimStart('\uFEFF')); }
            catch (ArgumentException) { return; }
            catch (InvalidOperationException) { return; }
            var payload = Log.Object(record, "payload");
            switch (Log.String(record, "type"))
            {
                case "session_meta":
                    provider = Log.String(payload, "model_provider");
                    return;
                case "turn_context":
                    model = Log.String(payload, "model");
                    tier = Log.String(payload, "service_tier");
                    return;
                case "event_msg":
                    if (Log.String(payload, "type") != "token_count") return;
                    break;
                default:
                    return;
            }

            DateTimeOffset time;
            bool hasTime = DateTimeOffset.TryParse(Log.String(record, "timestamp"),
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out time);
            if (hasTime)
            {
                var quota = QuotaParser.Read(Log.Object(payload, "rate_limits"), time);
                if (quota != null && (Weekly == null || quota.ObservedAt >= Weekly.ObservedAt)) Weekly = quota;
            }

            var info = Log.Object(payload, "info");
            var cumulative = Log.Object(info, "total_token_usage");
            if (cumulative == null) return;
            var next = ReadTokens(cumulative);
            var last = Log.Object(info, "last_token_usage");
            var delta = hasTotal ? next - total : next;
            if (!hasTotal || !delta.IsValid)
                delta = last != null ? ReadTokens(last) : next;
            total = next;
            hasTotal = true;
            if (delta.Total <= 0 || !hasTime || time.LocalDateTime.Date != day) return;

            // Forked rollouts can contain the original timestamped usage records.
            Samples.Add(new Sample {
                Time = time, Model = model, Tier = tier, Provider = provider, Tokens = delta,
                Key = time.UtcTicks + "/" + next.Input + "/" + next.Cached + "/" + next.Output
            });
        }

        private static Tokens ReadTokens(Json value)
        {
            long input = Log.Number(value, "input_tokens");
            return new Tokens {
                Input = input,
                Cached = Math.Min(input, Log.Number(value, "cached_input_tokens")),
                Output = Log.Number(value, "output_tokens")
            };
        }

        public void Dispose() { line.Dispose(); }
    }

}
