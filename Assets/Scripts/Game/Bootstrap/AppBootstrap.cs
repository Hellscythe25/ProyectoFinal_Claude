using TTTXO.Game.UI;
using UnityEngine;

namespace TTTXO.Game.Bootstrap
{
    /// <summary>
    /// Entry point placed on the persistent root GameObject. It hands off to the
    /// <see cref="ScreenRouter"/>, which binds the UXML screens and shows the RPS menu.
    /// </summary>
    [RequireComponent(typeof(GameManager))]
    [RequireComponent(typeof(ScreenRouter))]
    public class AppBootstrap : MonoBehaviour
    {
        private void Start()
        {
            var router = GetComponent<ScreenRouter>();
            router.Initialize();
        }
    }
}
