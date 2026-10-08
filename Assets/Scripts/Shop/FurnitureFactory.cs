using System.Collections.Generic;
using UnityEngine;
using PetShop.Core;
using PetShop.Commerce;
using PetShop.Pets;
using PetShop.Player;
using PetShop.Progression;

namespace PetShop.Shop
{
    /// <summary>Every furniture type the player can place, with its footprint and price.</summary>
    public static class BuildCatalog
    {
        public const string ShelfSmall = "shelf_small";
        public const string ShelfLarge = "shelf_large";
        public const string ShelfToys        = "shelf_toys";
        public const string ShelfAccessories = "shelf_accessories";
        public const string ShelfMedicine    = "shelf_medicine";
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

        /// <summary><see cref="PlacedObjectData.Type"/> shared by the legacy and per-species pens.</summary>
        public const string PenType = "pen";

        /// <summary>Id prefix of the per-species pens: <c>pet_pen_&lt;species lowercase&gt;</c>.</summary>
        public const string SpeciesPenPrefix = PetPen + "_";

        /// <summary>Price of the bare pen, before the breeding pair it ships with.</summary>
        public const float PenBaseCost = 350f;

        /// <summary>Adults delivered with every new per-species pen.</summary>
        public const int PenStarterPairSize = 2;

        /// <summary>Stalls in every pen: the most pets one pen holds.</summary>
        public const int PenCapacity = 4;

        /// <summary>Side of a normal pen's footprint, in cells.</summary>
        public const int PenFootprintCells = 2;

        /// <summary>Side of the deer pen's footprint, in cells: room for a pen of twice the area.</summary>
        public const int DeerPenFootprintCells = 3;

        /// <summary>Footprint of the pen for <paramref name="species"/>: the deer pen is the big one.</summary>
        public static Vector2Int PenFootprintFor(Pet.Species species)
        {
            int side = species == Pet.Species.Deer ? DeerPenFootprintCells : PenFootprintCells;
            return new Vector2Int(side, side);
        }

        /// <summary>Linear scale of a pen's fence and floor: 1, or sqrt(2) for the deer pen so its area doubles.</summary>
        public static float PenSideScale(Pet.Species species) =>
            species == Pet.Species.Deer ? Mathf.Sqrt(2f) : 1f;

        /// <summary>Species of the legacy <c>pet_pen</c> when its variant names none.</summary>
        public const Pet.Species DefaultLegacyPenSpecies = Pet.Species.Rabbit;

        /// <summary>
        /// The species a pen holds: a per-species pen takes it from <paramref name="catalogId"/>; the legacy
        /// <c>pet_pen</c> reads it from <paramref name="variant"/>, defaulting to <see cref="DefaultLegacyPenSpecies"/>.
        /// </summary>
        public static Pet.Species PenSpecies(string catalogId, string variant) =>
            ProgressionRules.PenSpeciesFor(catalogId) ??
            (!string.IsNullOrEmpty(variant) && System.Enum.TryParse(variant, out Pet.Species parsed)
                ? parsed : DefaultLegacyPenSpecies);

        /// <summary>Bedding colour of each species' pen, so every pen reads differently at a glance.</summary>
        private static readonly Dictionary<Pet.Species, Color> PenBeddingTints = new()
        {
            [Pet.Species.Dog]     = new Color(0.62f, 0.45f, 0.28f),
            [Pet.Species.Cat]     = new Color(0.72f, 0.42f, 0.55f),
            [Pet.Species.Chicken] = new Color(0.90f, 0.80f, 0.42f),
            [Pet.Species.Penguin] = new Color(0.78f, 0.88f, 0.95f),
            [Pet.Species.Deer]    = new Color(0.40f, 0.58f, 0.30f),
            [Pet.Species.Horse]   = new Color(0.55f, 0.36f, 0.22f),
            [Pet.Species.Tiger]   = new Color(0.30f, 0.42f, 0.20f),
        };

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
            [ShelfToys] = new PlacedObjectData
            {
                Id = ShelfToys, DisplayName = "Toy Shelf", Type = "shelf",
                Description = "A shelf for pet toys.",
                Size = new Vector2Int(1, 1), Cost = 140f, Tint = new Color(0.85f, 0.45f, 0.30f)
            },
            [ShelfAccessories] = new PlacedObjectData
            {
                Id = ShelfAccessories, DisplayName = "Accessory Shelf", Type = "shelf",
                Description = "A shelf for collars, leads and other accessories.",
                Size = new Vector2Int(1, 1), Cost = 140f, Tint = new Color(0.35f, 0.50f, 0.80f)
            },
            [ShelfMedicine] = new PlacedObjectData
            {
                Id = ShelfMedicine, DisplayName = "Medicine Shelf", Type = "shelf",
                Description = "A shelf for pet medicine.",
                Size = new Vector2Int(1, 1), Cost = 140f, Tint = new Color(0.40f, 0.72f, 0.55f)
            },
            // Legacy species-picked pen: still loadable from old saves, no longer sold.
            [PetPen] = new PlacedObjectData
            {
                Id = PetPen, DisplayName = "Pet Pen", Type = PenType, Hidden = true,
                Description = "Four individual stalls, one per pet, all of one species.",
                Size = new Vector2Int(2, 2), Cost = PenBaseCost, Tint = new Color(0.18f, 0.55f, 0.35f)
            },
            [Counter] = new PlacedObjectData
            {
                Id = Counter, DisplayName = "Counter", Type = "counter",
                Description = "Where customers queue and pay. Needed to open the shop.",
                Size = new Vector2Int(2, 1), Cost = 260f, Tint = new Color(0.70f, 0.55f, 0.35f)
            },

            // Building pieces. All one cell and rotatable, so a run of them makes a partition,
            // a room, or an enclosure wherever the player wants one.
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
                Description = "One cell of low fence, for enclosures and the yard.",
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

        /// <summary>Adds one per-species pen entry for every species a pen can ever hold.</summary>
        static BuildCatalog()
        {
            foreach (var species in ProgressionRules.PickableSpecies(ProgressionRules.MaxTier))
                Items[PenIdFor(species)] = SpeciesPen(species);
        }

        /// <summary>Catalogue id of the pen for <paramref name="species"/>.</summary>
        public static string PenIdFor(Pet.Species species) =>
            SpeciesPenPrefix + species.ToString().ToLowerInvariant();

        /// <summary>The pen entry for <paramref name="species"/>: base pen plus its breeding pair.</summary>
        private static PlacedObjectData SpeciesPen(Pet.Species species) => new()
        {
            Id = PenIdFor(species), DisplayName = $"{species} Pen", Type = PenType, PenSpecies = species,
            Category = BuildCategory.Furniture,
            Description = $"Four stalls for {species}s. Ships with a breeding pair of adult {species}s.",
            Size = PenFootprintFor(species),
            Cost = PenBaseCost + PenStarterPairSize * Pet.WholesalePrice(species),
            Tint = PenBeddingTints.TryGetValue(species, out var tint) ? tint : Color.white,
        };

        private static PlacedObjectData Decor(string id, string name, string description,
                                              int width, int depth, float cost) => new()
        {
            Id = id, DisplayName = name, Type = DecorationType, Category = BuildCategory.Decoration,
            Description = description, Size = new Vector2Int(width, depth), Cost = cost,
        };

        /// <summary>The product category a dedicated shelf id stocks (toys, accessories, medicine), else null.</summary>
        public static ProductCategory? ShelfCategoryFor(string id) =>
            id == ShelfToys        ? ProductCategory.Toy :
            id == ShelfAccessories ? ProductCategory.Accessory :
            id == ShelfMedicine    ? ProductCategory.Medicine : (ProductCategory?)null;

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
    /// Used by build mode and save loading alike, so both produce identical objects.
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

        /// <summary>
        /// Instantiate the baked prefab for a catalogue entry at an exact world position and yaw, off the
        /// grid. Same object as <see cref="Spawn"/> makes, just not snapped to a cell.
        /// </summary>
        public static GameObject SpawnFree(PlacedObjectData def, Vector3 position, string variant,
                                           Transform parent, float yRotation)
        {
            if (def == null) return null;

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

            go.name = $"Placed_{def.Id}_{position.x:0.##}_{position.z:0.##}";
            go.transform.position    = new Vector3(position.x, 0f, position.z);
            go.transform.eulerAngles = new Vector3(0f, yRotation, 0f);
            MeshBuilder.SetLayerRecursive(go, decor ? DecorationLayer : GameLayers.Furniture);
            return go;
        }

        private static void AddBehaviour(GameObject go, PlacedObjectData def, string variant)
        {
            switch (def.Type)
            {
                case "shelf": ConfigureShelf(go, def, variant); break;
                case BuildCatalog.PenType: ConfigurePen(go, def, variant); break;
                case "counter":
                    if (go.GetComponent<CounterInteractable>() == null) go.AddComponent<CounterInteractable>();
                    AddGuideBook(go);
                    break;
            }
        }

        /// <summary>Name of the counter prefab's books child.</summary>
        private const string BooksName = "books";

        /// <summary>
        /// Gives the counter's books child a collider fitted to its meshes and a
        /// <see cref="GuideBookInteractable"/>, so interacting with the books opens the guide.
        /// </summary>
        private static void AddGuideBook(GameObject counter)
        {
            Transform books = null;
            foreach (var t in counter.GetComponentsInChildren<Transform>(true))
                if (t.name == BooksName) { books = t; break; }
            if (books == null || books.GetComponent<GuideBookInteractable>() != null) return;

            bool any = false;
            Bounds fitted = default;
            foreach (var mf in books.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 toBooks = books.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = toBooks.MultiplyPoint3x4(corner);
                    if (!any) { fitted = new Bounds(p, Vector3.zero); any = true; }
                    else fitted.Encapsulate(p);
                }
            }

            var box = books.gameObject.AddComponent<BoxCollider>();
            if (any) { box.center = fitted.center; box.size = fitted.size; }
            else     { box.center = Vector3.up * 0.1f; box.size = new Vector3(0.4f, 0.2f, 0.3f); }
            books.gameObject.AddComponent<GuideBookInteractable>();
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
            unit.Category    = ParseEnum(variant, BuildCatalog.ShelfCategoryFor(def.Id) ?? ProductCategory.Food);
            if (BuildCatalog.ShelfCategoryFor(def.Id).HasValue) TintShelf(go, def);
        }

        /// <summary>Recolours a category shelf's renderers with the entry's tint so it reads apart from the plain shelf.</summary>
        private static void TintShelf(GameObject go, PlacedObjectData def)
        {
            var mat = MaterialFactory.Get($"shelf_{def.Id}", def.Tint, 0f, 0.2f);
            if (mat == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
        }

        /// <summary>
        /// A per-species pen takes its species from its id; the legacy <c>pet_pen</c> reads it
        /// from <paramref name="variant"/>, defaulting to Rabbit.
        /// </summary>
        private static void ConfigurePen(GameObject go, PlacedObjectData def, string variant)
        {
            var pen = go.GetComponent<PetPen>();
            if (pen == null) pen = go.AddComponent<PetPen>();
            pen.PenSpecies = BuildCatalog.PenSpecies(def.Id, variant);
            pen.Capacity   = BuildCatalog.PenCapacity;
            // The pen's side is its normal footprint's, times sqrt(2) for deer (twice the area); the deer
            // footprint (3 cells) is wider than that, so the bigger pen never overlaps its neighbours.
            float sideScale = BuildCatalog.PenSideScale(pen.PenSpecies);
            pen.PenSize = BuildCatalog.PenFootprintCells * GridManager.CellSize * 0.86f * sideScale;
            if (sideScale > 1f) ScalePrefabParts(go.transform, sideScale);
            if (def.Id != BuildCatalog.PetPen) ApplyPenBedding(go, def);
        }

        /// <summary>Grows the baked pen parts (floor, fences, posts) about the pen's centre by <paramref name="scale"/>.</summary>
        private static void ScalePrefabParts(Transform root, float scale)
        {
            foreach (Transform child in root)
            {
                child.localPosition *= scale;
                child.localScale    *= scale;
            }
        }

        /// <summary>Name of the pen prefab's floor child, recoloured as each species' bedding.</summary>
        private const string PenFloorName = "PenFloor";
        /// <summary>Bedding is matte.</summary>
        private const float BeddingSmoothness = 0.1f;

        /// <summary>Recolours the pen floor with the entry's tint so each species' pen looks distinct.</summary>
        private static void ApplyPenBedding(GameObject go, PlacedObjectData def)
        {
            var bedding = MaterialFactory.Get($"pen_bedding_{def.Id}", def.Tint, 0f, BeddingSmoothness);
            if (bedding == null) return;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != PenFloorName) continue;
                var renderer = t.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = bedding;
            }
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct =>
            !string.IsNullOrEmpty(value) && System.Enum.TryParse(value, out T parsed) ? parsed : fallback;
    }
}
