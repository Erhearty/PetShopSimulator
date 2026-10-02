using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The furniture catalogue: a tab per <see cref="BuildCategory"/>, one row per item with its
    /// name, description, cost, owned and on-order counts, and Order / Place buttons. Ordering
    /// charges the catalogue price now and the item arrives later as a crate on the forecourt;
    /// placing takes an owned unit into the hand, for free, and closes the panel.
    ///
    /// Owns the modal flag while open, like the other panels. Arrow keys / Enter navigate the
    /// buttons; PageUp / PageDown turn pages; Esc (routed by GameUI) closes.
    /// </summary>
    public partial class FurnitureCatalogPanel : MonoBehaviour
    {
        /// <summary>Catalogue rows shown per page.</summary>
        public const int ItemsPerPage = 6;

        private const KeyCode NextPageKey = KeyCode.PageDown;
        private const KeyCode PrevPageKey = KeyCode.PageUp;

        private static readonly BuildCategory[] Tabs =
            (BuildCategory[])System.Enum.GetValues(typeof(BuildCategory));

        private sealed class Row
        {
            public GameObject Root;
            public string     Id;
            public TMP_Text   Name, Description, Cost, Counts;
            public Button     Order, Place;
        }

        private GameObject  _root;
        private GameManager _game;
        private BuildMode   _build;
        private TMP_Text    _status, _pageLabel, _balance;
        private Button      _prev, _next;
        private readonly List<Row>    _rows       = new();
        private readonly List<Button> _tabButtons = new();

        /// <summary>True while the catalogue is on screen.</summary>
        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>The category tab being shown.</summary>
        public BuildCategory Tab { get; private set; }

        /// <summary>Zero-based page within the current tab.</summary>
        public int Page { get; private set; }

        /// <summary>Pages in the current tab (at least one).</summary>
        public int PageCount => Mathf.Max(1, Mathf.CeilToInt(ItemsIn(Tab).Count / (float)ItemsPerPage));

        /// <summary>The last order/place message shown in the panel's status line.</summary>
        public string Status => _status != null ? _status.text : string.Empty;

        private FurnitureSupply Supply => _game != null ? _game.Furniture : null;

        /// <summary>Every catalogue entry in <paramref name="category"/>, in catalogue order.</summary>
        public static List<PlacedObjectData> ItemsIn(BuildCategory category)
        {
            var list = new List<PlacedObjectData>();
            foreach (var def in BuildCatalog.Items.Values)
                if (def.Category == category) list.Add(def);
            return list;
        }

        /// <summary>Builds the (hidden) panel under <paramref name="canvas"/>.</summary>
        public void Build(Transform canvas, GameManager game, BuildMode build)
        {
            _game  = game;
            _build = build;
            var panel = BuildFrame(canvas);
            BuildTabs(panel);
            BuildRows(panel);
            BuildFooter(panel);
            if (Supply != null) Supply.OnChanged += RefreshIfOpen;
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Supply != null) Supply.OnChanged -= RefreshIfOpen;
        }

        // ── Open / close ────────────────────────────────────────────────────────

        /// <summary>Opens on the current tab.</summary>
        public void Show()
        {
            if (_root == null) return;
            SetStatus(string.Empty, true);
            Refresh();
            _root.SetActive(true);
            _game?.SetModalOpen(true);
            SelectCurrentTab();
        }

        /// <summary>
        /// Opens on the tab and page holding <paramref name="focusId"/>; an unknown or null id
        /// keeps the current tab.
        /// </summary>
        public void Open(string focusId)
        {
            var def = BuildCatalog.Get(focusId);
            if (def != null)
            {
                Tab  = def.Category;
                Page = Mathf.Max(0, ItemsIn(Tab).IndexOf(def)) / ItemsPerPage;
            }
            Show();
        }

        /// <summary>Closes the catalogue and releases the modal flag.</summary>
        public void Hide()
        {
            if (_root == null) return;
            _root.SetActive(false);
            _game?.SetModalOpen(false);
        }

        // ── Actions ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Orders one <paramref name="catalogId"/> at catalogue cost. False (with a status
        /// message) when the id is unknown or the shop cannot afford it.
        /// </summary>
        public bool Order(string catalogId)
        {
            var def = BuildCatalog.Get(catalogId);
            if (def == null || _game == null) return false;

            bool ok = _game.OrderFurniture(catalogId);
            SetStatus(ok ? $"Ordered a {def.DisplayName} — it arrives on the forecourt later today."
                         : OrderFailure(def), ok);
            Refresh();
            return ok;
        }

        /// <summary>Why ordering <paramref name="def"/> failed: no shop to pay, or not enough money.</summary>
        private string OrderFailure(PlacedObjectData def) =>
            _game.Shop == null
                ? $"Cannot order a {def.DisplayName} — there is no shop to pay for it."
                : $"Not enough money for a {def.DisplayName} (€ {def.Cost:N0}).";

        /// <summary>
        /// Closes the panel and takes one owned <paramref name="catalogId"/> into the hand for
        /// placing. False (panel stays open, with a status message) when none is owned.
        /// </summary>
        public bool Place(string catalogId)
        {
            var def = BuildCatalog.Get(catalogId);
            if (def == null || _build == null || Supply == null) return false;
            if (Supply.OwnedCount(catalogId) <= 0)
            {
                SetStatus($"You have no {def.DisplayName} — order one first.", false);
                return false;
            }

            Hide();
            if (_build.EnterPlacement(catalogId, null)) return true;
            Show();
            SetStatus($"Could not pick up the {def.DisplayName}.", false);
            return false;
        }

        /// <summary>Switches to <paramref name="category"/>'s first page.</summary>
        public void SelectTab(BuildCategory category)
        {
            Tab  = category;
            Page = 0;
            Refresh();
        }

        /// <summary>Turns <paramref name="delta"/> pages, clamped to the tab.</summary>
        public void ChangePage(int delta)
        {
            Page = Mathf.Clamp(Page + delta, 0, PageCount - 1);
            Refresh();
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(NextPageKey)) ChangePage(1);
            if (Input.GetKeyDown(PrevPageKey)) ChangePage(-1);
        }

        // ── Content ─────────────────────────────────────────────────────────────

        private void RefreshIfOpen()
        {
            if (IsOpen) Refresh();
        }

        private void Refresh()
        {
            var items = ItemsIn(Tab);
            Page = Mathf.Clamp(Page, 0, PageCount - 1);
            for (int i = 0; i < _rows.Count; i++)
            {
                int index = Page * ItemsPerPage + i;
                FillRow(_rows[i], index < items.Count ? items[index] : null);
            }
            for (int i = 0; i < _tabButtons.Count; i++)
                _tabButtons[i].targetGraphic.color = Tabs[i] == Tab ? UIFactory.ButtonOn : UIFactory.ButtonBg;

            _pageLabel.text  = $"Page {Page + 1} / {PageCount}";
            _prev.interactable = Page > 0;
            _next.interactable = Page < PageCount - 1;
            _balance.text = _game != null && _game.Shop != null ? $"Balance € {_game.Shop.Balance:N0}" : string.Empty;
        }

        private void FillRow(Row row, PlacedObjectData def)
        {
            row.Id = def?.Id;
            row.Root.SetActive(def != null);
            if (def == null) return;

            int owned = Supply != null ? Supply.OwnedCount(def.Id) : 0;
            row.Name.text        = def.DisplayName;
            row.Description.text = def.Description;
            row.Cost.text        = $"€ {def.Cost:N0}";
            row.Cost.color       = CanAfford(def) ? UIFactory.Ink : UIFactory.Bad;
            row.Counts.text      = $"owned {owned}\non order {PendingCount(def.Id)}";
            row.Place.interactable = owned > 0;
        }

        private bool CanAfford(PlacedObjectData def) =>
            _game == null || _game.Shop == null || _game.Shop.Balance >= def.Cost;

        private int PendingCount(string id)
        {
            if (Supply == null) return 0;
            int n = 0;
            foreach (var o in Supply.Pending)
                if (o.CatalogId == id) n++;
            return n;
        }

        private void SetStatus(string message, bool good)
        {
            if (_status == null) return;
            _status.text  = message;
            _status.color = good ? UIFactory.InkMuted : UIFactory.Bad;
        }

        /// <summary>Gives keyboard focus to the current tab's button.</summary>
        private void SelectCurrentTab()
        {
            int index = System.Array.IndexOf(Tabs, Tab);
            if (EventSystem.current == null || index < 0 || index >= _tabButtons.Count) return;
            EventSystem.current.SetSelectedGameObject(_tabButtons[index].gameObject);
        }
    }
}
