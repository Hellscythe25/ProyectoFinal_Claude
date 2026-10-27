using System;
using TTTXO.Core.Duel;
using UnityEngine;

namespace TTTXO.Game.Duel
{
    /// <summary>
    /// JSON (de)serialization for the duel document that Cloud Save stores as one text file per
    /// match. <see cref="JsonUtility"/> keeps the contract field names from <see cref="DuelStateDto"/>
    /// untouched, which is exactly what the flight plan asks for.
    ///
    /// <see cref="TryFromJson"/> never throws: a corrupt or half-written document is reported to the
    /// caller (and logged) instead of taking the Hub down, because the Hub loads every match at once.
    /// </summary>
    public static class DuelStateSerializer
    {
        public static string ToJson(DuelStateDto state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            state.Normalize();
            return JsonUtility.ToJson(state, prettyPrint: true);
        }

        public static bool TryFromJson(string json, out DuelStateDto state, out string error)
        {
            state = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "The document is empty.";
                return false;
            }

            try
            {
                state = JsonUtility.FromJson<DuelStateDto>(json);
                if (state == null)
                {
                    error = "The document could not be read as a duel.";
                    return false;
                }

                // JsonUtility does not run field initializers or constructors, so a document
                // missing a key arrives with null players/sequences. Normalize repairs that.
                state.Normalize();

                if (string.IsNullOrEmpty(state.match_id))
                {
                    error = "The document has no match id.";
                    state = null;
                    return false;
                }

                if (state.jugador1 == null || state.jugador2 == null
                    || string.IsNullOrEmpty(state.jugador1.id)
                    || string.IsNullOrEmpty(state.jugador2.id))
                {
                    error = "The document does not contain two players.";
                    state = null;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                state = null;
                return false;
            }
        }

        public static bool TryFromJson(string json, out DuelStateDto state)
        {
            return TryFromJson(json, out state, out _);
        }
    }
}
