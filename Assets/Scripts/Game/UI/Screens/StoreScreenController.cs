using System.Collections.Generic;
using System.Globalization;
using TTTXO.Game.Bootstrap;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// Store screen: a navigable stub. Category tabs switch the active class only - the catalog does
    /// not exist yet - so every tab shows the same placeholder "Coming soon" grid and there is no
    /// purchase logic. Reachable from the RPS menu.
    /// </summary>
    public class StoreScreenController : IScreenController
    {
        private enum Tab
        {
            Skins,
            Arenas,
            Fx
        }

        private static readonly Tab[] TabOrder = { Tab.Skins, Tab.Arenas, Tab.Fx };
        private const int PlaceholderCardCount = 4;

        private readonly ScreenRouter _router;
        private readonly GameManager _gameManager;
        private readonly Label _titleLabel;
        private readonly Label _hardCurrencyLabel;
        private readonly VisualElement _tabList;
        private readonly VisualElement _itemGrid;

        private readonly Dictionary<Tab, Button> _tabButtons = new();

        public StoreScreenController(VisualElement root, ScreenRouter router, GameManager gameManager)
        {
            _router = router;
            _gameManager = gameManager;

            root.Q<Button>("store-back-button").text = UiText.Common.BackIcon;
            root.Q<Button>("store-back-button").clicked += () => _router.GoBack(ScreenId.RpsMenu);
            _titleLabel = root.Q<Label>("store-title-label");

            _hardCurrencyLabel = root.Q<Label>("store-hard-currency-label");

            var addCurrencyButton = root.Q<Button>("store-add-currency-button");
            addCurrencyButton.text = UiText.Store.AddCurrencyButtonIcon;
            addCurrencyButton.clicked += () => _router.Toast.Show(UiText.Store.BuyCurrencyToast);

            _tabList = root.Q<VisualElement>("store-tab-list");
            _itemGrid = root.Q<VisualElement>("store-item-grid");

            BuildTabs();
            BuildPlaceholderGrid();
            ApplyLocalizedText();
        }

        private void ApplyLocalizedText()
        {
            _titleLabel.text = UiText.Store.Title;

            foreach (var pair in _tabButtons)
            {
                pair.Value.text = TabLabelFor(pair.Key);
            }
        }

        public void OnShow()
        {
            _hardCurrencyLabel.text = _gameManager.HardCurrencyBalance.ToString(CultureInfo.InvariantCulture);
        }

        public void OnHide()
        {
        }

        /// <summary>
        /// Also rebuilds the "Coming soon" placeholder cards (see <see cref="BuildPlaceholderGrid"/>)
        /// since their copy is otherwise only ever set once, at construction.
        /// </summary>
        public void RefreshLocalizedText()
        {
            ApplyLocalizedText();
            BuildPlaceholderGrid();
        }

        private void BuildTabs()
        {
            foreach (var tab in TabOrder)
            {
                var button = new Button { text = TabLabelFor(tab) };
                button.AddToClassList("tab-button");
                button.clicked += () => SelectTab(tab);

                _tabList.Add(button);
                _tabButtons[tab] = button;
            }

            SelectTab(Tab.Skins);
        }

        private static string TabLabelFor(Tab tab)
        {
            return tab switch
            {
                Tab.Skins => UiText.Store.TabSkins,
                Tab.Arenas => UiText.Store.TabArenas,
                Tab.Fx => UiText.Store.TabFx,
                _ => tab.ToString(),
            };
        }

        private void SelectTab(Tab tab)
        {
            foreach (var pair in _tabButtons)
            {
                pair.Value.EnableInClassList("tab-button--active", pair.Key == tab);
            }

            // The catalog is identical (placeholder) across tabs in Milestone 1 - no rebuild needed.
        }

        private void BuildPlaceholderGrid()
        {
            _itemGrid.Clear();

            for (int i = 0; i < PlaceholderCardCount; i++)
            {
                var card = new VisualElement();
                card.AddToClassList("card");
                card.AddToClassList("store-item-card");

                var icon = new Label(UiText.Icons.Store);
                icon.AddToClassList("store-item-card-icon");
                card.Add(icon);

                var title = new Label(UiText.Store.ComingSoonCardTitle);
                title.AddToClassList("store-item-card-title");
                card.Add(title);

                var description = new Label(UiText.Store.ComingSoonCardDescription);
                description.AddToClassList("store-item-card-description");
                card.Add(description);

                _itemGrid.Add(card);
            }
        }
    }
}
