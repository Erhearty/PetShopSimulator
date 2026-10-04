using UnityEngine;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The ledger's Build tab: hosts the <see cref="FurnitureCatalogPanel"/> content, so ordering
    /// furniture and picking pieces up to place lives in the ledger rather than its own modal.
    /// </summary>
    public partial class StatsPanel
    {
        /// <summary>Index of the Build tab.</summary>
        public const int BuildTab = 4;

        /// <summary>Number of ledger tabs.</summary>
        private const int TabCount = 5;

        /// <summary>The Build page runs down over the strip the other tabs keep for their buttons.</summary>
        private static readonly Vector2 BuildPageMin = new(0.03f, 0.015f);
        private static readonly Vector2 BuildPageMax = new(0.97f, 0.80f);

        private GameObject _buildPage;

        /// <summary>The furniture catalogue shown on the Build tab.</summary>
        public FurnitureCatalogPanel Catalogue { get; private set; }

        /// <summary>Creates the Build page and builds the catalogue content into it.</summary>
        private void BuildBuildPage(Transform panel, BuildMode build)
        {
            _buildPage = UIFactory.Node("BuildPage", panel, BuildPageMin, BuildPageMax);
            Catalogue  = gameObject.AddComponent<FurnitureCatalogPanel>();
            Catalogue.BuildContent(_buildPage.transform, _game, build, ShowBuild, Hide);
        }

        /// <summary>Opens the ledger (if closed) on the Build tab.</summary>
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
