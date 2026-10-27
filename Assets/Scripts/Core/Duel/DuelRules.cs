namespace TTTXO.Core.Duel
{
    /// <summary>
    /// Fixed numbers of the async duel, as defined by the "Duelo de Gremios" flight plan:
    /// 100 HP per player, 15 damage per clash won, three action slots per round, and a ten
    /// minute inactivity window before "Reclamar Victoria" unlocks.
    ///
    /// Everything here is a rule, not a preference: the client and the future Cloud Code host must
    /// read the same values, so they live in the Unity-free Core assembly.
    /// </summary>
    public static class DuelRules
    {
        /// <summary>HP every player starts a duel with.</summary>
        public const int MaxHp = 100;

        /// <summary>HP the loser of a single clash loses.</summary>
        public const int ClashDamage = 15;

        /// <summary>Action slots each player fills per round (and clashes resolved per round).</summary>
        public const int SlotsPerRound = 3;

        /// <summary>Default inactivity window before the rival can be claimed as abandoned.</summary>
        public const int DefaultTimeoutMinutes = 10;

        /// <summary>
        /// Shortened window used for demos and evaluation (the GDD allows 2-5 minutes).
        /// </summary>
        public const int DemoTimeoutMinutes = 2;

        /// <summary>
        /// Safety cap. Two players can draw every clash forever, so a round limit is needed to
        /// guarantee the duel ends; at the cap the higher HP wins and equal HP is a draw.
        /// </summary>
        public const int MaxRounds = 30;

        // estado_partida values (the wire contract, in Spanish on purpose - see DuelStateDto).

        /// <summary>Nobody has submitted this round: both players may still play.</summary>
        public const string StateYourTurn = "TU_TURNO";

        /// <summary>One side has submitted and the other has not.</summary>
        public const string StateWaitingRival = "ESPERANDO_RIVAL";

        /// <summary>The duel has a winner (or ended in a draw); no more input is accepted.</summary>
        public const string StateFinished = "FINALIZADO";

        // motivo_fin values.

        public const string EndKo = "KO";
        public const string EndAbandon = "ABANDONO";
        public const string EndDraw = "EMPATE";
        public const string EndRoundLimit = "LIMITE";

        /// <summary>True for the three values the wire contract allows in <c>estado_partida</c>.</summary>
        public static bool IsKnownState(string state)
        {
            return state == StateYourTurn || state == StateWaitingRival || state == StateFinished;
        }
    }
}
