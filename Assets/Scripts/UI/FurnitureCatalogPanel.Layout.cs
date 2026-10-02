using UnityEngine;
using TMPro;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>Construction of the catalogue's frame, tabs, item rows and footer.</summary>
    public partial class FurnitureCatalogPanel
    {
        private static readonly Vector2 Centre   = new(0.5f, 0.5f);
        private static readonly Vector2 HalfSize = new(480f, 320f);
        private static readonly Color   DimColour = new(0.03f, 0.05f, 0.08f, 0.62f);
        /// <summary>Row backing: a shade lighter than the panel so rows read as separate.</summary>
        private static readonly Color   RowColour = new(0.11f, 0.14f, 0.19f, 0.96f);

        private const float AccentHeight = 4f;
        private const float HeaderFont   = 24f;
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

        /// <summary>The dimmed backdrop, the panel and its header; returns the panel.</summary>
        private Transform BuildFrame(Transform canvas)
        {
            _root = UIFactory.Panel("CatalogueDim", canvas, Vector2.zero, Vector2.one, DimColour);
            var panel = UIFactory.Panel("Catalogue", _root.transform, Centre, Centre, UIFactory.PanelBg,
                                        -HalfSize, HalfSize).transform;
            UIFactory.Panel("Accent", panel, new Vector2(0f, 1f), Vector2.one, UIFactory.Accent,
                            new Vector2(0f, -AccentHeight), Vector2.zero);
            UIFactory.Label("Header", panel, "Furniture catalogue",
                new Vector2(Left, 0.89f), new Vector2(0.52f, 0.97f), HeaderFont, UIFactory.Ink);
            _balance = UIFactory.Label("Balance", panel, "", new Vector2(0.52f, 0.89f),
                new Vector2(0.78f, 0.97f), ButtonFont, UIFactory.InkMuted, TextAlignmentOptions.Right);
            UIFactory.Button("Close", panel, "Close", new Vector2(0.80f, 0.895f),
                new Vector2(Right, 0.965f), ButtonFont).onClick.AddListener(Hide);
            return panel;
        }

        private void BuildTabs(Transform panel)
        {
            float width = (Right - Left - TabGap * (Tabs.Length - 1)) / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
            {
                var   category = Tabs[i];
                float x0       = Left + i * (width + TabGap);
                var   btn = UIFactory.Button($"Tab_{category}", panel, category.ToString(),
                    new Vector2(x0, TabBottom), new Vector2(x0 + width, TabTop), ButtonFont);
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
            row.Description = UIFactory.Label("Description", t, "", new Vector2(0.01f, 0f),
                new Vector2(0.60f, 0.5f), BodyFont, UIFactory.InkMuted);
            row.Cost = UIFactory.Label("Cost", t, "", new Vector2(0.45f, 0.5f), new Vector2(0.60f, 1f),
                NameFont, UIFactory.Ink, TextAlignmentOptions.Right);
            row.Counts = UIFactory.Label("Counts", t, "", new Vector2(0.62f, 0f), new Vector2(0.76f, 1f),
                BodyFont, UIFactory.Ink, TextAlignmentOptions.Center);
            row.Order = UIFactory.Button("Order", t, "Order", new Vector2(0.77f, 0.15f),
                new Vector2(0.87f, 0.85f), ButtonFont);
            row.Order.onClick.AddListener(() => Order(row.Id));
            row.Place = UIFactory.Button("Place", t, "Place", new Vector2(0.88f, 0.15f),
                new Vector2(0.99f, 0.85f), ButtonFont, UIFactory.ButtonOn);
            row.Place.onClick.AddListener(() => Place(row.Id));
            return row;
        }

        private void BuildFooter(Transform panel)
        {
            _prev = UIFactory.Button("PrevPage", panel, "‹ Prev", new Vector2(Left, FooterBottom),
                new Vector2(0.14f, FooterTop), ButtonFont);
            _prev.onClick.AddListener(() => ChangePage(-1));
            _pageLabel = UIFactory.Label("Page", panel, "", new Vector2(0.15f, FooterBottom),
                new Vector2(0.29f, FooterTop), BodyFont, UIFactory.InkMuted, TextAlignmentOptions.Center);
            _next = UIFactory.Button("NextPage", panel, "Next ›", new Vector2(0.30f, FooterBottom),
                new Vector2(0.41f, FooterTop), ButtonFont);
            _next.onClick.AddListener(() => ChangePage(1));
            _status = UIFactory.Label("Status", panel, "", new Vector2(0.43f, FooterBottom),
                new Vector2(Right, FooterTop), BodyFont, UIFactory.InkMuted);
        }
    }
}
