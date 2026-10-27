using System;

namespace TTTXO.Core.Duel
{
    /// <summary>
    /// Translates between the three RPS moves and the strings stored in the Cloud Save document.
    ///
    /// The document is read by tools and, later, by other clients, so the canonical codes are the
    /// player-facing class names ("Guerrero", "Maga", "Asesino"). The English and plural aliases
    /// are accepted on read so an older or hand-edited document still loads.
    /// </summary>
    public static class DuelMoveCodec
    {
        public const string Guerrero = "Guerrero";
        public const string Maga = "Maga";
        public const string Asesino = "Asesino";

        /// <summary>Canonical document code for a move (Guerrero = Rock, Maga = Paper, Asesino = Scissors).</summary>
        public static string ToCode(RpsMove move)
        {
            return move switch
            {
                RpsMove.Rock => Guerrero,
                RpsMove.Paper => Maga,
                RpsMove.Scissors => Asesino,
                _ => throw new ArgumentOutOfRangeException(nameof(move), move, "Unknown RPS move.")
            };
        }

        /// <summary>Parses a document code. Returns false instead of throwing for unknown/empty values.</summary>
        public static bool TryParse(string code, out RpsMove move)
        {
            move = RpsMove.Rock;

            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            switch (code.Trim().ToLowerInvariant())
            {
                case "guerrero":
                case "piedra":
                case "rock":
                    move = RpsMove.Rock;
                    return true;

                case "maga":
                case "mago":
                case "papel":
                case "paper":
                    move = RpsMove.Paper;
                    return true;

                case "asesino":
                case "tijera":
                case "tijeras":
                case "scissors":
                    move = RpsMove.Scissors;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Parses a document code, throwing <see cref="ArgumentException"/> when it is not a move.</summary>
        public static RpsMove Parse(string code, string paramName = "code")
        {
            if (!TryParse(code, out var move))
            {
                throw new ArgumentException(
                    $"'{code}' is not a duel move code ({Guerrero}, {Maga}, {Asesino}).", paramName);
            }

            return move;
        }
    }
}
