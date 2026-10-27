using System;

namespace TTTXO.Core.Duel
{
    /// <summary>
    /// Inactivity rules for the async duel: the "Reclamar Victoria" window.
    ///
    /// The flight plan moved this check to the client (the document carries the last-move timestamp
    /// so the UI can count down locally). The check itself lives here, Unity-free, so the same rule
    /// can later be re-run inside Cloud Code before the server accepts a claim - a client-only check
    /// would let a modified build claim any match it liked.
    ///
    /// A claim is only legal when the claimant already locked in the round (otherwise somebody could
    /// claim a match they never played) and the rival is the one missing a move.
    /// </summary>
    public static class DuelTimeout
    {
        /// <summary>Default window, as specified by the GDD and the flight plan.</summary>
        public static readonly TimeSpan Default = TimeSpan.FromMinutes(DuelRules.DefaultTimeoutMinutes);

        /// <summary>Shortened window for demos/evaluation (GDD allows 2-5 minutes).</summary>
        public static readonly TimeSpan Demo = TimeSpan.FromMinutes(DuelRules.DemoTimeoutMinutes);

        /// <summary>True when the document has not seen activity within the window.</summary>
        public static bool HasExpired(DuelStateDto state, DateTimeOffset now, TimeSpan timeout)
        {
            if (state == null || state.IsFinished)
            {
                return false;
            }

            if (!DuelTimestamp.TryParse(state.timestamp_ultimo_movimiento, out var last))
            {
                return false;
            }

            return now - last >= timeout;
        }

        /// <summary>Time left before a claim unlocks; <see cref="TimeSpan.Zero"/> once it is available.</summary>
        public static TimeSpan Remaining(DuelStateDto state, DateTimeOffset now, TimeSpan timeout)
        {
            if (state == null || state.IsFinished)
            {
                return TimeSpan.Zero;
            }

            if (!DuelTimestamp.TryParse(state.timestamp_ultimo_movimiento, out var last))
            {
                return TimeSpan.Zero;
            }

            var left = timeout - (now - last);
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        /// <summary>
        /// True when <paramref name="claimantId"/> may claim the duel right now: the claimant has
        /// already played, the rival has not, and the rival has been silent for the whole window.
        /// </summary>
        public static bool CanClaimVictory(
            DuelStateDto state,
            string claimantId,
            DateTimeOffset now,
            TimeSpan timeout)
        {
            if (state == null || state.IsFinished || timeout <= TimeSpan.Zero)
            {
                return false;
            }

            var claimant = state.PlayerById(claimantId);
            var rival = state.OpponentOf(claimantId);

            if (claimant == null || rival == null)
            {
                return false;
            }

            if (!claimant.HasSubmitted || rival.HasSubmitted)
            {
                return false;
            }

            return HasExpired(state, now, timeout);
        }

        /// <summary>
        /// Claims the duel in place, marking it finished with <see cref="DuelRules.EndAbandon"/>.
        /// Returns false (and leaves the document untouched) when the claim is not legal yet.
        /// </summary>
        public static bool TryClaimVictory(
            DuelStateDto state,
            string claimantId,
            DateTimeOffset now,
            TimeSpan timeout)
        {
            if (!CanClaimVictory(state, claimantId, now, timeout))
            {
                return false;
            }

            state.estado_partida = DuelRules.StateFinished;
            state.turno_de = string.Empty;
            state.ganador = claimantId;
            state.motivo_fin = DuelRules.EndAbandon;
            state.timestamp_ultimo_movimiento = DuelTimestamp.ToIso(now);
            state.jugador1?.ClearSequence();
            state.jugador2?.ClearSequence();
            return true;
        }
    }
}
