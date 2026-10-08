using UnityEngine;
using TMPro;
using PetShop.Localization;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>Construction of the catalogue's balance line, tabs, item rows and footer.</summary>
    public partial class FurnitureCatalogPanel
    {
        /// <summary>Row backing: a shade lighter than the panel so rows read as separate.</summary>
        private static readonly Color   RowColour = UIFactory.Raised;

        private const float BalanceLeft   = 0.60f;
        private const float BalanceBottom = 0.88f;
        private const float BalanceTop    = 0.98f;
        private const float NameFont     = 17f;
        private const float BodyFont     = 13f;
        private const float ButtonFont   = 15f;

        private const float Left      = 0.03f;
        private const float Right     = 0.97f;
        private const float TabBottom = 0.80f;
        private const float TabTop    = 0.87f;
        private const float TabGap    = 0.01f;
        private const float RowsTop    = 0.77f;
        private const float RowsBottom = 0.12f;
        /// <summary>Share of each row slot the row itself fills; the rest is the gap below it.</summary>
        private const float RowFill    = 0.9f;
        private const float FooterBottom = 0.03f;
        private const float FooterTop    = 0.09f;

        /// <summary>The balance line above the category tabs; the host owns the header and Close.</summary>
        private void BuildBalance(Transform page)
        {
            _balance = UIFactory.Label("Balance", page, "", new Vector2(BalanceLeft, BalanceBottom),
                new Vector2(Right, BalanceTop), ButtonFont, UIFactory.InkMuted, TextAlignmentOptions.Right);
        }

        private void BuildTabs(Transform panel)
        {
            float width = (Right - Left - TabGap * (Tabs.Length - 1)) / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
            {
                var   category = Tabs[i];
                float x0       = Left + i * (width + TabGap);
                var   btn = UIFactory.Button($"Tab_{category}", panel, "",
                    new Vector2(x0, TabBottom), new Vector2(x0 + width, TabTop), ButtonFont);
                LocalizedText.Bind(btn.GetComponentInChildren<TMP_Text>(), () => LocNames.Of("catalog.tab", category));
                btn.onClick.AddListener(() => SelectTab(category));
                _tabButtons.Add(btn);
            }
        }

        private void BuildRows(Transform panel)
        {
            float slot = (RowsTop - RowsBottom) / ItemsPerPage;
            for (int i = 0; i < ItemsPerPage; i++)
            {
                float top = RowsTop - i * slot;
                _rows.Add(BuildRow(panel, i, top - slot * RowFill, top));
            }
        }

        private Row BuildRow(Transform panel, int index, float y0, float y1)
        {
            var row = new Row();
            row.Root = UIFactory.Panel($"Row_{index}", panel, new Vector2(Left, y0), new Vector2(Right, y1), RowColour);
            var t = row.Root.transform;
            row.Name = UIFactory.Label("Name", t, "", new Vector2(0.01f, 0.5f), new Vector2(0.44f, 1f), NameFont);
            UIFactory.AutoFit(row.Name);   // a long or locked name shrinks instead of being cut off
            row.Description = UIFactory.Label("Description", t, "", new Vector2(0.01f, 0f),
                new Vector2(0.60f, 0.5f), BodyFont, UIFactory.InkMuted);
            row.Cost = UIFactory.Label("Cost", t, "", new Vector2(0.45f, 0.5f), new Vector2(0.60f, 1f),
                NameFont, UIFactory.Ink, TextAlignmentOptions.Right);
            row.Counts = UIFactory.Label("Counts", t, "", new Vector2(0.62f, 0f), new Vector2(0.76f, 1f),
                BodyFont, UIFactory.Ink, TextAlignmentOptions.Center);
            row.Order = UIFactory.ButtonKey("Order", t, "catalog.order", new Vector2(0.77f, 0.15f),
                new Vector2(0.87f, 0.85f), ButtonFont);
            row.Order.onClick.AddListener(() => Order(row.Id));
            row.Place = UIFactory.ButtonKey("Place", t, "catalog.place", new Vector2(0.88f, 0.15f),
                new Vector2(0.99f, 0.85f), ButtonFont, UIFactory.ButtonOn);
            row.Place.onClick.AddListener(() => Place(row.Id));
            return row;
        }

        private void BuildFooter(Transform panel)
        {
            _prev = UIFactory.ButtonKey("PrevPage", panel, "catalog.prev", new Vector2(Left, FooterBottom),
                new Vector2(0.14f, FooterTop), ButtonFont);
            _prev.onClick.AddListener(() => ChangePage(-1));
            _pageLabel = UIFactory.Label("Page", panel, "", new Vector2(0.15f, FooterBottom),
                new Vector2(0.29f, FooterTop), BodyFont, UIFactory.InkMuted, TextAlignmentOptions.Center);
            UIFactory.AutoFit(_pageLabel);
            _next = UIFactory.ButtonKey("NextPage", panel, "catalog.next", new Vector2(0.30f, FooterBottom),
                new Vector2(0.41f, FooterTop), ButtonFont);
            _next.onClick.AddListener(() => ChangePage(1));
            _status = UIFactory.Label("Status", panel, "", new Vector2(0.43f, FooterBottom),
                new Vector2(Right, FooterTop), BodyFont, UIFactory.InkMuted);
        }
    }
}
