using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PetShop.Core;
using PetShop.Localization;
using PetShop.Shop;
using PetShop.Progression;

namespace PetShop.UI
{
    /// <summary>
    /// The furniture catalogue content: a tab per <see cref="BuildCategory"/>, one row per item
    /// with its name, description, cost, owned and on-order counts, and Order / Place buttons.
    /// Ordering charges the catalogue price now and the item arrives later as a crate on the
    /// forecourt; placing takes an owned unit into the hand, for free, and closes the host.
    ///
    /// Built into a page of a host panel (the ledger's Build tab), which owns the modal flag;
    /// <see cref="Show"/> / <see cref="Hide"/> open and close the host. Arrow keys / Enter
    /// navigate the buttons; PageUp / PageDown turn pages; Esc (routed by GameUI) closes.
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
        private System.Action _openHost, _closeHost;

        /// <summary>True while the catalogue is on screen (its host is open on its page).</summary>
        public bool IsOpen => _root != null && _root.activeInHierarchy;

        /// <summary>When it returns true (the guide is open over the book) the page keys are ignored.</summary>
        public System.Func<bool> InputBlocked { get; set; }

        /// <summary>The category tab being shown.</summary>
        public BuildCategory Tab { get; private set; }

        /// <summary>Zero-based page within the current tab.</summary>
        public int Page { get; private set; }

        /// <summary>Pages in the current tab (at least one).</summary>
        public int PageCount => Mathf.Max(1, Mathf.CeilToInt(ItemsIn(Tab).Count / (float)ItemsPerPage));

        /// <summary>The last order/place message shown in the panel's status line.</summary>
        public string Status => _status != null ? _status.text : string.Empty;

        private FurnitureSupply Supply => _game != null ? _game.Furniture : null;

        /// <summary>The top-down build view entered when building pieces are picked. Optional.</summary>
        public BuildCamera BuildView { get; set; }

        /// <summary>The shop's progression tier; the starting tier when no director is wired.</summary>
        private int Tier => _game != null && _game.Progression != null
            ? _game.Progression.Tier : ProgressionRules.CornerShopTier;

        /// <summary>Every listed catalogue entry in <paramref name="category"/>, in catalogue order.</summary>
        public static List<PlacedObjectData> ItemsIn(BuildCategory category)
        {
            var list = new List<PlacedObjectData>();
            foreach (var def in BuildCatalog.Items.Values)
                if (def.Category == category && !def.Hidden) list.Add(def);
            return list;
        }

        /// <summary>
        /// Builds the catalogue content into <paramref name="page"/>, a page of a host panel.
        /// <paramref name="openHost"/> opens the host on this page; <paramref name="closeHost"/>
        /// closes it. The host calls <see cref="Activate"/> whenever the page is shown.
        /// </summary>
        public void BuildContent(Transform page, GameManager game, BuildMode build,
                                 System.Action openHost, System.Action closeHost)
        {
            _game      = game;
            _build     = build;
            _openHost  = openHost;
            _closeHost = closeHost;
            _root      = page.gameObject;
            BuildBalance(page);
            BuildTabs(page);
            BuildRows(page);
            BuildFooter(page);
            if (Supply != null) Supply.OnChanged += RefreshIfOpen;
            Loc.LanguageChanged += RefreshIfOpen;
        }

        private void OnDestroy()
        {
            if (Supply != null) Supply.OnChanged -= RefreshIfOpen;
            Loc.LanguageChanged -= RefreshIfOpen;
        }

        // ── Open / close ────────────────────────────────────────────────────────

        /// <summary>Opens the host on the catalogue page, on the current tab.</summary>
        public void Show()
        {
            if (_root == null) return;
            if (_openHost != null) _openHost();
            else Activate();
        }

        /// <summary>Called by the host each time the catalogue page is shown: clears the status and refreshes.</summary>
        public void Activate()
        {
            if (_root == null) return;
            SetStatus(string.Empty, true);
            Refresh();
            _game?.Quests?.RaiseFlag(PetShop.Progression.Quests.QuestFlags.CatalogueOpened);
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

        /// <summary>Closes the host panel, which releases the modal flag.</summary>
        public void Hide()
        {
            if (_root == null) return;
            _closeHost?.Invoke();
        }

        // ── Actions ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Orders one <paramref name="catalogId"/> at catalogue cost. Building pieces are not
        /// delivered: they close the panel and go straight into paid placement in the build view,
        /// charged as each one is placed. False (with a status message) when the id is unknown,
        /// locked at the current tier, or the shop cannot afford it.
        /// </summary>
        public bool Order(string catalogId)
        {
            var def = BuildCatalog.Get(catalogId);
            if (def == null || _game == null) return false;
            if (!ProgressionRules.IsPenUnlocked(def.Id, Tier))
            {
                SetStatus($"{def.LocalizedName} {LockedText(def)}.", false);
                return false;
            }
            if (BuildCatalog.IsBuildingPiece(def.Id)) return StartBuilding(def);

            bool ok = _game.OrderFurniture(catalogId);
            SetStatus(ok ? Loc.F("catalog.ordered", def.LocalizedName)
                         : OrderFailure(def), ok);
            Refresh();
            return ok;
        }

        /// <summary>
        /// Closes the panel and opens pay-on-place build mode for <paramref name="def"/> in the
        /// build view; it stays in placement so a run of walls can be laid one after another.
        /// </summary>
        private bool StartBuilding(PlacedObjectData def)
        {
            if (_build == null) return false;
            Hide();
            _build.EnterBuildMode(def);
            if (BuildView != null) BuildView.Enter();
            return true;
        }

        /// <summary>“unlocks at &lt;tier name&gt;” for the first tier at which <paramref name="def"/> may be bought.</summary>
        private static string LockedText(PlacedObjectData def)
        {
            int tier = ProgressionRules.CornerShopTier;
            while (tier < ProgressionRules.MaxTier && !ProgressionRules.IsPenUnlocked(def.Id, tier)) tier++;
            return Loc.F("catalog.unlocks_at", ProgressionRules.TierName(tier));
        }

        /// <summary>Why ordering <paramref name="def"/> failed: no shop to pay, or not enough money.</summary>
        private string OrderFailure(PlacedObjectData def) =>
            _game.Shop == null
                ? Loc.F("catalog.no_shop", def.LocalizedName)
                : Loc.F("catalog.no_money", def.LocalizedName, def.Cost);

        /// <summary>
        /// Closes the panel and takes one owned <paramref name="catalogId"/> into the hand for
        /// placing in the build view. False (panel stays open, with a status message) when none
        /// is owned.
        /// </summary>
        public bool Place(string catalogId)
        {
            var def = BuildCatalog.Get(catalogId);
            if (def == null || _build == null || Supply == null) return false;
            if (Supply.OwnedCount(catalogId) <= 0)
            {
                SetStatus(Loc.F("catalog.none_owned", def.LocalizedName), false);
                return false;
            }

            Hide();
            if (_build.EnterPlacement(catalogId, null))
            {
                if (BuildView != null) BuildView.Enter();
                return true;
            }
            Show();
            SetStatus(Loc.F("catalog.pickup_failed", def.LocalizedName), false);
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
            if (!IsOpen || (InputBlocked != null && InputBlocked())) return;
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
                UIFactory.SetSelected(_tabButtons[i], Tabs[i] == Tab);

            _pageLabel.text  = Loc.F("catalog.page", Page + 1, PageCount);
            _prev.interactable = Page > 0;
            _next.interactable = Page < PageCount - 1;
            _balance.text = _game != null && _game.Shop != null ? Loc.F("catalog.balance", _game.Shop.Balance) : string.Empty;
        }

        private void FillRow(Row row, PlacedObjectData def)
        {
            row.Id = def?.Id;
            row.Root.SetActive(def != null);
            if (def == null) return;

            int  owned    = Supply != null ? Supply.OwnedCount(def.Id) : 0;
            bool unlocked = ProgressionRules.IsPenUnlocked(def.Id, Tier);
            row.Name.text        = unlocked ? def.LocalizedName : $"{def.LocalizedName} — {LockedText(def)}";
            row.Name.color       = unlocked ? UIFactory.Ink : UIFactory.InkMuted;
            row.Order.interactable = unlocked;
            row.Description.text = def.LocalizedDescription;
            row.Cost.text        = $"€ {def.Cost:N0}";
            row.Cost.color       = CanAfford(def) ? UIFactory.Ink : UIFactory.Bad;
            row.Counts.text      = Loc.F("catalog.counts", owned, PendingCount(def.Id));
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
