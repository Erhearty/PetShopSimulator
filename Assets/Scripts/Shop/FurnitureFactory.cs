using System.Collections.Generic;
using UnityEngine;
using PetShop.Core;
using PetShop.Commerce;
using PetShop.Pets;
using PetShop.Player;

namespace PetShop.Shop
{
    /// <summary>Every furniture type the player can place, with its footprint and price.</summary>
    public static class BuildCatalog
    {
        public const string ShelfSmall = "shelf_small";
        public const string ShelfLarge = "shelf_large";
        public const string PetPen     = "pet_pen";
        public const string Counter    = "counter";
        public const string Wall       = "wall";
        public const string WallWindow = "wall_window";
        public const string WallDoor   = "wall_door";
        public const string Fence      = "fence";

        // Decorations. Purely visual; prefabs live in Assets/Prefabs/Decor/<id>.prefab.
        public const string DecorPlantPot    = "decor_plant_pot";
        public const string DecorFlowers     = "decor_flowers";
        public const string DecorBench       = "decor_bench";
        public const string DecorRugRound    = "decor_rug_round";
        public const string DecorBookshelf   = "decor_bookshelf";
        public const string DecorBoxClosed   = "decor_box_closed";
        public const string DecorBoxOpen     = "decor_box_open";
        public const string DecorDustbin     = "decor_dustbin";
        public const string DecorPallet      = "decor_pallet";
        public const string DecorFeedBag     = "decor_feed_bag";
        public const string DecorFishTank    = "decor_fish_tank";
        public const string DecorNoticeBoard = "decor_notice_board";
        public const string DecorLeadRail    = "decor_lead_rail";

        /// <summary><see cref="PlacedObjectData.Type"/> shared by every decoration entry.</summary>
        public const string DecorationType = "decoration";

        public static readonly Dictionary<string, PlacedObjectData> Items = new()
        {
            [ShelfSmall] = new PlacedObjectData
            {
                Id = ShelfSmall, DisplayName = "Small Shelf", Type = "shelf",
                Description = "Holds up to two product lines.",
                Size = new Vector2Int(1, 1), Cost = 120f, Tint = new Color(0.60f, 0.40f, 0.20f)
            },
            [ShelfLarge] = new PlacedObjectData
            {
                Id = ShelfLarge, DisplayName = "Large Shelf", Type = "shelf",
                Description = "A wide shelf with room for more stock per line.",
                Size = new Vector2Int(2, 1), Cost = 210f, Tint = new Color(0.50f, 0.35f, 0.18f)
            },
            [PetPen] = new PlacedObjectData
            {
                Id = PetPen, DisplayName = "Pet Pen", Type = "pen",
                Description = "Houses up to four pets of one species.",
                Size = new Vector2Int(2, 2), Cost = 350f, Tint = new Color(0.18f, 0.55f, 0.35f)
            },
            [Counter] = new PlacedObjectData
            {
                Id = Counter, DisplayName = "Counter", Type = "counter",
                Description = "Where customers queue and pay. Needed to open the shop.",
                Size = new Vector2Int(2, 1), Cost = 260f, Tint = new Color(0.70f, 0.55f, 0.35f)
            },

            // Building pieces. All one cell and rotatable, so a run of them makes a partition,
            // a room, or a paddock wherever the player wants one.
            [Wall] = new PlacedObjectData
            {
                Id = Wall, DisplayName = "Wall", Type = "wall", Category = BuildCategory.Structure,
                Description = "One cell of solid wall.",
                Size = new Vector2Int(1, 1), Cost = 45f, Tint = new Color(0.86f, 0.84f, 0.79f)
            },
            [WallWindow] = new PlacedObjectData
            {
                Id = WallWindow, DisplayName = "Window Wall", Type = "wall_window", Category = BuildCategory.Structure,
                Description = "One cell of wall with a window.",
                Size = new Vector2Int(1, 1), Cost = 85f, Tint = new Color(0.66f, 0.78f, 0.82f)
            },
            [WallDoor] = new PlacedObjectData
            {
                Id = WallDoor, DisplayName = "Doorway", Type = "wall_door", Category = BuildCategory.Structure,
                Description = "One cell of wall with an open doorway.",
                Size = new Vector2Int(1, 1), Cost = 70f, Tint = new Color(0.55f, 0.40f, 0.28f)
            },
            [Fence] = new PlacedObjectData
            {
                Id = Fence, DisplayName = "Fence", Type = "fence", Category = BuildCategory.Structure,
                Description = "One cell of low fence, for paddocks and the yard.",
                Size = new Vector2Int(1, 1), Cost = 20f, Tint = new Color(0.80f, 0.63f, 0.38f)
            },

            [DecorPlantPot]    = Decor(DecorPlantPot,    "Potted Plant", "A leafy plant in a pot.",                1, 1,  35f),
            [DecorFlowers]     = Decor(DecorFlowers,     "Flowers",      "A clump of bright flowers.",             1, 1,  25f),
            [DecorBench]       = Decor(DecorBench,       "Bench",        "A wooden bench for weary customers.",    2, 1,  90f),
            [DecorRugRound]    = Decor(DecorRugRound,    "Round Rug",    "A soft round rug.",                      2, 2,  60f),
            [DecorBookshelf]   = Decor(DecorBookshelf,   "Bookcase",     "A bookcase full of pet-care books.",     1, 1, 110f),
            [DecorBoxClosed]   = Decor(DecorBoxClosed,   "Sealed Box",   "A taped-up cardboard box.",              1, 1,  20f),
            [DecorBoxOpen]     = Decor(DecorBoxOpen,     "Open Box",     "An opened cardboard box.",               1, 1,  20f),
            [DecorDustbin]     = Decor(DecorDustbin,     "Dustbin",      "Keeps the floor tidy.",                  1, 1,  30f),
            [DecorPallet]      = Decor(DecorPallet,      "Pallet",       "A wooden shipping pallet.",              1, 1,  25f),
            [DecorFeedBag]     = Decor(DecorFeedBag,     "Feed Sack",    "A sack of pet feed, for show.",          1, 1,  30f),
            [DecorFishTank]    = Decor(DecorFishTank,    "Fish Tank",    "An aquarium on a stand.",                2, 1, 120f),
            [DecorNoticeBoard] = Decor(DecorNoticeBoard, "Notice Board", "A board of community notices.",          1, 1,  45f),
            [DecorLeadRail]    = Decor(DecorLeadRail,    "Lead Rail",    "A rail of dog leads on display.",        1, 1,  70f),
        };

        private static PlacedObjectData Decor(string id, string name, string description,
                                              int width, int depth, float cost) => new()
        {
            Id = id, DisplayName = name, Type = DecorationType, Category = BuildCategory.Decoration,
            Description = description, Size = new Vector2Int(width, depth), Cost = cost,
        };

        /// <summary>Catalogue entries that are building fabric rather than furniture.</summary>
        public static bool IsBuildingPiece(string id) =>
            id == Wall || id == WallWindow || id == WallDoor || id == Fence;

        /// <summary>True for purely visual decor entries.</summary>
        public static bool IsDecoration(string id) =>
            Get(id)?.Category == BuildCategory.Decoration;

        /// <summary>The catalogue entry for an id, or null when unknown.</summary>
        public static PlacedObjectData Get(string id) =>
            id != null && Items.TryGetValue(id, out var item) ? item : null;

        /// <summary>Catalogue ids in the order the 1-4 hotkeys select them.</summary>
        public static readonly string[] HotkeyOrder =
            { ShelfSmall, ShelfLarge, PetPen, Counter, Wall, WallWindow, WallDoor, Fence };
    }

    /// <summary>
    /// Turns a catalogue entry into a live GameObject with the right behaviour component.
    /// Used by the starter shop, build mode and save loading alike, so all three
    /// produce identical objects.
    /// </summary>
    public static class FurnitureFactory
    {
        /// <summary>Unity's built-in "Default" layer: decorations live here, never interactable.</summary>
        public const int DecorationLayer = 0;

        /// <summary>
        /// The baked prefab for each catalogue id. Set at boot from the scene's
        /// <see cref="ShopLayout.FurniturePrefabs"/>; tests without a scene assign it directly.
        /// </summary>
        public static FurniturePrefabs Prefabs { get; set; }

        /// <summary>
        /// Instantiate the baked prefab for a catalogue entry at a grid cell.
        /// <paramref name="variant"/> is a ProductCategory name for shelves and a
        /// Pet.Species name for pens; null picks a sensible default. Decorations get no
        /// behaviour component and go on the Default layer. <paramref name="footprint"/> is the
        /// occupied size the object centres on; null uses <c>def.Size</c>.
        /// </summary>
        public static GameObject Spawn(PlacedObjectData def, Vector2Int cell, string variant,
                                       GridManager grid, Transform parent, float yRotation = 0f,
                                       Vector2Int? footprint = null)
        {
            if (def == null || grid == null) return null;

            GameObject prefab = Prefabs != null ? Prefabs.Get(def.Id) : null;
            if (prefab == null)
            {
                Debug.LogError($"[FurnitureFactory] No baked prefab for '{def.Id}'. " +
                               "Assign a FurniturePrefabs catalogue to ShopLayout.FurniturePrefabs.");
                return null;
            }

            GameObject go = Object.Instantiate(prefab, parent, false);
            bool decor = def.Category == BuildCategory.Decoration;
            if (!decor) AddBehaviour(go, def, variant);

            go.name = $"Placed_{def.Id}_{cell.x}_{cell.y}";
            go.transform.position      = grid.FootprintCenter(cell, footprint ?? def.Size);
            go.transform.eulerAngles   = new Vector3(0f, yRotation, 0f);
            MeshBuilder.SetLayerRecursive(go, decor ? DecorationLayer : GameLayers.Furniture);
            return go;
        }

        private static void AddBehaviour(GameObject go, PlacedObjectData def, string variant)
        {
            switch (def.Type)
            {
                case "shelf": ConfigureShelf(go, def, variant); break;
                case "pen":   ConfigurePen(go, def, variant);   break;
                case "counter":
                    if (go.GetComponent<CounterInteractable>() == null) go.AddComponent<CounterInteractable>();
                    break;
            }
        }

        private static void ConfigureShelf(GameObject go, PlacedObjectData def, string variant)
        {
            float cs = GridManager.CellSize;
            var unit = go.GetComponent<ShelfUnit>();
            if (unit == null) unit = go.AddComponent<ShelfUnit>();
            unit.ShelfWidth  = def.Size.x * cs * 0.88f;
            unit.ShelfHeight = 1.6f;
            unit.ShelfDepth  = Mathf.Min(0.6f, cs * 0.3f);
            unit.TwoShelves  = true;
            unit.MaxLines    = 3;
            unit.MaxPerLine  = def.Size.x >= 2 ? 6 : 4;
            unit.Category    = ParseEnum(variant, ProductCategory.Food);
        }

        private static void ConfigurePen(GameObject go, PlacedObjectData def, string variant)
        {
            var pen = go.GetComponent<PetPen>();
            if (pen == null) pen = go.AddComponent<PetPen>();
            pen.PenSize    = def.Size.x * GridManager.CellSize * 0.86f;
            pen.Capacity   = 4;
            pen.PenSpecies = ParseEnum(variant, Pet.Species.Rabbit);
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct =>
            !string.IsNullOrEmpty(value) && System.Enum.TryParse(value, out T parsed) ? parsed : fallback;
    }
}
