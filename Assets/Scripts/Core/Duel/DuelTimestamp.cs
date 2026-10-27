using System;
using System.Globalization;

namespace TTTXO.Core.Duel
{
    /// <summary>
    /// ISO 8601 timestamps for <c>timestamp_ultimo_movimiento</c>.
    ///
    /// Always UTC and always round-trippable ("o"), so two devices - or the client and Cloud Code -
    /// subtract the same instants without timezone drift. A document written by another culture
    /// is still accepted on read, but anything unparsable is reported as false instead of silently
    /// becoming "now", because "now" would make an abandoned duel look active forever.
    /// </summary>
    public static class DuelTimestamp
    {
        private const string Format = "o";

        public static string ToIso(DateTimeOffset value)
        {
            return value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture);
        }

        public static string Now()
        {
            return ToIso(DateTimeOffset.UtcNow);
        }

        public static bool TryParse(string value, out DateTimeOffset result)
        {
            result = default;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal,
                out result);
        }
    }
}
