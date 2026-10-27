using System.Collections.Generic;
using TTTXO.Core.Duel;

namespace TTTXO.Game.Duel
{
    /// <summary>
    /// Where duel documents live. Today only <see cref="LocalDuelStore"/> implements it (one JSON per
    /// match under <c>Application.persistentDataPath</c>), which is what makes the offline State
    /// Machine of Phase 3 possible; the Cloud Save adapter implements the same contract so the Hub
    /// and the combat screen never learn where the document came from.
    /// </summary>
    public interface IDuelStore
    {
        /// <summary>Every document found, including finished ones. Corrupt documents are skipped.</summary>
        IReadOnlyList<DuelStateDto> LoadAll();

        void Save(DuelStateDto state);

        void Delete(string matchId);
    }
}
