using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Localization;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The bottom-of-screen inventory: one slot per owned furniture piece (icon swatch, name,
    /// count) bound to <see cref="FurnitureSupply"/>, plus a strip of stockroom unit counts.
    /// Slot N is picked with number key N or a click and starts placement of that piece. Hidden
    /// while a modal or the game-over screen is up.
    /// </summary>
    public class InventoryBar : MonoBehaviour
    {
        /// <summary>Most slots shown (number keys 1-9).</summary>
        public const int MaxSlots = 9;

        private const float SlotWidth  = 96f;
        private const float SlotHeight = 88f;
        private const float BarBottom  = 14f;
        private const float StripHeight = 24f;

        /// <summary>Top edge of the bar above the screen bottom, so other HUD can sit above it.</summary>
        public const float TopEdge = BarBottom + SlotHeight + StripHeight + UIFactory.Gap;

        private sealed class Slot
        {
            public GameObject Root;
            public Image      Swatch;
            public TMP_Text   Letter, Name, Count, Key;
            public string     Id;
        }

        private FurnitureSupply _supply;
        private ShopManager     _shop;
        private BuildMode       _build;
        private Func<bool>     _hidden;

        private GameObject _root;
        private TMP_Text   _stock;
        private string     _lastStock;
        private float      _stockTimer;

        private readonly List<Slot>   _slots = new();
        private readonly List<string> _ids   = new();

        /// <summary>Catalogue ids shown, in slot order (slot 1 first).</summary>
        public IReadOnlyList<string> SlotIds => _ids;

        /// <summary>Number of occupied slots currently shown.</summary>
        public int SlotCount => _ids.Count;

        /// <summary>True while the bar is on screen.</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        /// <summary>The stockroom strip text.</summary>
        public string StockText => _stock != null ? _stock.text : string.Empty;

        /// <summary>The count text of slot <paramref name="index"/> (0-based), or null.</summary>
        public string CountText(int index) => index >= 0 && index < _ids.Count ? _slots[index].Count.text : null;

        /// <summary>The name text of slot <paramref name="index"/> (0-based), or null.</summary>
        public string NameText(int index) => index >= 0 && index < _ids.Count ? _slots[index].Name.text : null;

        /// <summary>
        /// Builds the bar under <paramref name="canvas"/>. <paramref name="isHidden"/> says when it
        /// should be off screen (a modal open, game over); null means never.
        /// </summary>
        public void Build(Transform canvas, FurnitureSupply supply, ShopManager shop, BuildMode build,
                          Func<bool> isHidden = null)
        {
            _supply = supply;
            _shop   = shop;
            _build  = build;
            _hidden = isHidden;

            float half = MaxSlots * (SlotWidth + UIFactory.Gap) * 0.5f;
            _root = UIFactory.Node("InventoryBar", canvas, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                   new Vector2(-half, BarBottom), new Vector2(half, TopEdge));

            var stripCard = UIFactory.Card("StockStrip", _root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                           UIFactory.CardBg, new Vector2(-half * 0.8f, -StripHeight), new Vector2(half * 0.8f, 0f));
            _stock = UIFactory.Label("Stock", stripCard.transform, "", Vector2.zero, Vector2.one, 14f,
                                     UIFactory.InkMuted, TextAlignmentOptions.Center);
            _stock.overflowMode = TextOverflowModes.Ellipsis;

            for (int i = 0; i < MaxSlots; i++) _slots.Add(BuildSlot(i));

            if (_supply != null) _supply.OnChanged += Refresh;
            Loc.LanguageChanged += OnLanguageChanged;
            Refresh();
            RefreshStock(true);
        }

        private Slot BuildSlot(int index)
        {
            var slot = new Slot();
            var card = UIFactory.Card($"Slot{index + 1}", _root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                      UIFactory.CardBg);
            slot.Root = card;

            var btn = card.AddComponent<Button>();
            btn.targetGraphic = card.GetComponent<Image>();
            card.GetComponent<Image>().raycastTarget = true;
            int captured = index;
            btn.onClick.AddListener(() => Activate(captured));

            var swatchGo = UIFactory.Card("Icon", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                          UIFactory.NoTint, new Vector2(8f, -38f), new Vector2(38f, -8f));
            slot.Swatch = swatchGo.GetComponent<Image>();
            slot.Letter = UIFactory.Label("Letter", swatchGo.transform, "", Vector2.zero, Vector2.one, 16f,
                                          UIFactory.NoTint, TextAlignmentOptions.Center);

            slot.Key = UIFactory.Label("Key", card.transform, (index + 1).ToString(), new Vector2(0.55f, 0.55f), new Vector2(0.96f, 0.96f),
                                       13f, UIFactory.InkMuted, TextAlignmentOptions.TopRight);
            slot.Count = UIFactory.Label("Count", card.transform, "", new Vector2(0.45f, 0.46f), new Vector2(0.96f, 0.74f),
                                         17f, UIFactory.Accent, TextAlignmentOptions.MidlineRight);
            slot.Name = UIFactory.Label("Name", card.transform, "", new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.42f),
                                        14f, UIFactory.Ink, TextAlignmentOptions.Center);
            slot.Name.enableAutoSizing = true;
            slot.Name.fontSizeMin      = 9f;
            slot.Name.fontSizeMax      = 14f;
            card.SetActive(false);
            return slot;
        }

        private void OnDestroy()
        {
            if (_supply != null) _supply.OnChanged -= Refresh;
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        /// <summary>Re-reads the slot names and stockroom strip in the new language.</summary>
        private void OnLanguageChanged()
        {
            Refresh();
            RefreshStock(true);
        }

        /// <summary>Rebuilds the slots from the supply's owned furniture.</summary>
        public void Refresh()
        {
            if (_supply == null || _slots.Count == 0) return;

            _ids.Clear();
            var owned = new List<string>();
            foreach (var kvp in _supply.Owned)
                if (kvp.Value > 0 && BuildCatalog.Get(kvp.Key) != null) owned.Add(kvp.Key);
            owned.Sort(StringComparer.Ordinal);
            for (int i = 0; i < owned.Count && i < MaxSlots; i++) _ids.Add(owned[i]);

            float step  = SlotWidth + UIFactory.Gap;
            float start = -(_ids.Count - 1) * step * 0.5f;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                bool used = i < _ids.Count;
                slot.Root.SetActive(used);
                if (!used) { slot.Id = null; continue; }

                var def = BuildCatalog.Get(_ids[i]);
                slot.Id = _ids[i];
                var rt = (RectTransform)slot.Root.transform;
                rt.anchoredPosition = new Vector2(start + i * step, 0f);
                rt.sizeDelta        = new Vector2(SlotWidth, SlotHeight);
                rt.pivot            = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(start + i * step, 0f);

                slot.Swatch.color = def.Tint;
                string name = def.LocalizedName ?? "";
                slot.Letter.text  = name.Length > 0 ? name.Substring(0, 1) : "?";
                slot.Name.text    = name;
                slot.Count.text   = "×" + _supply.OwnedCount(_ids[i]);
            }
        }

        /// <summary>Starts placing the piece in slot <paramref name="index"/> (0-based). False when empty or refused.</summary>
        public bool Activate(int index)
        {
            if (_build == null || index < 0 || index >= _ids.Count) return false;
            if (IsHidden()) return false;
            return _build.EnterPlacement(_ids[index]);
        }

        private bool IsHidden() => _hidden != null && _hidden();

        private void Update()
        {
            if (_root == null) return;

            bool hide = IsHidden();
            if (_root.activeSelf == hide) _root.SetActive(!hide);
            if (hide) return;

            for (int i = 0; i < _ids.Count; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { Activate(i); break; }

            _stockTimer -= Time.unscaledDeltaTime;
            if (_stockTimer <= 0f) { _stockTimer = 0.5f; RefreshStock(false); }
        }

        private void RefreshStock(bool force)
        {
            if (_stock == null) return;

            var sb = new StringBuilder(Loc.T("inventory.stockroom")).Append("  ");
            bool any = false;
            foreach (ProductCategory c in Enum.GetValues(typeof(ProductCategory)))
            {
                int units = _shop != null ? _shop.Warehouse(c) : 0;
                if (any) sb.Append("  \u00b7  ");
                sb.Append(LocNames.CategoryTitle(c)).Append(' ').Append(units);
                any = true;
            }
            string text = sb.ToString();
            if (!force && text == _lastStock) return;
            _lastStock  = text;
            _stock.text = text;
        }
    }
}
