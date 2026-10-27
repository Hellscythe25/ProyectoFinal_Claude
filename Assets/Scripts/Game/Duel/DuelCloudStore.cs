using System.Collections.Generic;
using TTTXO.Core.Duel;
using TTTXO.Game.Services;

namespace TTTXO.Game.Duel
{
    /// <summary>
    /// Offline-first Cloud Save store: the local mirror is always the source of truth for this
    /// device, and every write is also pushed to the account in the background once the anonymous
    /// session is ready.
    ///
    /// This is what lets the whole async loop keep working with the network down - the Hub never
    /// awaits an upload, and a failed push only logs a warning. <see cref="IDuelStore"/> stays
    /// synchronous because it backs the screens; the cloud work is queued behind it.
    /// </summary>
    public sealed class DuelCloudStore : IDuelStore
    {
        public DuelCloudStore(LocalDuelStore mirror = null)
        {
            Mirror = mirror ?? new LocalDuelStore();
        }

        /// <summary>Local files that survive a restart and act as the offline cache.</summary>
        public LocalDuelStore Mirror { get; }

        public IReadOnlyList<DuelStateDto> LoadAll()
        {
            return Mirror.LoadAll();
        }

        public void Save(DuelStateDto state)
        {
            Mirror.Save(state);

            if (DuelCloudService.IsReady)
            {
                // Fire-and-forget: the service catches and logs its own failures, so an upload can
                // never break the local save that already succeeded.
                _ = DuelCloudService.PushAsync(state);
            }
        }

        public void Delete(string matchId)
        {
            Mirror.Delete(matchId);

            if (DuelCloudService.IsReady)
            {
                _ = DuelCloudService.DeleteAsync(matchId);
            }
        }
    }
}
