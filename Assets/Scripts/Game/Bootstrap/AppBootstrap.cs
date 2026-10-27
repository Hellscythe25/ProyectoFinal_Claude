using System.Threading.Tasks;
using TTTXO.Game.Services;
using TTTXO.Game.UI;
using UnityEngine;

namespace TTTXO.Game.Bootstrap
{
    /// <summary>
    /// Entry point placed on the persistent root GameObject. It hands off to the
    /// <see cref="ScreenRouter"/>, which binds the UXML screens and shows the RPS menu.
    ///
    /// Phase 4 of the flight plan also starts here: after the UI is up, an anonymous Unity
    /// Authentication session is opened and the account's duel documents are pulled into the local
    /// mirror. It is deliberately fire-and-forget - the menu must appear instantly and the game must
    /// stay fully playable offline, so a failed sign-in only logs a warning and the Hub keeps using
    /// the files on disk.
    /// </summary>
    [RequireComponent(typeof(GameManager))]
    [RequireComponent(typeof(ScreenRouter))]
    public class AppBootstrap : MonoBehaviour
    {
        private void Start()
        {
            var router = GetComponent<ScreenRouter>();
            router.Initialize();

            _ = StartCloudAsync(router);
        }

        private static async Task StartCloudAsync(ScreenRouter router)
        {
            if (router == null || router.DuelStore == null)
            {
                return;
            }

            if (!await DuelCloudService.InitializeAsync())
            {
                return;
            }

            int restored = await DuelCloudService.PullAsync(router.DuelStore.Mirror);
            if (restored > 0)
            {
                Debug.Log($"AppBootstrap: restored {restored} duel(s) from Cloud Save.");
                router.Duels.Refresh();
            }
        }
    }
}
