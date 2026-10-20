namespace TTTXO.Game.UI
{
    /// <summary>
    /// The screens the <see cref="ScreenRouter"/> can navigate to. This build is the
    /// Rock-Paper-Scissors game: the RPS menu is the hub, the match flow is
    /// Mode Select -> Character Select -> VS, and Store/Settings are the secondary screens.
    /// </summary>
    public enum ScreenId
    {
        RpsMenu = 0,
        RpsModeSelect = 1,
        RpsCharacterSelect = 2,
        RpsVs = 3,
        Store = 4,
        Settings = 5
    }
}
