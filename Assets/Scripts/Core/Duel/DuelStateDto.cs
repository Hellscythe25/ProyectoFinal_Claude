using System;
using System.Collections.Generic;

namespace TTTXO.Core.Duel
{
    /// <summary>
    /// One player inside the duel document.
    ///
    /// The field names are the wire contract (<c>id</c>, <c>nombre</c>, <c>hp</c>,
    /// <c>secuencia_actual</c>) and are intentionally Spanish/snake_case: they are serialized to the
    /// JSON that Cloud Save stores and that any future Cloud Code module reads. C# code should use
    /// the English aliases below instead of touching the contract fields directly.
    /// </summary>
    [Serializable]
    public sealed class DuelPlayerDto
    {
        public string id = string.Empty;
        public string nombre = string.Empty;
        public int hp = DuelRules.MaxHp;
        public string[] secuencia_actual = EmptySequence();

        /// <summary>Code-facing alias for <see cref="id"/>.</summary>
        public string PlayerId
        {
            get => id;
            set => id = value ?? string.Empty;
        }

        /// <summary>Code-facing alias for <see cref="nombre"/>.</summary>
        public string DisplayName
        {
            get => nombre;
            set => nombre = value ?? string.Empty;
        }

        /// <summary>Code-facing alias for <see cref="hp"/>.</summary>
        public int CurrentHp
        {
            get => hp;
            set => hp = value;
        }

        /// <summary>Code-facing alias for <see cref="secuencia_actual"/>.</summary>
        public string[] Sequence => secuencia_actual;

        /// <summary>How many of the three slots hold a valid move code.</summary>
        public int FilledSlots
        {
            get
            {
                if (secuencia_actual == null)
                {
                    return 0;
                }

                int filled = 0;
                for (int i = 0; i < secuencia_actual.Length && i < DuelRules.SlotsPerRound; i++)
                {
                    if (DuelMoveCodec.TryParse(secuencia_actual[i], out _))
                    {
                        filled++;
                    }
                }

                return filled;
            }
        }

        /// <summary>True when all three slots hold a valid move: the player has locked in this round.</summary>
        public bool HasFullSequence => FilledSlots == DuelRules.SlotsPerRound;

        /// <summary>True when this player has locked in the current round.</summary>
        public bool HasSubmitted => HasFullSequence;

        /// <summary>Empties every slot, keeping the array at three entries.</summary>
        public void ClearSequence()
        {
            secuencia_actual = EmptySequence();
        }

        /// <summary>
        /// Locks in exactly three moves - repeats are allowed (Guerrero, Guerrero, Asesino).
        /// Rejects a wrong count or an already-submitted sequence instead of overwriting it, so a
        /// double tap cannot rewrite a move the rival is already waiting on.
        /// </summary>
        public bool TrySetSequence(IReadOnlyList<RpsMove> moves, out string error)
        {
            if (moves == null)
            {
                error = "No moves were provided.";
                return false;
            }

            if (moves.Count != DuelRules.SlotsPerRound)
            {
                error = $"A round needs exactly {DuelRules.SlotsPerRound} moves, got {moves.Count}.";
                return false;
            }

            if (HasSubmitted)
            {
                error = "This player has already locked in the round.";
                return false;
            }

            var sequence = EmptySequence();
            for (int i = 0; i < DuelRules.SlotsPerRound; i++)
            {
                sequence[i] = DuelMoveCodec.ToCode(moves[i]);
            }

            secuencia_actual = sequence;
            error = null;
            return true;
        }

        /// <summary>Repairs a document loaded from JSON: non-null fields, three slots, HP in range.</summary>
        public void Normalize()
        {
            id ??= string.Empty;
            nombre ??= string.Empty;
            hp = Math.Max(0, Math.Min(DuelRules.MaxHp, hp));

            // Always rebuild the array: JsonUtility can hand back a null array, a wrong length, or
            // three null strings, and the rest of the code only understands three non-null slots.
            var resized = EmptySequence();
            if (secuencia_actual != null)
            {
                int copy = Math.Min(secuencia_actual.Length, DuelRules.SlotsPerRound);
                for (int i = 0; i < copy; i++)
                {
                    resized[i] = secuencia_actual[i] ?? string.Empty;
                }
            }

            secuencia_actual = resized;
        }

        private static string[] EmptySequence()
        {
            var sequence = new string[DuelRules.SlotsPerRound];
            for (int i = 0; i < sequence.Length; i++)
            {
                sequence[i] = string.Empty;
            }

            return sequence;
        }
    }

    /// <summary>
    /// The whole async duel, exactly as it is stored in Cloud Save (one JSON document per match).
    ///
    /// Contract fields are Spanish/snake_case for the same reason as <see cref="DuelPlayerDto"/>;
    /// use the English aliases in game code. <c>ganador</c> and <c>motivo_fin</c> are additive
    /// extensions - the flight plan's field list has no way to say who won or why, and both the
    /// result screen and the "victory by abandonment" analytics event need that.
    /// </summary>
    [Serializable]
    public sealed class DuelStateDto
    {
        public string match_id = string.Empty;
        public int ronda_actual = 1;
        public string estado_partida = DuelRules.StateYourTurn;
        public string turno_de = string.Empty;
        public string timestamp_ultimo_movimiento = string.Empty;
        public DuelPlayerDto jugador1 = new DuelPlayerDto();
        public DuelPlayerDto jugador2 = new DuelPlayerDto();

        // --- Extensions over the flight-plan contract (documented in the class remarks) ---
        public string ganador = string.Empty;
        public string motivo_fin = string.Empty;

        public string MatchId
        {
            get => match_id;
            set => match_id = value ?? string.Empty;
        }

        /// <summary>1-based round currently being played (or the last one played once finished).</summary>
        public int Round => ronda_actual;

        public string State
        {
            get => estado_partida;
            set => estado_partida = value ?? DuelRules.StateYourTurn;
        }

        /// <summary>Player the document is waiting on, or empty when both may still play.</summary>
        public string TurnOwnerId
        {
            get => turno_de;
            set => turno_de = value ?? string.Empty;
        }

        public string LastMoveTimestamp
        {
            get => timestamp_ultimo_movimiento;
            set => timestamp_ultimo_movimiento = value ?? string.Empty;
        }

        public DuelPlayerDto PlayerOne => jugador1;

        public DuelPlayerDto PlayerTwo => jugador2;

        public string WinnerId
        {
            get => ganador;
            set => ganador = value ?? string.Empty;
        }

        public string EndReason
        {
            get => motivo_fin;
            set => motivo_fin = value ?? string.Empty;
        }

        public bool IsFinished => estado_partida == DuelRules.StateFinished;

        /// <summary>
        /// Creates a fresh duel with both players at full HP and no move submitted.
        /// The caller owns id generation; Core never touches the clock or the filesystem.
        /// </summary>
        public static DuelStateDto Create(
            string matchId,
            string playerOneId,
            string playerOneName,
            string playerTwoId,
            string playerTwoName,
            DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("A duel needs a match id.", nameof(matchId));
            }

            if (string.IsNullOrWhiteSpace(playerOneId) || string.IsNullOrWhiteSpace(playerTwoId))
            {
                throw new ArgumentException("A duel needs two player ids.");
            }

            if (playerOneId == playerTwoId)
            {
                throw new ArgumentException("A duel needs two different players.", nameof(playerTwoId));
            }

            return new DuelStateDto
            {
                match_id = matchId,
                ronda_actual = 1,
                estado_partida = DuelRules.StateYourTurn,
                turno_de = string.Empty,
                timestamp_ultimo_movimiento = DuelTimestamp.ToIso(now),
                jugador1 = new DuelPlayerDto { id = playerOneId, nombre = playerOneName ?? string.Empty },
                jugador2 = new DuelPlayerDto { id = playerTwoId, nombre = playerTwoName ?? string.Empty },
                ganador = string.Empty,
                motivo_fin = string.Empty
            };
        }

        public DuelPlayerDto PlayerById(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return null;
            }

            if (jugador1 != null && jugador1.id == playerId)
            {
                return jugador1;
            }

            if (jugador2 != null && jugador2.id == playerId)
            {
                return jugador2;
            }

            return null;
        }

        /// <summary>The other player, or null when <paramref name="playerId"/> is not in the duel.</summary>
        public DuelPlayerDto OpponentOf(string playerId)
        {
            var player = PlayerById(playerId);
            if (player == null)
            {
                return null;
            }

            return ReferenceEquals(player, jugador1) ? jugador2 : jugador1;
        }

        public string OpponentIdOf(string playerId)
        {
            return OpponentOf(playerId)?.id ?? string.Empty;
        }

        /// <summary>
        /// The document's state from one player's point of view. The stored value is shared, so it
        /// only distinguishes "open round" from "someone is waiting"; each client needs its own
        /// TU_TURNO / ESPERANDO_RIVAL, which is what this method derives.
        /// </summary>
        public string StateFor(string playerId)
        {
            if (IsFinished)
            {
                return DuelRules.StateFinished;
            }

            var me = PlayerById(playerId);
            if (me == null)
            {
                return estado_partida;
            }

            return me.HasSubmitted ? DuelRules.StateWaitingRival : DuelRules.StateYourTurn;
        }

        /// <summary>True when this player still owes a sequence for the current round.</summary>
        public bool CanSubmit(string playerId)
        {
            if (IsFinished)
            {
                return false;
            }

            var me = PlayerById(playerId);
            return me != null && !me.HasSubmitted;
        }

        /// <summary>Repairs a document loaded from JSON so the rules never see nulls or bad ranges.</summary>
        public void Normalize()
        {
            match_id ??= string.Empty;
            turno_de ??= string.Empty;
            timestamp_ultimo_movimiento ??= string.Empty;
            ganador ??= string.Empty;
            motivo_fin ??= string.Empty;

            jugador1 ??= new DuelPlayerDto();
            jugador2 ??= new DuelPlayerDto();
            jugador1.Normalize();
            jugador2.Normalize();

            if (!DuelRules.IsKnownState(estado_partida))
            {
                bool looksOver = !string.IsNullOrEmpty(ganador)
                    || jugador1.hp <= 0
                    || jugador2.hp <= 0;
                estado_partida = looksOver ? DuelRules.StateFinished : DuelRules.StateYourTurn;
            }

            if (ronda_actual < 1)
            {
                ronda_actual = 1;
            }
        }
    }
}
