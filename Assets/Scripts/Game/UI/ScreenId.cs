namespace TTTXO.Game.UI
{
    /// <summary>
    /// The screens the <see cref="ScreenRouter"/> can navigate to. This build is the
    /// Rock-Paper-Scissors game plus the async duel: the RPS menu is the hub, the local match flow is
    /// Mode Select -> Character Select -> VS, the async flow is Duel Hub -> Duel Combat, and
    /// Store/Settings are the secondary screens.
    ///
    /// Values are explicit and append-only because <c>_startScreen</c> is serialized in the scene.
    /// </summary>
    public enum ScreenId
    {
        RpsMenu = 0,
        RpsModeSelect = 1,
        RpsCharacterSelect = 2,
        RpsVs = 3,
        Store = 4,
        Settings = 5,
        DuelHub = 6,
        DuelCombat = 7
    }
}
