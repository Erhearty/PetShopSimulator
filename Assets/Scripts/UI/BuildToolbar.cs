using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PetShop.Core;
using PetShop.Commerce;
using PetShop.Progression;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The build view's tool strip: Wall, Window wall, Doorway, Fence and Remove, each with its
    /// hotkey on the button. A building tool opens paid build mode for that piece
    /// (<see cref="BuildMode.EnterBuildMode"/>); Remove opens <see cref="BuildMode.EnterRemoveMode"/>.
    /// Shown only while the top-down build view is active and no modal is up. Esc cancels the
    /// tool through <see cref="GameUI.HandleEscape"/> before it leaves the view.
    /// </summary>
    public class BuildToolbar : MonoBehaviour
    {
        /// <summary>Hotkey for the Wall tool.</summary>
        public const KeyCode WallKey       = KeyCode.Z;
        /// <summary>Hotkey for the Window wall tool.</summary>
        public const KeyCode WindowWallKey = KeyCode.X;
        /// <summary>Hotkey for the Doorway tool.</summary>
        public const KeyCode DoorwayKey    = KeyCode.C;
        /// <summary>Hotkey for the Fence tool.</summary>
        public const KeyCode FenceKey      = KeyCode.V;
        /// <summary>Hotkey for the Remove tool.</summary>
        public const KeyCode RemoveKey     = KeyCode.G;

        private const float ButtonWidth  = 132f;
        private const float ButtonHeight = 46f;
        private const float FontSize     = 15f;

        /// <summary>One button: the catalogue id it places (null for Remove) and its hotkey.</summary>
        private sealed class Tool
        {
            public string  Id;
            public KeyCode Key;
            public Button  Button;
            public Image   Image;
        }

        /// <summary>The building tools in strip order; Remove follows them.</summary>
        private static readonly (string id, KeyCode key)[] BuildingTools =
        {
            (BuildCatalog.Wall,       WallKey),
            (BuildCatalog.WallWindow, WindowWallKey),
            (BuildCatalog.WallDoor,   DoorwayKey),
            (BuildCatalog.Fence,      FenceKey),
        };

        private readonly List<Tool> _tools = new();
        private GameManager       _game;
        private ShopManager       _shop;
        private BuildMode         _build;
        private Func<BuildCamera> _view;
        private Func<bool>        _hidden;
        private GameObject        _root;

        /// <summary>True while the strip is on screen.</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        /// <summary>
        /// Builds the strip under <paramref name="canvas"/>, just above the inventory bar.
        /// <paramref name="view"/> supplies the build view (wired after the UI is built);
        /// <paramref name="isHidden"/> says when a modal or game over hides it.
        /// </summary>
        public void Build(Transform canvas, GameManager game, ShopManager shop, BuildMode build,
                          Func<BuildCamera> view, Func<bool> isHidden = null)
        {
            _game = game; _shop = shop; _build = build; _view = view; _hidden = isHidden;

            int count = BuildingTools.Length + 1;
            float half   = count * (ButtonWidth + UIFactory.Gap) * 0.5f;
            float bottom = InventoryBar.TopEdge + UIFactory.Gap;
            _root = UIFactory.Node("BuildToolbar", canvas, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                   new Vector2(-half, bottom), new Vector2(half, bottom + ButtonHeight));

            for (int i = 0; i < BuildingTools.Length; i++)
                AddTool(i, count, BuildingTools[i].id, BuildingTools[i].key);
            AddTool(BuildingTools.Length, count, null, RemoveKey);
            _root.SetActive(false);
        }

        private void AddTool(int index, int count, string id, KeyCode key)
        {
            var def   = BuildCatalog.Get(id);
            string text = def != null ? $"{def.DisplayName}  €{def.Cost:N0}\n[{KeyLabel(key)}]" : $"Remove\n[{KeyLabel(key)}]";
            var btn = UIFactory.Button($"Tool_{id ?? "remove"}", _root.transform, text,
                                       new Vector2(index / (float)count, 0f), new Vector2((index + 1) / (float)count, 1f),
                                       FontSize);
            var rt = (RectTransform)btn.transform;
            rt.offsetMin = new Vector2(UIFactory.Gap * 0.5f, 0f);
            rt.offsetMax = new Vector2(-UIFactory.Gap * 0.5f, 0f);

            var tool = new Tool { Id = id, Key = key, Button = btn, Image = btn.GetComponent<Image>() };
            btn.onClick.AddListener(() => Activate(tool));
            _tools.Add(tool);
        }

        /// <summary>The hotkey's label as printed on its button.</summary>
        public static string KeyLabel(KeyCode key) => key.ToString();

        /// <summary>
        /// Picks the building tool for <paramref name="catalogId"/>: opens paid build mode for it.
        /// False (with a notice) when unknown, locked at the shop's tier, or unaffordable.
        /// </summary>
        public bool SelectPiece(string catalogId)
        {
            var def = BuildCatalog.Get(catalogId);
            if (def == null || _build == null) return false;
            if (!ProgressionRules.IsPenUnlocked(catalogId, Tier))
            {
                Notify($"{def.DisplayName} is not unlocked yet.");
                return false;
            }
            if (_shop != null && _shop.Balance < def.Cost)
            {
                Notify($"Not enough money — {def.DisplayName} costs €{def.Cost:N0}.");
                return false;
            }
            _build.EnterBuildMode(def);
            return true;
        }

        /// <summary>Picks the Remove tool.</summary>
        public void SelectRemove()
        {
            if (_build != null) _build.EnterRemoveMode();
        }

        private void Activate(Tool tool)
        {
            if (tool.Id == null) SelectRemove();
            else SelectPiece(tool.Id);
        }

        private int Tier => _game != null && _game.Progression != null
            ? _game.Progression.Tier : ProgressionRules.CornerShopTier;

        private void Notify(string message)
        {
            if (_game != null) _game.Notify(message);
        }

        private bool ShouldShow()
        {
            var view = _view?.Invoke();
            return view != null && view.IsActive && (_hidden == null || !_hidden());
        }

        private void Update()
        {
            if (_root == null) return;
            bool show = ShouldShow();
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) return;

            foreach (var tool in _tools)
                if (Input.GetKeyDown(tool.Key)) { Activate(tool); break; }
            RefreshButtons();
        }

        /// <summary>Lights the selected tool and greys out pieces the shop cannot afford.</summary>
        private void RefreshButtons()
        {
            foreach (var tool in _tools)
            {
                bool selected = _build != null && _build.IsActive &&
                                (tool.Id == null ? _build.IsRemoving
                                                 : !_build.IsHolding && _build.CurrentItem?.Id == tool.Id);
                tool.Image.color = selected ? UIFactory.ButtonOn : UIFactory.ButtonBg;
                var def = BuildCatalog.Get(tool.Id);
                tool.Button.interactable = def == null || _shop == null || _shop.Balance >= def.Cost;
            }
        }
    }
}
