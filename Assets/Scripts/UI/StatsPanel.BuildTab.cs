using System;
using UnityEngine;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The Shop book's Build page: hosts the <see cref="FurnitureCatalogPanel"/> content, so ordering
    /// furniture and picking pieces up to place lives in the book rather than its own modal, plus
    /// a way into the build view and its wall tools.
    /// </summary>
    public partial class StatsPanel
    {
        /// <summary>The catalogue sits between the purpose line and the tools row.</summary>
        private static readonly Vector2 BuildPageMin = new(0f, 0.11f);
        private static readonly Vector2 BuildPageMax = new(1f, 0.83f);

        private const float ToolRowBottom = 0.015f, ToolRowTop = 0.085f;
        private const float OpenViewWidth = 0.22f;

        /// <summary>Wall tools in the order of the build view's strip, with their button text.</summary>
        private static readonly (string id, string text)[] WallTools =
        {
            (BuildCatalog.Wall,       "Wall"),
            (BuildCatalog.WallWindow, "Window wall"),
            (BuildCatalog.WallDoor,   "Doorway"),
            (BuildCatalog.Fence,      "Fence"),
            (null,                    "Remove"),
        };

        private GameObject _buildPage;

        /// <summary>The furniture catalogue shown on the Build page.</summary>
        public FurnitureCatalogPanel Catalogue { get; private set; }

        /// <summary>Supplies the top-down build view (wired by <see cref="GameUI"/> once built).</summary>
        public Func<BuildCamera> BuildView { get; set; }

        /// <summary>The build view's tool strip, used by the wall tool buttons.</summary>
        public BuildToolbar Toolbar { get; set; }

        /// <summary>Creates the Build page and builds the catalogue content into it.</summary>
        private void BuildBuildPage(BuildMode build)
        {
            var page = AddPage("Build", "Order furniture, place what you own, and change the walls.", UIFactory.TextSmall);
            _buildPage = UIFactory.Node("BuildPage", page.Root.transform, BuildPageMin, BuildPageMax);
            Catalogue  = gameObject.AddComponent<FurnitureCatalogPanel>();
            Catalogue.BuildContent(_buildPage.transform, _game, build, ShowBuild, Hide);
            Catalogue.InputBlocked = () => GuideIsOpen;
            BuildToolRow(page.Root.transform);
        }

        private void BuildToolRow(Transform page)
        {
            var open = UIFactory.Button("OpenBuildView", page, "Open build view", new Vector2(0f, ToolRowBottom),
                new Vector2(OpenViewWidth, ToolRowTop), UIFactory.TextSmall, UIFactory.Accent);
            open.onClick.AddListener(() => EnterBuildView(null));

            float w = (1f - OpenViewWidth) / WallTools.Length;
            for (int i = 0; i < WallTools.Length; i++)
            {
                var (id, text) = WallTools[i];
                float x = OpenViewWidth + ButtonGap + i * w;
                var btn = UIFactory.Button($"Wall_{text}", page, text, new Vector2(x, ToolRowBottom),
                    new Vector2(x + w - ButtonGap, ToolRowTop), UIFactory.TextSmall);
                btn.onClick.AddListener(() => EnterBuildView(() => PickTool(id)));
            }
        }

        /// <summary>Closes the book, enters the build view and then picks a tool if given.</summary>
        private void EnterBuildView(Action pick)
        {
            Hide();
            var view = BuildView?.Invoke();
            if (view != null && !view.IsActive) view.Enter();
            pick?.Invoke();
        }

        private void PickTool(string id)
        {
            if (Toolbar == null) return;
            if (id == null) Toolbar.SelectRemove();
            else Toolbar.SelectPiece(id);
        }

        /// <summary>Opens the book (if closed) on the Build page.</summary>
        public void ShowBuild()
        {
            if (!IsOpen) Show();
            ShowPage(BuildTab);
        }

        private void ShowBuildPage(bool visible)
        {
            if (_buildPage == null) return;
            _buildPage.SetActive(visible);
            if (visible && Catalogue != null) Catalogue.Activate();
        }
    }
}
