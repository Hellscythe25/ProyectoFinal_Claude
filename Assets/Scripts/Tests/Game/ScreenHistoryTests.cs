using NUnit.Framework;
using TTTXO.Game.UI;

namespace TTTXO.Game.Tests
{
    /// <summary>
    /// The back-stack rules, exercised through a stand-in that reproduces
    /// <see cref="ScreenRouter"/>'s exact call sequence minus the UIDocument. The scenarios matter
    /// more than the primitives here: a back button that strands a player or ping-pongs between two
    /// screens is invisible until someone hits it, and the router itself cannot be instantiated in a
    /// test because <c>Initialize()</c> builds every screen against a live document.
    /// </summary>
    [TestFixture]
    public class ScreenHistoryTests
    {
        /// <summary>
        /// Mirrors <see cref="ScreenRouter.Show"/> and <see cref="ScreenRouter.GoBack"/>: the same
        /// order of operations, the same back flag, no visual side effects. If the router's own
        /// sequence ever changes, this has to change with it.
        /// </summary>
        private sealed class Navigator
        {
            private ScreenId? _current;
            private bool _navigatingBack;

            public ScreenHistory History { get; } = new();

            public ScreenId? Current => _current;

            public void Show(ScreenId target)
            {
                History.RecordNavigation(_current, target, _navigatingBack);
                _current = target;
            }

            public void GoBack(ScreenId fallback)
            {
                var target = History.ResolveBack(fallback);
                _navigatingBack = true;
                try
                {
                    Show(target);
                }
                finally
                {
                    _navigatingBack = false;
                }
            }
        }

        // -------------------------------------------------------------------------------------
        // The four rules, one at a time.
        // -------------------------------------------------------------------------------------

        [Test]
        public void NewHistory_IsEmpty()
        {
            var history = new ScreenHistory();

            Assert.AreEqual(0, history.Depth);
        }

        [Test]
        public void ResolveBack_ReturnsTheFallback_WhenThereIsNoHistory()
        {
            var history = new ScreenHistory();

            Assert.AreEqual(ScreenId.RpsMenu, history.ResolveBack(ScreenId.RpsMenu));
        }

        [Test]
        public void RecordNavigation_PushesTheScreenBeingLeft()
        {
            var history = new ScreenHistory();

            history.RecordNavigation(ScreenId.Store, ScreenId.Settings, isBack: false);

            Assert.AreEqual(1, history.Depth);
            Assert.AreEqual(ScreenId.Store, history.ResolveBack(ScreenId.RpsMenu));
        }

        [Test]
        public void RecordNavigation_PushesNothing_WhenThereIsNoScreenToLeave()
        {
            var history = new ScreenHistory();

            history.RecordNavigation(null, ScreenId.RpsMenu, isBack: false);

            Assert.AreEqual(0, history.Depth, "the first navigation of the session has nothing behind it");
        }

        [Test]
        public void RecordNavigation_PushesNothing_WhenTheTargetIsAlreadyShowing()
        {
            var history = new ScreenHistory();

            history.RecordNavigation(ScreenId.Store, ScreenId.Store, isBack: false);

            Assert.AreEqual(0, history.Depth, "a redundant Show must not make back a no-op that looks broken");
        }

        [Test]
        public void RecordNavigation_PushesNothing_WhenGoingBack()
        {
            var history = new ScreenHistory();

            history.RecordNavigation(ScreenId.Store, ScreenId.Settings, isBack: true);

            Assert.AreEqual(0, history.Depth, "the popped entry must not be pushed straight back on");
        }

        [Test]
        public void RecordNavigation_ClearsEverything_OnArrivingAtTheMenu()
        {
            var history = new ScreenHistory();
            history.RecordNavigation(ScreenId.RpsMenu, ScreenId.RpsModeSelect, isBack: false);
            history.RecordNavigation(ScreenId.RpsModeSelect, ScreenId.RpsCharacterSelect, isBack: false);

            history.RecordNavigation(ScreenId.RpsCharacterSelect, ScreenId.RpsMenu, isBack: false);

            Assert.AreEqual(0, history.Depth, "the menu is the hub and has no back button");
        }

        [Test]
        public void ResolveBack_ConsumesTheEntry()
        {
            var history = new ScreenHistory();
            history.RecordNavigation(ScreenId.RpsMenu, ScreenId.Store, isBack: false);
            history.RecordNavigation(ScreenId.Store, ScreenId.Settings, isBack: false);

            Assert.AreEqual(ScreenId.Store, history.ResolveBack(ScreenId.RpsMenu));
            Assert.AreEqual(ScreenId.RpsMenu, history.ResolveBack(ScreenId.RpsMenu));
            Assert.AreEqual(0, history.Depth);
        }

        [Test]
        public void Clear_DropsEverything()
        {
            var history = new ScreenHistory();
            history.RecordNavigation(ScreenId.RpsMenu, ScreenId.Store, isBack: false);

            history.Clear();

            Assert.AreEqual(0, history.Depth);
        }

        // -------------------------------------------------------------------------------------
        // Real navigation, driven through the router's own call sequence.
        // -------------------------------------------------------------------------------------

        [Test]
        public void StoreAndSettings_DoNotLoopBetweenEachOther()
        {
            // The failure a single-level "previous screen" field produces, and the reason the
            // history is a stack (see ScreenHistory's own docs).
            var nav = new Navigator();
            nav.Show(ScreenId.RpsMenu);
            nav.Show(ScreenId.Store);
            nav.Show(ScreenId.Settings);

            nav.GoBack(ScreenId.RpsMenu);
            Assert.AreEqual(ScreenId.Store, nav.Current);

            nav.GoBack(ScreenId.RpsMenu);
            Assert.AreEqual(ScreenId.RpsMenu, nav.Current, "back from Store reaches the menu instead of bouncing to Settings");
        }

        [Test]
        public void Back_WalksTheMatchFlowInReverseOrder()
        {
            var nav = new Navigator();
            nav.Show(ScreenId.RpsMenu);
            nav.Show(ScreenId.RpsModeSelect);
            nav.Show(ScreenId.RpsCharacterSelect);
            nav.Show(ScreenId.RpsVs);

            nav.GoBack(ScreenId.RpsMenu);
            Assert.AreEqual(ScreenId.RpsCharacterSelect, nav.Current);

            nav.GoBack(ScreenId.RpsMenu);
            Assert.AreEqual(ScreenId.RpsModeSelect, nav.Current);

            nav.GoBack(ScreenId.RpsMenu);
            Assert.AreEqual(ScreenId.RpsMenu, nav.Current);
        }

        [Test]
        public void Back_FallsBackOnceTheChainIsExhausted()
        {
            var nav = new Navigator();
            nav.Show(ScreenId.RpsMenu);
            nav.Show(ScreenId.Settings);

            nav.GoBack(ScreenId.RpsMenu);
            nav.GoBack(ScreenId.RpsMenu);

            Assert.AreEqual(ScreenId.RpsMenu, nav.Current, "back past the start lands on the fallback, never nowhere");
            Assert.AreEqual(0, nav.History.Depth);
        }

        [Test]
        public void SubScreens_ReturnToWhereverTheyWereOpenedFrom()
        {
            // Settings is reachable from the menu and from the Store; the point of the stack is
            // that back depends on the route taken, not on a hardcoded parent.
            var viaMenu = new Navigator();
            viaMenu.Show(ScreenId.RpsMenu);
            viaMenu.Show(ScreenId.Settings);
            viaMenu.GoBack(ScreenId.RpsMenu);
            Assert.AreEqual(ScreenId.RpsMenu, viaMenu.Current);

            var viaStore = new Navigator();
            viaStore.Show(ScreenId.RpsMenu);
            viaStore.Show(ScreenId.Store);
            viaStore.Show(ScreenId.Settings);
            viaStore.GoBack(ScreenId.RpsMenu);
            Assert.AreEqual(ScreenId.Store, viaStore.Current);
        }

        [Test]
        public void MatchFlowCycles_DoNotAccumulateHistory()
        {
            var nav = new Navigator();

            for (int i = 0; i < 10; i++)
            {
                nav.Show(ScreenId.RpsMenu);
                nav.Show(ScreenId.RpsModeSelect);
                nav.Show(ScreenId.RpsCharacterSelect);
                nav.Show(ScreenId.RpsVs);

                Assert.AreEqual(3, nav.History.Depth, $"cycle {i} should carry exactly one match flow");

                nav.Show(ScreenId.RpsMenu);
                Assert.AreEqual(0, nav.History.Depth, "returning to the menu drops the cycle");
            }
        }

        [Test]
        public void RepeatedShowOfTheSameScreen_DoesNotStackUp()
        {
            var nav = new Navigator();
            nav.Show(ScreenId.RpsMenu);
            nav.Show(ScreenId.Store);

            nav.Show(ScreenId.Store);
            nav.Show(ScreenId.Store);

            Assert.AreEqual(1, nav.History.Depth);

            nav.GoBack(ScreenId.RpsMenu);
            Assert.AreEqual(ScreenId.RpsMenu, nav.Current, "back still works after redundant navigations");
        }

        [Test]
        public void TheMenu_IsNotSomewhereBackCanReturnTo()
        {
            // The menu is the hub and clears the stack, so a back button can never land on a screen
            // that has no way out of it.
            var nav = new Navigator();
            nav.Show(ScreenId.RpsMenu);
            nav.Show(ScreenId.Store);
            nav.Show(ScreenId.Settings);

            nav.GoBack(ScreenId.RpsMenu);

            Assert.AreEqual(ScreenId.Store, nav.Current);
            Assert.AreEqual(1, nav.History.Depth);
        }
    }
}
