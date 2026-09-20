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

        public static readonly Dictionary<string, PlacedObjectData> Items = new()
        {
            [ShelfSmall] = new PlacedObjectData
            {
                Id = ShelfSmall, DisplayName = "Small Shelf", Type = "shelf",
                Size = new Vector2Int(1, 1), Cost = 120f, Tint = new Color(0.60f, 0.40f, 0.20f)
            },
            [ShelfLarge] = new PlacedObjectData
            {
                Id = ShelfLarge, DisplayName = "Large Shelf", Type = "shelf",
                Size = new Vector2Int(2, 1), Cost = 210f, Tint = new Color(0.50f, 0.35f, 0.18f)
            },
            [PetPen] = new PlacedObjectData
            {
                Id = PetPen, DisplayName = "Pet Pen", Type = "pen",
                Size = new Vector2Int(2, 2), Cost = 350f, Tint = new Color(0.18f, 0.55f, 0.35f)
            },
            [Counter] = new PlacedObjectData
            {
                Id = Counter, DisplayName = "Counter", Type = "counter",
                Size = new Vector2Int(2, 1), Cost = 260f, Tint = new Color(0.70f, 0.55f, 0.35f)
            },

            // Building pieces. All one cell and rotatable, so a run of them makes a partition,
            // a room, or a paddock wherever the player wants one.
            [Wall] = new PlacedObjectData
            {
                Id = Wall, DisplayName = "Wall", Type = "wall",
                Size = new Vector2Int(1, 1), Cost = 45f, Tint = new Color(0.86f, 0.84f, 0.79f)
            },
            [WallWindow] = new PlacedObjectData
            {
                Id = WallWindow, DisplayName = "Window Wall", Type = "wall_window",
                Size = new Vector2Int(1, 1), Cost = 85f, Tint = new Color(0.66f, 0.78f, 0.82f)
            },
            [WallDoor] = new PlacedObjectData
            {
                Id = WallDoor, DisplayName = "Doorway", Type = "wall_door",
                Size = new Vector2Int(1, 1), Cost = 70f, Tint = new Color(0.55f, 0.40f, 0.28f)
            },
            [Fence] = new PlacedObjectData
            {
                Id = Fence, DisplayName = "Fence", Type = "fence",
                Size = new Vector2Int(1, 1), Cost = 20f, Tint = new Color(0.80f, 0.63f, 0.38f)
            },
        };

        /// <summary>Catalogue entries that are building fabric rather than furniture.</summary>
        public static bool IsBuildingPiece(string id) =>
            id == Wall || id == WallWindow || id == WallDoor || id == Fence;

        public static PlacedObjectData Get(string id) =>
            Items.TryGetValue(id, out var item) ? item : null;

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
        /// <summary>
        /// Build furniture at a grid cell.
        /// <paramref name="variant"/> is a ProductCategory name for shelves and a
        /// Pet.Species name for pens; null picks a sensible default.
        /// </summary>
        public static GameObject Spawn(PlacedObjectData def, Vector2Int cell, string variant,
                                       GridManager grid, Transform parent, float yRotation = 0f)
        {
            if (def == null || grid == null) return null;

            float cs = GridManager.CellSize;
            GameObject go;

            switch (def.Type)
            {
                case "shelf":
                {
                    float w = def.Size.x * cs * 0.88f;
                    float h = 1.6f;
                    float d = Mathf.Min(0.6f, cs * 0.3f);
                    bool twoShelves = true;

                    go = MeshBuilder.CreateShelf(w, h, d, twoShelves);

                    var unit = go.AddComponent<ShelfUnit>();
                    unit.ShelfWidth  = w;
                    unit.ShelfHeight = h;
                    unit.ShelfDepth  = d;
                    unit.TwoShelves  = twoShelves;
                    unit.MaxLines    = 3;
                    unit.MaxPerLine  = def.Size.x >= 2 ? 6 : 4;
                    unit.Category    = ParseEnum(variant, ProductCategory.Food);
                    break;
                }
                case "pen":
                {
                    float size = def.Size.x * cs * 0.86f;
                    go = MeshBuilder.CreatePetPen(size);

                    var pen = go.AddComponent<PetPen>();
                    pen.PenSize    = size;
                    pen.Capacity   = 4;
                    pen.PenSpecies = ParseEnum(variant, Pet.Species.Rabbit);

                    ModelLibrary.Spawn(ModelLibrary.Food + "bowl", go.transform,
                        new Vector3(size * 0.32f, 0.04f, -size * 0.30f), 0f, ModelLibrary.Fit.Width, 0.22f);
                    ModelLibrary.Spawn(ModelLibrary.Food + "bowl", go.transform,
                        new Vector3(size * 0.32f, 0.04f, -size * 0.10f), 0f, ModelLibrary.Fit.Width, 0.22f);

                    string tuft = ModelLibrary.FirstAvailable("Packs/Nature/Grass_01",
                                                              "Packs/Nature/Bush_02",
                                                              ModelLibrary.Nature + "grass_large");
                    for (int i = 0; i < 3; i++)
                        ModelLibrary.Spawn(tuft, go.transform,
                            new Vector3(-size * Random.Range(0.1f, 0.34f), 0.03f, size * Random.Range(0.05f, 0.32f)),
                            Random.Range(0f, 360f), ModelLibrary.Fit.Height, Random.Range(0.22f, 0.4f));
                    break;
                }
                case "counter":
                {
                    float width = def.Size.x * cs * 0.82f;
                    float depth = Mathf.Min(1f, cs * 0.5f);
                    go = BuildCounter(width, 1.1f, depth);
                    go.AddComponent<CounterInteractable>();
                    break;
                }
                case "wall":
                case "wall_window":
                case "wall_door":
                case "fence":
                {
                    go = BuildWallPiece(def.Type, cs);
                    break;
                }
                default:
                {
                    go = MeshBuilder.CreateBox(def.Size.x * cs * 0.85f, 1f, def.Size.y * cs * 0.85f,
                                               MaterialFactory.Get("placed_" + def.Id, def.Tint), def.Id);
                    break;
                }
            }

            go.name = $"Placed_{def.Id}_{cell.x}_{cell.y}";
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position      = grid.FootprintCenter(cell, def.Size);
            go.transform.eulerAngles   = new Vector3(0f, yRotation, 0f);
            MeshBuilder.SetLayerRecursive(go, GameLayers.Furniture);
            return go;
        }

        /// <summary>
        /// A shop counter built from the Kenney modular bar pieces when they are available,
        /// falling back to the procedural one otherwise. Either way the root carries a single
        /// box collider so interaction and movement behave identically.
        /// </summary>
        private static GameObject BuildCounter(float width, float height, float depth)
        {
            // The stylised wooden cupboard run reads as a shop counter and tiles cleanly.
            string shopCounter = ModelLibrary.FirstAvailable("Packs/Furniture/Cupboards/Cupboards");
            if (shopCounter != null) return BuildTiledCounter(shopCounter, null, width, height, depth);

            const string bar = ModelLibrary.Furniture + "kitchenBar";
            const string end = ModelLibrary.Furniture + "kitchenBarEnd";

            if (!ModelLibrary.Has(bar))
                return MeshBuilder.CreateCounter(width, height, depth);

            return BuildTiledCounter(bar, ModelLibrary.Has(end) ? end : null, width, height, depth);
        }

        /// <summary>
        /// Lays a modular counter piece end to end to fill <paramref name="width"/>, with an
        /// optional distinct end cap. A single box collider on the root keeps interaction and
        /// movement identical to the procedural counter.
        /// </summary>
        private static GameObject BuildTiledCounter(string bar, string end,
                                                    float width, float height, float depth)
        {

            var root    = new GameObject("Counter");
            float barW  = ModelLibrary.SizeAfterFit(bar, ModelLibrary.Fit.Height, height).x;
            float endW  = end != null
                ? ModelLibrary.SizeAfterFit(end, ModelLibrary.Fit.Height, height).x
                : 0f;
            if (barW < 0.05f) barW = 1f;

            int segments = Mathf.Max(1, Mathf.RoundToInt((width - endW * 2f) / Mathf.Max(0.01f, barW)));
            float span   = segments * barW + endW * 2f;
            float x      = -span * 0.5f;

            if (endW > 0f)
            {
                ModelLibrary.Spawn(end, root.transform, new Vector3(x + endW * 0.5f, 0f, 0f),
                                    0f, ModelLibrary.Fit.Height, height);
                x += endW;
            }
            for (int i = 0; i < segments; i++)
            {
                ModelLibrary.Spawn(bar, root.transform, new Vector3(x + barW * 0.5f, 0f, 0f),
                                    0f, ModelLibrary.Fit.Height, height);
                x += barW;
            }
            if (endW > 0f)
                ModelLibrary.Spawn(end, root.transform, new Vector3(x + endW * 0.5f, 0f, 0f),
                                    180f, ModelLibrary.Fit.Height, height);

            float actualDepth = ModelLibrary.SizeAfterFit(bar, ModelLibrary.Fit.Height, height).z;

            // Till and screen on the counter top
            ModelLibrary.Spawn(ModelLibrary.Furniture + "computerScreen", root.transform,
                new Vector3(span * 0.28f, height, -actualDepth * 0.15f), 8f, ModelLibrary.Fit.Height, 0.42f);
            ModelLibrary.Spawn(ModelLibrary.Furniture + "computerKeyboard", root.transform,
                new Vector3(span * 0.28f, height, actualDepth * 0.18f), 8f, ModelLibrary.Fit.Width, 0.34f);
            ModelLibrary.Spawn(ModelLibrary.Furniture + "books", root.transform,
                new Vector3(-span * 0.30f, height, 0f), -14f, ModelLibrary.Fit.Width, 0.3f);

            var collider = root.AddComponent<BoxCollider>();
            collider.size   = new Vector3(span, height, Mathf.Max(actualDepth, depth));
            collider.center = new Vector3(0f, height * 0.5f, 0f);
            return root;
        }

        /// <summary>
        /// One cell of building fabric, spanning the full cell width so a row of them joins
        /// seamlessly. Rotate with R while placing to run the wall along the other axis.
        /// </summary>
        private static GameObject BuildWallPiece(string type, float cellSize)
        {
            const float height = 3.2f;
            const float thick  = 0.24f;

            var root  = new GameObject(type);
            var solid = MaterialFactory.Get("built_wall", new Color(0.86f, 0.84f, 0.79f), 0f, 0.15f);
            var trim  = MaterialFactory.Get("built_trim", new Color(0.46f, 0.38f, 0.32f), 0f, 0.2f);

            switch (type)
            {
                case "fence":
                {
                    const float fenceH = 1.05f;
                    var rail = MeshBuilder.CreateBox(cellSize, 0.1f, 0.07f,
                        MaterialFactory.Get("built_fence", new Color(0.80f, 0.63f, 0.38f), 0f, 0.2f), "Rail");
                    rail.transform.SetParent(root.transform, false);
                    rail.transform.localPosition = new Vector3(0f, fenceH - 0.12f, 0f);

                    var rail2 = MeshBuilder.CreateBox(cellSize, 0.1f, 0.07f,
                        MaterialFactory.Get("built_fence", new Color(0.80f, 0.63f, 0.38f), 0f, 0.2f), "Rail2");
                    rail2.transform.SetParent(root.transform, false);
                    rail2.transform.localPosition = new Vector3(0f, fenceH * 0.5f, 0f);

                    foreach (float sx in new[] { -0.5f, 0f, 0.5f })
                    {
                        var post = MeshBuilder.CreateBox(0.12f, fenceH, 0.12f,
                            MaterialFactory.Get("built_fence", new Color(0.72f, 0.56f, 0.33f), 0f, 0.2f), "Post");
                        post.transform.SetParent(root.transform, false);
                        post.transform.localPosition = new Vector3(sx * cellSize * 0.94f, 0f, 0f);
                    }

                    var fenceCollider = root.AddComponent<BoxCollider>();
                    fenceCollider.size   = new Vector3(cellSize, fenceH, thick);
                    fenceCollider.center = new Vector3(0f, fenceH * 0.5f, 0f);
                    return root;
                }

                case "wall_door":
                {
                    // Jambs and a head, leaving a gap you can walk through.
                    float jamb = (cellSize - 1.2f) * 0.5f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var post = MeshBuilder.CreateBox(jamb, height, thick, solid, "Jamb");
                        post.transform.SetParent(root.transform, false);
                        post.transform.localPosition = new Vector3(side * (cellSize - jamb) * 0.5f, 0f, 0f);
                    }
                    var head = MeshBuilder.CreateBox(cellSize, height - 2.1f, thick, solid, "Head");
                    head.transform.SetParent(root.transform, false);
                    head.transform.localPosition = new Vector3(0f, 2.1f, 0f);

                    var frame = MeshBuilder.CreateBox(1.3f, 0.12f, thick + 0.06f, trim, "Lintel");
                    frame.transform.SetParent(root.transform, false);
                    frame.transform.localPosition = new Vector3(0f, 2.04f, 0f);
                    break;
                }

                case "wall_window":
                {
                    var sill = MeshBuilder.CreateBox(cellSize, 0.95f, thick, solid, "Sill");
                    sill.transform.SetParent(root.transform, false);

                    var head = MeshBuilder.CreateBox(cellSize, height - 2.4f, thick, solid, "Head");
                    head.transform.SetParent(root.transform, false);
                    head.transform.localPosition = new Vector3(0f, 2.4f, 0f);

                    var glass = MeshBuilder.CreateBox(cellSize - 0.2f, 1.45f, 0.06f, MaterialFactory.Glass, "Glass");
                    glass.transform.SetParent(root.transform, false);
                    glass.transform.localPosition = new Vector3(0f, 0.95f, 0f);

                    var mullion = MeshBuilder.CreateBox(0.08f, 1.45f, thick + 0.04f, trim, "Mullion");
                    mullion.transform.SetParent(root.transform, false);
                    mullion.transform.localPosition = new Vector3(0f, 0.95f, 0f);
                    break;
                }

                default:   // plain wall
                {
                    var panel = MeshBuilder.CreateBox(cellSize, height, thick, solid, "Panel");
                    panel.transform.SetParent(root.transform, false);

                    var skirt = MeshBuilder.CreateBox(cellSize, 0.14f, thick + 0.05f, trim, "Skirting");
                    skirt.transform.SetParent(root.transform, false);
                    break;
                }
            }

            var collider = root.AddComponent<BoxCollider>();
            collider.size   = new Vector3(cellSize, height, thick);
            collider.center = new Vector3(0f, height * 0.5f, 0f);
            return root;
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct =>
            !string.IsNullOrEmpty(value) && System.Enum.TryParse(value, out T parsed) ? parsed : fallback;
    }
}
