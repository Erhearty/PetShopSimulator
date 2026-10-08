using UnityEngine;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The Shop book's Build page: hosts the <see cref="FurnitureCatalogPanel"/> content, so ordering
    /// furniture and picking pieces up to place lives in the book rather than its own modal.
    /// </summary>
    public partial class StatsPanel
    {
        /// <summary>The catalogue fills the page below the purpose line.</summary>
        private static readonly Vector2 BuildPageMin = new(0f, 0.015f);
        private static readonly Vector2 BuildPageMax = new(1f, 0.83f);

        private GameObject _buildPage;

        /// <summary>The furniture catalogue shown on the Build page.</summary>
        public FurnitureCatalogPanel Catalogue { get; private set; }

        /// <summary>Creates the Build page and builds the catalogue content into it.</summary>
        private void BuildBuildPage(BuildMode build)
        {
            var page = AddPage("Build", UIFactory.TextSmall);
            _buildPage = UIFactory.Node("BuildPage", page.Root.transform, BuildPageMin, BuildPageMax);
            Catalogue  = gameObject.AddComponent<FurnitureCatalogPanel>();
            Catalogue.BuildContent(_buildPage.transform, _game, build, ShowBuild, Hide);
            Catalogue.InputBlocked = () => GuideIsOpen;
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
