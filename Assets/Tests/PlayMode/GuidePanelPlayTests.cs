using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Shop;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// PlayMode tests for the <see cref="GuidePanel"/> alongside the Shop book, routed through
    /// <see cref="GameUI"/>'s key handlers: the ledger key is ignored over the guide, the guide opens
    /// over the book and Esc unwinds them in order with the clock paused until both are closed, and
    /// the guide takes keyboard focus from the menu behind it and hands it back on close.
    /// </summary>
    public class GuidePanelPlayTests
    {
        private static readonly Vector2Int FloorSize = new(6, 6);

        private GameObject  _root, _canvas, _events;
        private GameManager _game;
        private BuildMode   _build;
        private StatsPanel  _stats;
        private GuidePanel  _guide;
        private GameUI      _ui;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("guide-play-test");
            var grid = _root.AddComponent<GridManager>();
            var shop = _root.AddComponent<ShopManager>();
            grid.FillFloorRect(Vector2Int.zero, FloorSize);

            _game  = new GameObject("GameManager").AddComponent<GameManager>();
            _build = _root.AddComponent<BuildMode>();
            _build.GridManager = grid; _build.Shop = shop; _build.ObjectRoot = _root.transform;
            _build.Supply = _game.Furniture;
            _game.Grid = grid; _game.Shop = shop; _game.Build = _build;

            _events = new GameObject("EventSystem", typeof(EventSystem));
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _stats  = _canvas.AddComponent<StatsPanel>();
            _stats.Build(_canvas.transform, _game, null, _build);
            _guide  = _canvas.AddComponent<GuidePanel>();
            _guide.Build(_canvas.transform, _game);
            _stats.OpenGuide = _guide.Show;
            _stats.GuideOpen = () => _guide.IsOpen;
            _ui = _canvas.AddComponent<GameUI>();
            _ui.WireForTests(_game, _build, _stats.Catalogue, _stats, _guide);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            // Immediate: GameManager is a singleton, and a deferred destroy would make the next
            // test's fresh manager see this one as Instance and destroy itself.
            Object.DestroyImmediate(_canvas);
            Object.DestroyImmediate(_events);
            Object.DestroyImmediate(_root);
            if (_game != null) Object.DestroyImmediate(_game.gameObject);
        }

        [Test]
        public void LedgerKey_WhileGuideOpen_DoesNotOpenBook()
        {
            _guide.Show();

            _ui.HandleLedgerKey();

            Assert.IsFalse(_stats.IsOpen, "the book must not open over the guide");
            Assert.IsTrue(_guide.IsOpen);
            Assert.IsTrue(_game.IsModalOpen);
        }

        [Test]
        public void GuideKey_OverBook_EscUnwindsGuideThenBook()
        {
            _stats.Show();

            _ui.HandleGuideKey();
            Assert.IsTrue(_guide.IsOpen, "the guide opens over the book");
            Assert.IsTrue(_stats.IsOpen);

            _ui.HandleEscape();
            Assert.IsFalse(_guide.IsOpen, "the first Esc closes the guide");
            Assert.IsTrue(_stats.IsOpen, "the book is still open");
            Assert.IsTrue(_game.IsModalOpen, "the clock stays paused under the book");

            _ui.HandleEscape();
            Assert.IsFalse(_stats.IsOpen, "the second Esc closes the book");
            Assert.IsFalse(_game.IsModalOpen, "the clock runs again");
        }

        [Test]
        public void Show_ClearsSelection_AndHideRestoresIt()
        {
            var behind = UIFactory.Button("Behind", _canvas.transform, "Behind", Vector2.zero, Vector2.one);
            EventSystem.current.SetSelectedGameObject(behind.gameObject);

            _guide.Show();
            Assert.IsNull(EventSystem.current.currentSelectedGameObject, "the guide takes keyboard focus");

            _guide.Hide();
            Assert.AreSame(behind.gameObject, EventSystem.current.currentSelectedGameObject,
                "closing the guide hands focus back");
        }
    }
}
