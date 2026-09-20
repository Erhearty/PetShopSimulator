using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using PetShop.Core;
using PetShop.Commerce;
using PetShop.Pets;
using PetShop.Player;

namespace PetShop.Shop
{
    /// <summary>
    /// Builds the physical shop at runtime: room shell, lighting, the player, and the
    /// NavMesh customers walk on. Furniture itself is owned by <see cref="Core.GameManager"/>
    /// so that the starter layout and a loaded save go through exactly the same path.
    /// </summary>
    public class ShopGenerator : MonoBehaviour
    {
        [Header("Yard — the open lot the shop stands in (metres)")]
        public float YardWidth = 64f;
        public float YardDepth = 34f;

        [Header("Shop building (metres) — sits in the yard's near-left corner")]
        public float RoomWidth  = 16f;
        public float RoomDepth  = 12f;
        public float WallHeight = 4f;
        public float DoorWidth  = 4f;

        [Tooltip("Gap between the shop and the yard's left wall.")]
        public float ShopSideMargin = 2f;

        [Tooltip("Gap between the shop front and the street, i.e. the depth of the forecourt.")]
        public float ShopFrontMargin = 7f;

        /// <summary>Centre of the shop building in world space. The yard is centred on the origin.</summary>
        public Vector3 ShopCentre => new(
            -YardWidth * 0.5f + ShopSideMargin  + RoomWidth * 0.5f,
            0f,
             YardDepth * 0.5f - ShopFrontMargin - RoomDepth * 0.5f);

        /// <summary>World Z of the yard's street-facing edge.</summary>
        public float YardFrontZ => YardDepth * 0.5f;

        public StreetGenerator Street  { get; private set; }
        public Transform ShopRoot      { get; private set; }
        public Transform FurnitureRoot { get; private set; }
        public Transform Player        { get; private set; }

        /// <summary>Just inside the shop's front door.</summary>
        public Vector3 DoorPosition => ShopCentre + new Vector3(0f, 0f, RoomDepth * 0.5f - 1.6f);

        /// <summary>On the forecourt, a couple of metres outside the shop door.</summary>
        public Vector3 ForecourtPosition => ShopCentre + new Vector3(0f, 0f, RoomDepth * 0.5f + 2.5f);

        private GridManager   _grid;
        private NavMeshSurface _surface;

        public void Generate(GridManager grid)
        {
            _grid    = grid;
            ShopRoot = new GameObject("Shop").transform;

            FurnitureRoot = new GameObject("Furniture").transform;
            FurnitureRoot.SetParent(ShopRoot, false);

            BuildYard();
            BuildShopBuilding();
            BuildShopFront();
            BuildFacade();
            FillFloorGrid();

            Street = gameObject.GetComponent<StreetGenerator>() ?? gameObject.AddComponent<StreetGenerator>();
            // The whole street cross-section is measured from the yard's front edge —
            // forgetting to shift it leaves the road sitting on top of the yard.
            Street.ShopFrontZ    = YardFrontZ;
            Street.PavementBackZ = YardFrontZ - 1f;
            Street.KerbZ         = YardFrontZ + 7f;
            Street.FarPavementZ  = Street.KerbZ + Street.RoadWidth + 6f;
            Street.ShopHalfWidth = YardWidth * 0.5f;
            // The street has to reach roughly as far as the city blocks behind it, or the
            // parade trails off into open ground while the skyline carries on.
            Street.HalfLength    = Mathf.Max(120f, YardWidth * 0.5f + 60f);
            Street.WalkableHalf  = YardWidth * 0.5f;
            Street.Generate(ShopRoot);

            SetupLighting();
            DressInterior();
            DressYard();
            SpawnPlayer();
            Debug.Log($"[ShopGenerator] Yard {YardWidth}x{YardDepth} m with a {RoomWidth}x{RoomDepth} m shop at " +
                      $"({ShopCentre.x:F0}, {ShopCentre.z:F0}); street and props built.");
        }

        // ── The yard ────────────────────────────────────────────────────────────

        /// <summary>
        /// The open lot. One continuous slab under everything — including the shop's
        /// footprint — so the NavMesh never has a seam at the shop door.
        /// </summary>
        private void BuildYard()
        {
            float hw = YardWidth * 0.5f, hd = YardDepth * 0.5f;

            var ground = MeshBuilder.CreateBox(YardWidth, 0.2f, YardDepth,
                MaterialFactory.Get("yard", new Color(0.38f, 0.42f, 0.31f), 0f, 0.12f), "YardGround");
            Attach(ground, new Vector3(0f, -0.1f, 0f));

            // A paved forecourt strip in front of the shop, so the entrance reads
            var apron = MeshBuilder.CreateBox(RoomWidth + 8f, 0.06f, 7f,
                MaterialFactory.Get("apron_paving", new Color(0.56f, 0.55f, 0.52f), 0f, 0.18f), "Forecourt");
            Attach(apron, new Vector3(ShopCentre.x + 2f, 0.01f, ShopCentre.z + RoomDepth * 0.5f + 3f));
            Destroy(apron.GetComponent<Collider>());

            // Perimeter: solid wall on three sides, railing with a gate along the street
            var wallMat = MaterialFactory.Get("yard_wall", new Color(0.70f, 0.66f, 0.58f), 0f, 0.5f);
            SpawnBoundary("YardWallWest",  YardDepth, new Vector3(-hw, 1.1f, 0f), 90f, wallMat);
            SpawnBoundary("YardWallEast",  YardDepth, new Vector3( hw, 1.1f, 0f), 90f, wallMat);
            SpawnBoundary("YardWallBack",  YardWidth, new Vector3(0f, 1.1f, -hd), 0f, wallMat);

            // Street frontage: railing either side of an entrance aligned with the shop door
            float gateCentre = ShopCentre.x;
            const float gateWidth = 9f;
            float leftEnd  = gateCentre - gateWidth * 0.5f;
            float rightEnd = gateCentre + gateWidth * 0.5f;

            BuildRailing(-hw, leftEnd, hd);
            BuildRailing(rightEnd, hw, hd);

            foreach (float x in new[] { leftEnd, rightEnd })
            {
                var pier = MeshBuilder.CreateBox(0.5f, 2.2f, 0.5f, wallMat, "GatePier");
                Attach(pier, new Vector3(x, 0f, hd));
            }
        }

        private void SpawnBoundary(string name, float length, Vector3 pos, float yRot, Material mat)
        {
            var go = MeshBuilder.CreateWall(length, 2.2f, 0.3f, mat, name);
            go.transform.position    = pos;
            go.transform.eulerAngles = new Vector3(0f, yRot, 0f);
            go.transform.SetParent(ShopRoot, true);
        }

        /// <summary>Low railing along the street frontage: a kerb rail plus uprights.</summary>
        private void BuildRailing(float fromX, float toX, float z)
        {
            float length = toX - fromX;
            if (length <= 0.2f) return;

            var railMat = MaterialFactory.Get("yard_rail", new Color(0.32f, 0.36f, 0.34f), 0.2f, 0.5f);

            var plinth = MeshBuilder.CreateBox(length, 0.45f, 0.32f,
                MaterialFactory.Get("yard_wall", new Color(0.70f, 0.66f, 0.58f), 0f, 0.5f), "Plinth");
            Attach(plinth, new Vector3((fromX + toX) * 0.5f, 0f, z));

            foreach (float y in new[] { 1.0f, 1.35f })
            {
                var rail = MeshBuilder.CreateBox(length, 0.07f, 0.07f, railMat, "Rail");
                Attach(rail, new Vector3((fromX + toX) * 0.5f, y, z));
                Destroy(rail.GetComponent<Collider>());
            }
            for (float x = fromX + 0.6f; x < toX; x += 1.6f)
            {
                var post = MeshBuilder.CreateBox(0.07f, 1.0f, 0.07f, railMat, "RailPost");
                Attach(post, new Vector3(x, 0.45f, z));
                Destroy(post.GetComponent<Collider>());
            }

            // Invisible barrier so nobody hops the railing
            var blocker = new GameObject("RailBlocker");
            blocker.transform.SetParent(ShopRoot, false);
            blocker.transform.position = new Vector3((fromX + toX) * 0.5f, 1.2f, z);
            blocker.AddComponent<BoxCollider>().size = new Vector3(length, 2.4f, 0.3f);
            blocker.layer = GameLayers.Ghost;
        }

        // ── The shop building ───────────────────────────────────────────────────

        private void BuildShopBuilding()
        {
            Vector3 c = ShopCentre;
            float hw = RoomWidth * 0.5f, hd = RoomDepth * 0.5f;

            var floor = MeshBuilder.CreateFloorTile(RoomWidth, RoomDepth, MaterialFactory.Floor, "ShopFloor");
            Attach(floor, c + new Vector3(0f, 0.02f, 0f));

            var ceil = MeshBuilder.CreateWall(RoomWidth, 0.1f, RoomDepth, MaterialFactory.Wall, "Ceiling");
            Attach(ceil, c + new Vector3(0f, WallHeight, 0f));

            SpawnWall("WallNorth", RoomWidth, c + new Vector3(0f, WallHeight * 0.5f, -hd));
            SpawnWall("WallEast",  RoomDepth, c + new Vector3(hw, WallHeight * 0.5f, 0f), 90f);
            SpawnWall("WallWest",  RoomDepth, c + new Vector3(-hw, WallHeight * 0.5f, 0f), 90f);

            SpawnTrim("TrimN", RoomWidth, c + new Vector3(0f, 0.09f, -hd + 0.12f));
            SpawnTrim("TrimE", RoomDepth, c + new Vector3(hw - 0.12f, 0.09f, 0f), 90f);
            SpawnTrim("TrimW", RoomDepth, c + new Vector3(-hw + 0.12f, 0.09f, 0f), 90f);

            var mat = MeshBuilder.CreateBox(DoorWidth, 0.02f, 1.6f,
                MaterialFactory.Get("mat", new Color(0.35f, 0.28f, 0.24f)), "WelcomeMat");
            Attach(mat, c + new Vector3(0f, 0.04f, hd - 1f));
            Destroy(mat.GetComponent<Collider>());
        }

        /// <summary>
        /// Plant on the roof: a border trim, an air-conditioning unit and a vent pipe. From
        /// the street the roofline is visible above the facade, and a bare slab reads as an
        /// unfinished block.
        /// </summary>
        private void BuildRoofDetail(Vector3 c, float roofTop, float hw)
        {
            float hd = RoomDepth * 0.5f;

            // Border trim round the edge of the roof
            var trim = MaterialFactory.Get("roof_trim", new Color(0.46f, 0.47f, 0.49f), 0f, 0.2f);
            foreach (var (size, offset) in new[]
            {
                (new Vector3(RoomWidth + 0.3f, 0.34f, 0.28f), new Vector3(0f, 0f,  hd)),
                (new Vector3(RoomWidth + 0.3f, 0.34f, 0.28f), new Vector3(0f, 0f, -hd)),
                (new Vector3(0.28f, 0.34f, RoomDepth + 0.3f), new Vector3( hw, 0f, 0f)),
                (new Vector3(0.28f, 0.34f, RoomDepth + 0.3f), new Vector3(-hw, 0f, 0f)),
            })
            {
                var edge = MeshBuilder.CreateBox(size.x, size.y, size.z, trim, "RoofTrim");
                Attach(edge, new Vector3(c.x, roofTop + size.y * 0.5f, c.z) + offset);
                Destroy(edge.GetComponent<Collider>());
            }

            // Air-conditioning unit on a short plinth
            Vector3 acAt = new(c.x + hw * 0.45f, roofTop, c.z - hd * 0.35f);

            var plinth = MeshBuilder.CreateBox(2.0f, 0.18f, 1.5f,
                MaterialFactory.Get("roof_plinth", new Color(0.38f, 0.38f, 0.40f), 0f, 0.15f), "AcPlinth");
            Attach(plinth, acAt + new Vector3(0f, 0.09f, 0f));
            Destroy(plinth.GetComponent<Collider>());

            var acBody = MeshBuilder.CreateBox(1.7f, 1.0f, 1.2f,
                MaterialFactory.Get("roof_ac", new Color(0.72f, 0.73f, 0.75f), 0.3f, 0.35f), "AcUnit");
            Attach(acBody, acAt + new Vector3(0f, 0.18f + 0.5f, 0f));

            var grille = MeshBuilder.CreateBox(1.5f, 0.75f, 0.06f,
                MaterialFactory.Get("roof_grille", new Color(0.30f, 0.31f, 0.33f), 0.2f, 0.3f), "AcGrille");
            Attach(grille, acAt + new Vector3(0f, 0.68f, 0.62f));
            Destroy(grille.GetComponent<Collider>());

            var fanHousing = MeshBuilder.CreateCylinder(0.38f, 0.16f,
                MaterialFactory.Get("roof_fan", new Color(0.26f, 0.27f, 0.29f), 0.3f, 0.4f), "AcFan");
            fanHousing.transform.position = acAt + new Vector3(0f, 1.18f, 0f);
            fanHousing.transform.SetParent(ShopRoot, true);
            Destroy(fanHousing.GetComponent<Collider>());

            // Vent pipe with a cowl
            Vector3 ventAt = new(c.x - hw * 0.5f, roofTop, c.z + hd * 0.2f);
            var pipeMat = MaterialFactory.Get("roof_pipe", new Color(0.55f, 0.56f, 0.58f), 0.35f, 0.4f);

            var pipe = MeshBuilder.CreateCylinder(0.16f, 1.5f, pipeMat, "VentPipe");
            pipe.transform.position = ventAt;
            pipe.transform.SetParent(ShopRoot, true);
            Destroy(pipe.GetComponent<Collider>());

            var cowl = MeshBuilder.CreateCylinder(0.26f, 0.22f, pipeMat, "VentCowl");
            cowl.transform.position = ventAt + new Vector3(0f, 1.5f, 0f);
            cowl.transform.SetParent(ShopRoot, true);
            Destroy(cowl.GetComponent<Collider>());

            // A second, smaller flue
            var flue = MeshBuilder.CreateCylinder(0.1f, 0.9f, pipeMat, "Flue");
            flue.transform.position = ventAt + new Vector3(0.9f, 0f, 0.6f);
            flue.transform.SetParent(ShopRoot, true);
            Destroy(flue.GetComponent<Collider>());
        }

        // ── Placement helpers ───────────────────────────────────────────────────

        private void SpawnWall(string name, float width, Vector3 pos, float yRot = 0f)
        {
            var go = MeshBuilder.CreateWall(width, WallHeight, 0.2f, MaterialFactory.Wall, name);
            go.transform.position    = pos;
            go.transform.eulerAngles = new Vector3(0f, yRot, 0f);
            go.transform.SetParent(ShopRoot, true);
        }

        private void SpawnTrim(string name, float length, Vector3 pos, float yRot = 0f)
        {
            var go = MeshBuilder.CreateBox(length, 0.18f, 0.06f, MaterialFactory.WallTrim, name);
            go.transform.position    = pos;
            go.transform.eulerAngles = new Vector3(0f, yRot, 0f);
            go.transform.SetParent(ShopRoot, true);
            Destroy(go.GetComponent<Collider>());
        }

        private void Attach(GameObject go, Vector3 pos)
        {
            go.transform.position = pos;
            go.transform.SetParent(ShopRoot, true);
        }

        // ── Shopfront ───────────────────────────────────────────────────────────

        /// <summary>
        /// Replaces the flat south wall with a glazed shopfront: a stallriser, full-height
        /// glass between mullions, a transom, and an open doorway in the middle. This is what
        /// makes the street visible from inside.
        /// </summary>
        private void BuildShopFront()
        {
            Vector3 c = ShopCentre;
            float hd = c.z + RoomDepth * 0.5f;
            const float riserH  = 0.55f;   // solid base under the glass
            const float glassT  = 0.10f;
            const float frameT  = 0.26f;
            float transomY      = WallHeight - 0.85f;

            var frameMat = MaterialFactory.Get("front_frame", new Color(0.30f, 0.34f, 0.38f), 0.25f, 0.55f);
            var riserMat = MaterialFactory.Get("front_riser", new Color(0.36f, 0.46f, 0.42f), 0f, 0.45f);
            var glassMat = MaterialFactory.Glass;

            float halfDoor = DoorWidth * 0.5f;

            foreach (float side in new[] { -1f, 1f })
            {
                float inner = c.x + side * halfDoor;
                float outer = c.x + side * RoomWidth * 0.5f;
                float width = Mathf.Abs(outer - inner);
                float centre = (inner + outer) * 0.5f;

                // Stallriser
                var riser = MeshBuilder.CreateBox(width, riserH, frameT, riserMat, "Stallriser");
                Attach(riser, new Vector3(centre, 0f, hd));

                // Glazing
                var glass = MeshBuilder.CreateBox(width - 0.04f, transomY - riserH, glassT, glassMat, "Glass");
                Attach(glass, new Vector3(centre, (riserH + transomY) * 0.5f, hd));

                // Transom above the glass
                var transom = MeshBuilder.CreateBox(width, WallHeight - transomY, frameT, frameMat, "Transom");
                Attach(transom, new Vector3(centre, (transomY + WallHeight) * 0.5f, hd));

                // Mullions every ~2 m, plus the posts that close each end
                int bays = Mathf.Max(1, Mathf.RoundToInt(width / 2f));
                for (int i = 0; i <= bays; i++)
                {
                    float x = Mathf.Lerp(inner, outer, i / (float)bays);
                    var mullion = MeshBuilder.CreateBox(0.14f, transomY - riserH, frameT + 0.02f, frameMat, "Mullion");
                    Attach(mullion, new Vector3(x, (riserH + transomY) * 0.5f, hd));
                }
            }

            // Door reveal: posts either side and a head over the opening
            foreach (float side in new[] { -1f, 1f })
            {
                var post = MeshBuilder.CreateBox(0.20f, transomY, frameT + 0.06f, frameMat, "DoorPost");
                Attach(post, new Vector3(c.x + side * halfDoor, transomY * 0.5f, hd));
            }
            var head = MeshBuilder.CreateBox(DoorWidth + 0.4f, WallHeight - transomY, frameT + 0.06f, frameMat, "DoorHead");
            Attach(head, new Vector3(c.x, (transomY + WallHeight) * 0.5f, hd));

            BuildAwningAndSign(hd);
        }

        private void BuildAwningAndSign(float hd)
        {
            var stripeA = MaterialFactory.Get("awning_a", new Color(0.82f, 0.34f, 0.30f), 0f, 0.35f);
            var stripeB = MaterialFactory.Get("awning_b", new Color(0.95f, 0.93f, 0.88f), 0f, 0.35f);

            // Striped awning over the doorway
            float awningW = DoorWidth + 2.4f;
            int stripes = 10;
            for (int i = 0; i < stripes; i++)
            {
                float w = awningW / stripes;
                float x = -awningW * 0.5f + w * (i + 0.5f);
                var stripe = MeshBuilder.CreateBox(w, 0.08f, 1.5f, i % 2 == 0 ? stripeA : stripeB, "AwningStripe");
                Attach(stripe, new Vector3(ShopCentre.x + x, WallHeight - 0.95f, hd + 0.78f));
                stripe.transform.localEulerAngles = new Vector3(-14f, 0f, 0f);
                Destroy(stripe.GetComponent<Collider>());
            }
            foreach (float side in new[] { -1f, 1f })
            {
                var stay = MeshBuilder.CreateBox(0.06f, 0.06f, 1.5f,
                    MaterialFactory.Get("awning_stay", new Color(0.25f, 0.25f, 0.27f), 0.3f, 0.5f), "AwningStay");
                Attach(stay, new Vector3(ShopCentre.x + side * awningW * 0.5f, WallHeight - 1.02f, hd + 0.78f));
                Destroy(stay.GetComponent<Collider>());
            }

            // Sign board and lettering above the awning
            var board = MeshBuilder.CreateBox(RoomWidth * 0.55f, 1.25f, 0.22f,
                MaterialFactory.Get("sign_board", new Color(0.16f, 0.26f, 0.30f), 0f, 0.4f), "SignBoard");
            Attach(board, new Vector3(ShopCentre.x, WallHeight + 0.75f, hd + 0.12f));

            var trim = MeshBuilder.CreateBox(RoomWidth * 0.55f + 0.18f, 0.12f, 0.3f,
                MaterialFactory.Get("sign_trim", new Color(0.88f, 0.72f, 0.32f), 0.3f, 0.6f), "SignTrim");
            Attach(trim, new Vector3(ShopCentre.x, WallHeight + 0.12f, hd + 0.12f));

            CreateSignText("PAWS & WHISKERS", new Vector3(ShopCentre.x, WallHeight + 1.45f, hd + 0.26f), 1.0f);
            CreateSignText("pet shop",        new Vector3(ShopCentre.x, WallHeight + 0.78f, hd + 0.26f), 0.5f,
                           new Color(0.75f, 0.84f, 0.88f));
        }

        private void CreateSignText(string text, Vector3 position, float size, Color? color = null)
        {
            var go = new GameObject($"Sign_{text}");
            go.transform.SetParent(ShopRoot, true);
            go.transform.position = position;

            // Text renders facing its own +Z; the sign is read from +Z, so turn it round.
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.text           = text;
            tmp.fontSize       = size * 8f;
            tmp.color          = color ?? new Color(0.98f, 0.86f, 0.45f);
            tmp.alignment      = TMPro.TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(RoomWidth * 0.55f, size * 1.4f);
        }

        /// <summary>
        /// The storeys above the shop, so from the street the shop reads as the ground floor of
        /// a building rather than a lone box. Exterior only — nothing up here is enterable.
        /// </summary>
        private void BuildFacade()
        {
            Vector3 c = ShopCentre;
            float hd = c.z + RoomDepth * 0.5f;
            float hw = RoomWidth * 0.5f;
            const float storeyH = 3.2f;
            const int   storeys = 2;

            var brick  = MaterialFactory.Get("facade", new Color(0.62f, 0.45f, 0.40f), 0f, 0.55f);
            var band   = MaterialFactory.Get("facade_band", new Color(0.82f, 0.79f, 0.74f), 0f, 0.5f);
            var window = MaterialFactory.Get("facade_window", new Color(0.20f, 0.30f, 0.36f), 0.35f, 0.75f);

            float top = WallHeight;
            for (int s = 0; s < storeys; s++)
            {
                float y0 = top + s * storeyH;

                var wall = MeshBuilder.CreateBox(RoomWidth, storeyH, 0.35f, brick, $"FacadeStorey{s}");
                Attach(wall, new Vector3(c.x, y0 + storeyH * 0.5f, hd));

                // Window openings
                for (int i = 0; i < 4; i++)
                {
                    float x = Mathf.Lerp(c.x - hw + 2.2f, c.x + hw - 2.2f, i / 3f);
                    var pane = MeshBuilder.CreateBox(1.5f, 1.8f, 0.12f, window, "FacadeWindow");
                    Attach(pane, new Vector3(x, y0 + storeyH * 0.5f, hd + 0.2f));
                    Destroy(pane.GetComponent<Collider>());

                    var sill = MeshBuilder.CreateBox(1.75f, 0.14f, 0.3f, band, "Sill");
                    Attach(sill, new Vector3(x, y0 + storeyH * 0.5f - 0.97f, hd + 0.22f));
                    Destroy(sill.GetComponent<Collider>());
                }

                var course = MeshBuilder.CreateBox(RoomWidth + 0.2f, 0.22f, 0.5f, band, "StringCourse");
                Attach(course, new Vector3(c.x, y0, hd + 0.08f));
                Destroy(course.GetComponent<Collider>());
            }

            // Parapet and side returns so the building is not a flat card
            var parapet = MeshBuilder.CreateBox(RoomWidth + 0.4f, 0.7f, 0.7f, band, "Parapet");
            Attach(parapet, new Vector3(c.x, top + storeys * storeyH, hd));

            foreach (float side in new[] { -1f, 1f })
            {
                // CreateBox is centred once its position is overridden, so the upper storeys
                // have to be placed at their mid-height or they swallow the shopfront.
                var flank = MeshBuilder.CreateBox(0.35f, storeys * storeyH, RoomDepth, brick, "FacadeFlank");
                Attach(flank, new Vector3(c.x + side * hw, top + storeys * storeyH * 0.5f, c.z));

                // The side elevations were blank slabs. Windows, a string course and a
                // downpipe give them the same language as the front.
                for (int st = 0; st < storeys; st++)
                for (int i = 0; i < 3; i++)
                {
                    float z = Mathf.Lerp(c.z - RoomDepth * 0.35f, c.z + RoomDepth * 0.35f, i / 2f);
                    var pane = MeshBuilder.CreateBox(0.12f, 1.5f, 1.2f, window, "SideWindow");
                    Attach(pane, new Vector3(c.x + side * (hw + 0.18f), top + storeyH * (st + 0.5f), z));
                    Destroy(pane.GetComponent<Collider>());
                }

                var course = MeshBuilder.CreateBox(0.5f, 0.2f, RoomDepth, band, "SideCourse");
                Attach(course, new Vector3(c.x + side * hw, top, c.z));
                Destroy(course.GetComponent<Collider>());

                var pipe = MeshBuilder.CreateCylinder(0.08f, top + storeys * storeyH,
                    MaterialFactory.Get("downpipe", new Color(0.36f, 0.37f, 0.38f), 0.2f, 0.3f), "Downpipe");
                pipe.transform.position = new Vector3(c.x + side * (hw + 0.2f), 0f, c.z + RoomDepth * 0.45f);
                pipe.transform.SetParent(ShopRoot, true);
                Destroy(pipe.GetComponent<Collider>());
            }

            // Ground-floor side walls in brick too, so the shop is not grey below the facade
            foreach (float side in new[] { -1f, 1f })
            {
                var plinth = MeshBuilder.CreateBox(0.14f, WallHeight, RoomDepth, brick, "SideCladding");
                Attach(plinth, new Vector3(c.x + side * (hw + 0.1f), WallHeight * 0.5f, c.z));
                Destroy(plinth.GetComponent<Collider>());
            }

            var rearClad = MeshBuilder.CreateBox(RoomWidth, WallHeight, 0.14f, brick, "RearCladding");
            Attach(rearClad, new Vector3(c.x, WallHeight * 0.5f, c.z - RoomDepth * 0.5f - 0.1f));
            Destroy(rearClad.GetComponent<Collider>());

            float roofY = top + storeys * storeyH;
            var roof = MeshBuilder.CreateBox(RoomWidth, 0.3f, RoomDepth,
                MaterialFactory.Get("roof", new Color(0.34f, 0.35f, 0.37f), 0f, 0.15f), "Roof");
            Attach(roof, new Vector3(c.x, roofY + 0.15f, c.z));

            BuildRoofDetail(c, roofY + 0.3f, hw);

            // Back wall of the upper storeys, so the building is not open from behind
            var back = MeshBuilder.CreateBox(RoomWidth, storeys * storeyH, 0.35f, brick, "FacadeBack");
            Attach(back, new Vector3(c.x, top + storeys * storeyH * 0.5f, c.z - RoomDepth * 0.5f));
        }

        // ── Grid ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Every cell inside the yard is buildable — that is the open red area in the brief,
        /// including the shop's own footprint, so shelves go indoors and pens outdoors
        /// through exactly the same placement path.
        /// </summary>
        private void FillFloorGrid()
        {
            float cs = GridManager.CellSize;
            int halfX = Mathf.FloorToInt(YardWidth * 0.5f / cs);
            int halfZ = Mathf.FloorToInt(YardDepth * 0.5f / cs);
            _grid.FillFloorRect(new Vector2Int(-halfX, -halfZ), new Vector2Int(halfX * 2, halfZ * 2));

            // Keep the shop doorway and the path through it clear of furniture
            Vector2Int door = _grid.WorldToGrid(DoorPosition);
            for (int dz = -1; dz <= 2; dz++)
                for (int dx = -1; dx <= 0; dx++)
                    _grid.SetFloor(new Vector2Int(door.x + dx, door.y + dz), false);
        }

        // ── Lighting ────────────────────────────────────────────────────────────

        private void SetupLighting()
        {
            var sky = MaterialFactory.CreateSkybox(
                new Color(0.62f, 0.74f, 0.92f),
                new Color(0.44f, 0.42f, 0.38f));

            if (sky != null)
            {
                RenderSettings.skybox      = sky;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 0.72f;
            }
            else
            {
                RenderSettings.ambientMode         = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor     = new Color(0.44f, 0.47f, 0.54f);
                RenderSettings.ambientEquatorColor = new Color(0.34f, 0.33f, 0.32f);
                RenderSettings.ambientGroundColor  = new Color(0.20f, 0.19f, 0.18f);
            }

            // Fog is the distance fade: it has to finish before anything is clipped, or the
            // far row of buildings ends on a hard edge instead of dissolving.
            RenderSettings.fog        = true;
            RenderSettings.fogMode    = FogMode.Linear;
            // Matched to the skybox near the horizon: a fog colour that differs from the sky
            // turns the far edge of the ground plane into a visible band.
            RenderSettings.fogColor   = new Color(0.70f, 0.78f, 0.82f);
            RenderSettings.fogStartDistance = 95f;
            RenderSettings.fogEndDistance   = 400f;

            var sun = new GameObject("SunLight").AddComponent<Light>();
            sun.type            = LightType.Directional;
            sun.color           = new Color(1f, 0.96f, 0.88f);
            sun.intensity       = 0.95f;
            sun.shadows         = LightShadows.Soft;
            sun.shadowStrength  = 0.62f;
            sun.transform.eulerAngles = new Vector3(46f, 158f, 0f);   // sun comes in through the shopfront
            sun.transform.SetParent(ShopRoot, true);
            RenderSettings.sun = sun;

            if (sky != null) DynamicGI.UpdateEnvironment();

            // Interior fill so the shop is not a cave once you step away from the window
            Vector3 shop = ShopCentre;
            foreach (float ox in new[] { -RoomWidth * 0.28f, RoomWidth * 0.28f })
            foreach (float oz in new[] { -RoomDepth * 0.25f, RoomDepth * 0.25f })
            {
                float x = shop.x + ox, z = shop.z + oz;
                var lamp = new GameObject("CeilingLight").AddComponent<Light>();
                lamp.type      = LightType.Point;
                lamp.range     = 9f;
                lamp.intensity = 0.45f;
                lamp.color     = new Color(1f, 0.95f, 0.84f);
                lamp.shadows   = LightShadows.None;
                lamp.transform.position = new Vector3(x, WallHeight - 0.5f, z);
                lamp.transform.SetParent(ShopRoot, true);

                var fitting = ModelLibrary.Spawn(ModelLibrary.Furniture + "lampSquareCeiling", ShopRoot,
                    new Vector3(x, WallHeight, z), 0f, ModelLibrary.Fit.Height, 0.55f);
                if (fitting != null)
                {
                    // The model hangs from its top, so drop it under the ceiling.
                    fitting.transform.position = new Vector3(x, WallHeight - 0.55f, z);
                    ModelLibrary.SetLayer(fitting, GameLayers.Scenery);
                }
                else
                {
                    var shade = MeshBuilder.CreateBox(0.7f, 0.08f, 0.7f,
                        MaterialFactory.Get("lamp", new Color(0.95f, 0.93f, 0.85f)), "Shade");
                    shade.transform.position = new Vector3(x, WallHeight - 0.2f, z);
                    shade.transform.SetParent(ShopRoot, true);
                    Destroy(shade.GetComponent<Collider>());
                }
            }
        }

        // ── Interior dressing ───────────────────────────────────────────────────

        /// <summary>
        /// Decorative props that make the shop look lived in. None of this is interactive or
        /// grid-registered, and every model is optional — the shop still works without the
        /// Kenney kits present.
        /// </summary>
        private void DressInterior()
        {
            var dressing = new GameObject("Dressing").transform;
            dressing.SetParent(ShopRoot, false);
            dressing.position = ShopCentre;   // interior props are laid out shop-relative

            float hw = RoomWidth * 0.5f, hd = RoomDepth * 0.5f;

            // Wall shelving down both side walls — the shop's stock room look
            foreach (float z in new[] { -3.5f, -1f, 1.5f })
            foreach (float side in new[] { -1f, 1f })
                Dress(Pick("Packs/Furniture/Book Shelve/BookShelve", ModelLibrary.Furniture + "bookcaseClosed"),
                      dressing, new Vector3(side * (hw - 0.55f), 0f, z), side > 0 ? 90f : -90f,
                      ModelLibrary.Fit.Height, 2.0f);

            // Mats: one at the door, one in the middle of the floor
            Dress(Pick("Packs/Furniture/Floor Tiles/FloorTile_1", ModelLibrary.Furniture + "rugRectangle"),
                  dressing, new Vector3(0f, 0.01f, hd - 2.2f), 0f, ModelLibrary.Fit.Width, 4.2f);
            Dress(ModelLibrary.FirstAvailable(ModelLibrary.Furniture + "rugRound"),
                  dressing, new Vector3(0f, 0.01f, -0.5f), 0f, ModelLibrary.Fit.Width, 4.5f);

            // Stockroom clutter behind the counter — feed sacks and boxes, not café furniture
            Dress(ModelLibrary.FirstAvailable(ModelLibrary.Furniture + "cardboardBoxClosed"), dressing,
                  new Vector3(-hw + 1.5f, 0f, -hd + 2.4f), 20f, ModelLibrary.Fit.Height, 0.6f);
            Dress(ModelLibrary.FirstAvailable(ModelLibrary.Furniture + "cardboardBoxOpen"), dressing,
                  new Vector3(-hw + 2.3f, 0f, -hd + 3.1f), -12f, ModelLibrary.Fit.Height, 0.6f);
            Dress(Pick("Packs/City/Props/Props_Dustbin", ModelLibrary.Furniture + "trashcan"), dressing,
                  new Vector3(hw - 1.3f, 0f, -hd + 1.6f), 0f, ModelLibrary.Fit.Height, 0.9f);

            BuildPetThemedDecor(dressing, hw, hd);
        }

        /// <summary>
        /// Decoration that belongs in a pet shop: stacked feed sacks, a display of bowls, a
        /// notice board and a fish tank. Previously the interior was dressed with a café
        /// table, armchairs and outdoor shrubbery, none of which said "pet shop".
        /// </summary>
        private void BuildPetThemedDecor(Transform parent, float hw, float hd)
        {
            // Pallet of feed sacks by the side wall
            var pallet = MeshBuilder.CreateBox(1.5f, 0.12f, 1.1f,
                MaterialFactory.Get("pallet", new Color(0.58f, 0.44f, 0.28f), 0f, 0.2f), "Pallet");
            pallet.transform.SetParent(parent, false);
            pallet.transform.localPosition = new Vector3(-hw + 1.4f, 0f, 1.6f);

            string sack = ModelLibrary.FirstAvailable(ModelLibrary.Food + "bag", ModelLibrary.Food + "barrel");
            for (int i = 0; i < 5; i++)
            {
                float x = -hw + 1.0f + (i % 2) * 0.62f;
                float y = 0.12f + (i / 2) * 0.34f;
                Dress(sack, parent, new Vector3(x, y, 1.35f + (i % 2) * 0.45f),
                      Random(-12f, 12f), ModelLibrary.Fit.Height, 0.42f);
            }

            // Fish tank on a stand — the classic pet shop fixture
            var stand = MeshBuilder.CreateBox(1.5f, 0.75f, 0.6f,
                MaterialFactory.Get("tank_stand", new Color(0.36f, 0.27f, 0.20f), 0f, 0.2f), "TankStand");
            stand.transform.SetParent(parent, false);
            stand.transform.localPosition = new Vector3(hw - 1.1f, 0f, 2.4f);

            var tank = MeshBuilder.CreateBox(1.4f, 0.65f, 0.5f, MaterialFactory.Glass, "FishTank");
            tank.transform.SetParent(parent, false);
            tank.transform.localPosition = new Vector3(hw - 1.1f, 0.75f, 2.4f);

            var water = MeshBuilder.CreateBox(1.3f, 0.5f, 0.42f,
                MaterialFactory.Get("tank_water", new Color(0.25f, 0.52f, 0.62f, 0.55f), 0.1f, 0.9f), "TankWater");
            water.transform.SetParent(parent, false);
            water.transform.localPosition = new Vector3(hw - 1.1f, 0.82f, 2.4f);
            Destroy(water.GetComponent<Collider>());

            var gravel = MeshBuilder.CreateBox(1.3f, 0.08f, 0.42f,
                MaterialFactory.Get("tank_gravel", new Color(0.52f, 0.45f, 0.34f), 0f, 0.2f), "TankGravel");
            gravel.transform.SetParent(parent, false);
            gravel.transform.localPosition = new Vector3(hw - 1.1f, 0.79f, 2.4f);
            Destroy(gravel.GetComponent<Collider>());

            // Notice board by the door
            var board = MeshBuilder.CreateBox(1.2f, 0.85f, 0.06f,
                MaterialFactory.Get("notice_board", new Color(0.45f, 0.32f, 0.20f), 0f, 0.2f), "NoticeBoard");
            board.transform.SetParent(parent, false);
            board.transform.localPosition = new Vector3(-hw + 0.25f, 1.6f, hd - 2.6f);
            board.transform.localEulerAngles = new Vector3(0f, 90f, 0f);
            Destroy(board.GetComponent<Collider>());

            var noticeMat = MaterialFactory.Get("notice", new Color(0.93f, 0.91f, 0.85f), 0f, 0.1f);
            for (int i = 0; i < 5; i++)
            {
                var notice = MeshBuilder.CreateBox(0.22f, 0.28f, 0.02f, noticeMat, "Notice");
                notice.transform.SetParent(parent, false);
                notice.transform.localPosition = new Vector3(
                    -hw + 0.21f, 1.35f + (i / 3) * 0.42f, hd - 3.0f + (i % 3) * 0.36f);
                notice.transform.localEulerAngles = new Vector3(0f, 90f, Random(-5f, 5f));
                Destroy(notice.GetComponent<Collider>());
            }

            // Leads and collars hanging on a rail behind the counter
            var rail = MeshBuilder.CreateBox(2.2f, 0.05f, 0.05f,
                MaterialFactory.Get("lead_rail", new Color(0.40f, 0.42f, 0.44f), 0.3f, 0.4f), "LeadRail");
            rail.transform.SetParent(parent, false);
            rail.transform.localPosition = new Vector3(2.4f, 1.85f, -hd + 0.35f);
            Destroy(rail.GetComponent<Collider>());

            var leadColours = new[]
            {
                new Color(0.80f, 0.25f, 0.22f), new Color(0.24f, 0.45f, 0.78f),
                new Color(0.30f, 0.62f, 0.35f), new Color(0.85f, 0.65f, 0.20f),
            };
            for (int i = 0; i < 8; i++)
            {
                var lead = MeshBuilder.CreateBox(0.06f, 0.55f, 0.03f,
                    MaterialFactory.Get($"lead_{i % 4}", leadColours[i % 4], 0f, 0.25f), "Lead");
                lead.transform.SetParent(parent, false);
                lead.transform.localPosition = new Vector3(1.45f + i * 0.27f, 1.55f, -hd + 0.35f);
                Destroy(lead.GetComponent<Collider>());
            }
        }

        // ── The yard ────────────────────────────────────────────────────────────

        // Yard zones, in world coordinates. The paddock is where StarterLayout puts the pens;
        // sized to the six starter pens plus working margins, because a paddock much bigger
        // than its pens reads as an empty sand pit with a few hutches in one corner.
        private Rect PaddockArea => new(2f, -9f, 24f, 17f);      // x, z, width, depth
        private float PathZ      => ShopCentre.z - RoomDepth * 0.5f - 2.2f;

        private System.Random _yardRng;
        private float YRand(float a, float b) => (float)(_yardRng.NextDouble() * (b - a) + a);

        private void DressYard()
        {
            var yard = new GameObject("YardDressing").transform;
            yard.SetParent(ShopRoot, false);

            _yardRng = new System.Random(4242);

            BuildYardSurfaces(yard);
            BuildPaddockFence(yard);
            BuildPaddockShelter(yard);
            BuildYardPlanting(yard);
            BuildGarden(yard);
            BuildYardProps(yard);
        }

        /// <summary>
        /// Surfacing. An open lot of flat grass reads as an unfinished level, so the yard is
        /// broken into legible zones: paved circulation, a sanded paddock, and planting.
        /// </summary>
        private void BuildYardSurfaces(Transform parent)
        {
            var paving = MaterialFactory.Get("yard_paving", new Color(0.58f, 0.57f, 0.54f), 0f, 0.15f);
            var sand   = MaterialFactory.Get("paddock_sand", new Color(0.64f, 0.56f, 0.40f), 0f, 0.1f);

            var approach = MeshBuilder.CreateBox(5.5f, 0.06f, ShopFrontMargin + 1f, paving, "PathApproach");
            Slab(approach, parent, new Vector3(ShopCentre.x, 0.02f,
                                               YardDepth * 0.5f - (ShopFrontMargin + 1f) * 0.5f));

            float spineFrom = ShopCentre.x;
            float spineTo   = PaddockArea.x + PaddockArea.width * 0.5f;
            var spine = MeshBuilder.CreateBox(spineTo - spineFrom + 6f, 0.06f, 4.2f, paving, "PathSpine");
            Slab(spine, parent, new Vector3((spineFrom + spineTo) * 0.5f, 0.02f, PathZ));

            var paddock = MeshBuilder.CreateBox(PaddockArea.width, 0.05f, PaddockArea.height, sand, "PaddockGround");
            Slab(paddock, parent, new Vector3(PaddockArea.x + PaddockArea.width * 0.5f, 0.015f,
                                              PaddockArea.y + PaddockArea.height * 0.5f));

            var spur = MeshBuilder.CreateBox(3.2f, 0.06f, 5f, paving, "PathSpur");
            Slab(spur, parent, new Vector3(PaddockArea.x + PaddockArea.width * 0.5f, 0.025f, PathZ - 4f));
        }

        private void Slab(GameObject go, Transform parent, Vector3 position)
        {
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Destroy(go.GetComponent<Collider>());
        }

        /// <summary>A post-and-rail fence round the paddock, with a gap onto the path.</summary>
        private void BuildPaddockFence(Transform parent)
        {
            var wood = MaterialFactory.Get("paddock_rail", new Color(0.62f, 0.47f, 0.30f), 0f, 0.2f);
            float x0 = PaddockArea.x, x1 = PaddockArea.x + PaddockArea.width;
            float z0 = PaddockArea.y, z1 = PaddockArea.y + PaddockArea.height;
            float gateCentre = PaddockArea.x + PaddockArea.width * 0.5f;

            void Run(Vector3 from, Vector3 to, float gapAt = float.NaN, float gapWidth = 0f)
            {
                float length = Vector3.Distance(from, to);
                int posts = Mathf.Max(2, Mathf.RoundToInt(length / 2.2f));
                for (int i = 0; i <= posts; i++)
                {
                    Vector3 at = Vector3.Lerp(from, to, i / (float)posts);
                    if (!float.IsNaN(gapAt) && Mathf.Abs(at.x - gapAt) < gapWidth * 0.5f) continue;

                    var post = MeshBuilder.CreateBox(0.12f, 1.1f, 0.12f, wood, "PaddockPost");
                    post.transform.SetParent(parent, false);
                    post.transform.position = at;
                }
                foreach (float y in new[] { 0.55f, 0.95f })
                {
                    Vector3 mid = (from + to) * 0.5f; mid.y = y;
                    bool alongX = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.z - from.z);
                    var rail = MeshBuilder.CreateBox(alongX ? length : 0.08f, 0.08f,
                                                     alongX ? 0.08f : length, wood, "PaddockRail");
                    rail.transform.SetParent(parent, false);
                    rail.transform.position = mid;
                    Destroy(rail.GetComponent<Collider>());
                }
            }

            Run(new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0));
            Run(new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), gateCentre, 4.5f);
            Run(new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1));
            Run(new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1));
        }

        /// <summary>
        /// An open pergola over the pen rows: posts, beams and slatted rafters. Gives the
        /// animals shade and the paddock a bit of height, which a flat field of hutches
        /// badly needed.
        /// </summary>
        private void BuildPaddockShelter(Transform parent)
        {
            var post  = MaterialFactory.Get("pergola_post", new Color(0.55f, 0.40f, 0.26f), 0f, 0.2f);
            var beam  = MaterialFactory.Get("pergola_beam", new Color(0.62f, 0.47f, 0.31f), 0f, 0.2f);
            var slat  = MaterialFactory.Get("pergola_slat", new Color(0.68f, 0.54f, 0.36f), 0f, 0.2f);

            const float height = 3.1f;

            // Covers the pen rows, not the whole paddock — the gate end stays open.
            float x0 = PaddockArea.x + 1.5f;
            float x1 = PaddockArea.x + PaddockArea.width - 5.5f;
            float z0 = PaddockArea.y + 3.5f;
            float z1 = PaddockArea.y + PaddockArea.height - 1.5f;

            foreach (float x in new[] { x0, (x0 + x1) * 0.5f, x1 })
            foreach (float z in new[] { z0, z1 })
            {
                var leg = MeshBuilder.CreateBox(0.18f, height, 0.18f, post, "PergolaPost");
                leg.transform.SetParent(parent, false);
                leg.transform.position = new Vector3(x, 0f, z);
            }

            // Beams along X over each row of posts
            foreach (float z in new[] { z0, z1 })
            {
                var run = MeshBuilder.CreateBox(x1 - x0 + 0.6f, 0.18f, 0.16f, beam, "PergolaBeam");
                run.transform.SetParent(parent, false);
                run.transform.position = new Vector3((x0 + x1) * 0.5f, height, z);
                Destroy(run.GetComponent<Collider>());
            }

            // Slatted rafters across, spaced so light still reaches the pens
            for (float x = x0; x <= x1 + 0.01f; x += 0.85f)
            {
                var rafter = MeshBuilder.CreateBox(0.1f, 0.12f, z1 - z0 + 0.7f, slat, "PergolaRafter");
                rafter.transform.SetParent(parent, false);
                rafter.transform.position = new Vector3(x, height + 0.15f, (z0 + z1) * 0.5f);
                Destroy(rafter.GetComponent<Collider>());
            }

            // A short solid canopy at the far end, for real shade
            var canopy = MeshBuilder.CreateBox(x1 - x0 + 0.6f, 0.08f, 3.2f,
                MaterialFactory.Get("pergola_canopy", new Color(0.46f, 0.50f, 0.42f), 0f, 0.15f), "PergolaCanopy");
            canopy.transform.SetParent(parent, false);
            canopy.transform.position = new Vector3((x0 + x1) * 0.5f, height + 0.24f, z0 + 1.2f);
            Destroy(canopy.GetComponent<Collider>());
        }

        /// <summary>Hedging and beds along the walls, so the boundary is not a bare line.</summary>
        private void BuildYardPlanting(Transform parent)
        {
            float hw = YardWidth * 0.5f, hd = YardDepth * 0.5f;

            string hedge = ModelLibrary.FirstAvailable("Packs/Nature/Bush_03", "Packs/Nature/Bush_01",
                                                        ModelLibrary.Nature + "plant_bush");
            string tree  = ModelLibrary.FirstAvailable("Packs/Street/Foliage/Trees/Tree_A_V01",
                                                        "Packs/Nature/Tree_02",
                                                        ModelLibrary.Nature + "tree_default");
            string grass = ModelLibrary.FirstAvailable("Packs/Nature/Grass_01", ModelLibrary.Nature + "grass_large");

            var bedMat = MaterialFactory.Get("yard_bed", new Color(0.32f, 0.25f, 0.18f), 0f, 0.1f);

            var backBed = MeshBuilder.CreateBox(YardWidth - 4f, 0.08f, 3.4f, bedMat, "BackBed");
            Slab(backBed, parent, new Vector3(0f, 0.03f, -hd + 2.2f));

            for (float x = -hw + 4f; x < hw - 4f; x += YRand(2.2f, 3.6f))
                YardProp(hedge, parent, new Vector3(x, 0.06f, -hd + YRand(1.4f, 3f)),
                         YRand(0f, 360f), YRand(0.9f, 1.5f));

            for (float x = -hw + 7f; x < hw - 6f; x += YRand(9f, 14f))
                YardProp(tree, parent, new Vector3(x, 0f, -hd + 3.4f), YRand(0f, 360f), YRand(5f, 7f), block: true);

            var westBed = MeshBuilder.CreateBox(2.6f, 0.08f, YardDepth - 8f, bedMat, "WestBed");
            Slab(westBed, parent, new Vector3(-hw + 1.6f, 0.03f, -2f));
            for (float z = -hd + 6f; z < hd - 8f; z += YRand(2.4f, 4f))
                YardProp(hedge, parent, new Vector3(-hw + YRand(0.9f, 2.4f), 0.06f, z),
                         YRand(0f, 360f), YRand(0.8f, 1.3f));

            // Scatter tufts over the open grass only. Without the shop test, tufts sprout
            // through the shop floor and show up as weeds growing indoors.
            var shopFootprint = new Rect(ShopCentre.x - RoomWidth * 0.5f - 1f,
                                         ShopCentre.z - RoomDepth * 0.5f - 1f,
                                         RoomWidth + 2f, RoomDepth + 2f);

            for (int i = 0; i < 40; i++)
            {
                float x = YRand(-hw + 3f, hw - 3f), z = YRand(-hd + 6f, hd - 3f);
                var at = new Vector2(x, z);

                if (PaddockArea.Contains(at))    continue;
                if (shopFootprint.Contains(at))  continue;
                if (Mathf.Abs(z - PathZ) < 3f)   continue;
                if (z > YardDepth * 0.5f - ShopFrontMargin - 1f) continue;   // keep the forecourt clear

                YardProp(grass, parent, new Vector3(x, 0f, z), YRand(0f, 360f), YRand(0.3f, 0.6f));
            }
        }

        /// <summary>
        /// A small display garden in the gap between the shop and the paddock. That stretch
        /// was the largest patch of featureless grass in the lot, which a plan view makes
        /// obvious and an eye-level shot does not.
        /// </summary>
        private void BuildGarden(Transform parent)
        {
            float cx = (ShopCentre.x + RoomWidth * 0.5f + PaddockArea.x) * 0.5f;
            float cz = PathZ - 7f;

            var bedMat = MaterialFactory.Get("garden_bed", new Color(0.34f, 0.27f, 0.19f), 0f, 0.1f);
            var edging = MaterialFactory.Get("garden_edge", new Color(0.60f, 0.58f, 0.54f), 0f, 0.15f);

            var bed = MeshBuilder.CreateBox(9f, 0.1f, 7f, bedMat, "GardenBed");
            Slab(bed, parent, new Vector3(cx, 0.04f, cz));

            foreach (var (size, offset) in new[]
            {
                (new Vector3(9.4f, 0.18f, 0.3f), new Vector3(0f, 0f,  3.5f)),
                (new Vector3(9.4f, 0.18f, 0.3f), new Vector3(0f, 0f, -3.5f)),
                (new Vector3(0.3f, 0.18f, 7f),   new Vector3( 4.55f, 0f, 0f)),
                (new Vector3(0.3f, 0.18f, 7f),   new Vector3(-4.55f, 0f, 0f)),
            })
            {
                var kerb = MeshBuilder.CreateBox(size.x, size.y, size.z, edging, "GardenKerb");
                kerb.transform.SetParent(parent, false);
                kerb.transform.position = new Vector3(cx, 0f, cz) + offset;
                Destroy(kerb.GetComponent<Collider>());
            }

            string shrub  = ModelLibrary.FirstAvailable("Packs/Nature/Bush_02", "Packs/Nature/Bush_01");
            string flower = ModelLibrary.FirstAvailable("Packs/Nature/Flowers_01", "Packs/Nature/Flowers_02");
            string tree   = ModelLibrary.FirstAvailable("Packs/Nature/Tree_03", "Packs/Nature/Tree_01");

            YardProp(tree, parent, new Vector3(cx, 0f, cz), 0f, YRand(4.5f, 5.5f), block: true);
            for (int i = 0; i < 10; i++)
                YardProp(i % 3 == 0 ? shrub : flower, parent,
                         new Vector3(cx + YRand(-3.8f, 3.8f), 0.08f, cz + YRand(-2.8f, 2.8f)),
                         YRand(0f, 360f), YRand(0.4f, 0.9f));

            string bench = ModelLibrary.FirstAvailable("Packs/Street/StreetProps/Bench/Bench_A",
                                                        ModelLibrary.Furniture + "benchCushion");
            YardProp(bench, parent, new Vector3(cx - 5.6f, 0f, cz), 0f, 1.0f);
            YardProp(bench, parent, new Vector3(cx + 5.6f, 0f, cz), 180f, 1.0f);
        }

        /// <summary>The things that make a yard look worked in.</summary>
        private void BuildYardProps(Transform parent)
        {
            float hw = YardWidth * 0.5f;
            float paddockLeft = PaddockArea.x;

            var straw = MaterialFactory.Get("straw_bale", new Color(0.82f, 0.71f, 0.38f), 0f, 0.12f);
            foreach (var offset in new[] { new Vector3(0f, 0f, 0f), new Vector3(1.05f, 0f, 0.1f),
                                           new Vector3(0.5f, 0.72f, 0.05f) })
            {
                var bale = MeshBuilder.CreateBox(1f, 0.7f, 0.7f, straw, "StrawBale");
                bale.transform.SetParent(parent, false);
                bale.transform.position = new Vector3(paddockLeft + 1.6f, 0f, PaddockArea.y + 1.8f) + offset;
                bale.transform.localEulerAngles = new Vector3(0f, YRand(-8f, 8f), 0f);
            }

            var trough = MeshBuilder.CreateBox(2.2f, 0.45f, 0.8f,
                MaterialFactory.Get("trough", new Color(0.42f, 0.44f, 0.46f), 0.2f, 0.3f), "WaterTrough");
            trough.transform.SetParent(parent, false);
            trough.transform.position = new Vector3(paddockLeft + PaddockArea.width - 3f, 0f,
                                                     PaddockArea.y + 2f);

            var water = MeshBuilder.CreateBox(1.95f, 0.06f, 0.6f,
                MaterialFactory.Get("trough_water", new Color(0.35f, 0.55f, 0.65f), 0.1f, 0.85f), "Water");
            water.transform.SetParent(trough.transform, false);
            water.transform.localPosition = new Vector3(0f, 0.86f, 0f);
            Destroy(water.GetComponent<Collider>());

            string bench = ModelLibrary.FirstAvailable("Packs/Street/StreetProps/Bench/Bench_A",
                                                        ModelLibrary.Furniture + "benchCushion");
            foreach (float dx in new[] { -5.5f, 5.5f })
                YardProp(bench, parent, ShopCentre + new Vector3(dx, 0f, RoomDepth * 0.5f + 3.4f), 90f, 1.0f);

            YardProp(ModelLibrary.FirstAvailable("Packs/City/Props/Props_Dustbin",
                                                  ModelLibrary.Furniture + "trashcan"),
                     parent, ShopCentre + new Vector3(RoomWidth * 0.5f + 1.2f, 0f, RoomDepth * 0.5f + 1.2f),
                     0f, 1.0f);

            // Planters flanking the shop door. Fitted by WIDTH: PlantPot_A is a wide shallow
            // trough, so sizing it by height blows it up into a mound of earth taller than
            // the doorway.
            string pot = ModelLibrary.FirstAvailable("Packs/Street/StreetProps/PlantPots/Elements/PlantPot_A",
                                                      ModelLibrary.Furniture + "pottedPlant");
            foreach (float dx in new[] { -3.4f, 3.4f })
            {
                var planter = ModelLibrary.Spawn(pot, parent,
                    ShopCentre + new Vector3(dx, 0f, RoomDepth * 0.5f + 1.1f),
                    0f, ModelLibrary.Fit.Width, 1.3f);
                if (planter != null) ModelLibrary.SetLayer(planter, GameLayers.Scenery);

                YardProp(ModelLibrary.FirstAvailable("Packs/Nature/Bush_02", "Packs/Nature/Flowers_01"),
                         parent, ShopCentre + new Vector3(dx, 0.25f, RoomDepth * 0.5f + 1.1f),
                         YRand(0f, 360f), 0.7f);
            }

            string lamp = ModelLibrary.FirstAvailable("Packs/Street/StreetProps/ParkLamp/ParkLamp",
                                                       ModelLibrary.Roads + "light-square");
            foreach (var pos in new[] { new Vector3(ShopCentre.x + 9f, 0f, PathZ + 2.6f),
                                        new Vector3(PaddockArea.x - 2f, 0f, PathZ + 2.6f),
                                        new Vector3(-hw + 3f, 0f, -YardDepth * 0.5f + 8f),
                                        new Vector3( hw - 3f, 0f, -YardDepth * 0.5f + 8f) })
                YardProp(lamp, parent, pos, 0f, 5f);
        }

        private void YardProp(string model, Transform parent, Vector3 pos, float yRot, float size, bool block = false)
        {
            if (model == null) return;
            var go = ModelLibrary.Spawn(model, parent, pos, yRot, ModelLibrary.Fit.Height, size);
            if (go == null) return;
            ModelLibrary.SetLayer(go, GameLayers.Scenery);

            if (!block) return;
            var blocker = new GameObject("PropBlocker");
            blocker.transform.SetParent(parent, false);
            blocker.transform.position = pos + Vector3.up;
            blocker.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
            blocker.layer = GameLayers.Ghost;
        }

        /// <summary>First of these models that is installed        /// <summary>First of these models that is installed — pack model preferred, kit fallback.</summary>
        private static string Pick(string preferred, string fallback) =>
            ModelLibrary.FirstAvailable(preferred, fallback);

        private void Dress(string model, Transform parent, Vector3 position, float yRot,
                           ModelLibrary.Fit fit, float size)
        {
            if (model == null) return;
            var go = ModelLibrary.Spawn(model, parent, parent.position + position, yRot, fit, size);
            if (go == null) return;
            ModelLibrary.SetLayer(go, GameLayers.Scenery);
        }

        private float Random(float min, float max) => UnityEngine.Random.Range(min, max);

        // ── Player ──────────────────────────────────────────────────────────────

        private void SpawnPlayer()
        {
            var playerGo = new GameObject("Player") { layer = GameLayers.Character };
            playerGo.transform.position = ForecourtPosition + new Vector3(2f, 0f, 2f);
            playerGo.transform.rotation = Quaternion.Euler(0f, 200f, 0f);

            var cc = playerGo.AddComponent<CharacterController>();
            cc.height     = 1.8f;
            cc.radius     = 0.28f;
            cc.center     = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;
            cc.skinWidth  = 0.04f;

            // The shopkeeper is always the same face; customers are randomised.
            CharacterFactory.Attach(playerGo, () => cc.velocity, variant: 1);

            playerGo.AddComponent<PlayerController>();
            playerGo.AddComponent<InteractionSystem>();

            Player = playerGo.transform;
            SpawnCamera(Player);
        }

        private void SpawnCamera(Transform target)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            cam.nearClipPlane   = 0.1f;
            // The city blocks run past z = 300. A 200 m far plane sliced them off in a hard
            // straight line across the skyline; fog now takes over well before this.
            cam.farClipPlane    = 900f;
            cam.fieldOfView     = 68f;   // a touch wide, which suits first person indoors

            // First person is the default view. The orbit camera is left in the project and
            // can be swapped back in, but nothing creates it.
            var orbit = cam.GetComponent<ThirdPersonCamera>();
            if (orbit != null) Destroy(orbit);

            var fpc = cam.GetComponent<FirstPersonCamera>() ?? cam.gameObject.AddComponent<FirstPersonCamera>();
            fpc.Body = target;
        }

        // ── NavMesh ─────────────────────────────────────────────────────────────

        /// <summary>
        /// (Re)bakes the walkable surface. Call after furniture changes so customers
        /// path around new shelves.
        /// </summary>
        public void BakeNavMesh()
        {
            if (ShopRoot == null) return;

            if (_surface == null)
            {
                _surface = ShopRoot.gameObject.AddComponent<NavMeshSurface>();
                _surface.collectObjects   = CollectObjects.Children;
                _surface.useGeometry      = NavMeshCollectGeometry.PhysicsColliders;
                _surface.layerMask        = ~(1 << GameLayers.Character);
                _surface.overrideVoxelSize = true;
                _surface.voxelSize        = 0.16f;
            }
            _surface.BuildNavMesh();
            Debug.Log("[ShopGenerator] NavMesh baked.");
        }

        // ── Starter layout ──────────────────────────────────────────────────────

        /// <summary>
        /// The shop you start a brand-new game with.
        ///
        /// Shelves and the till go inside the building; the pens go out in the yard, which is
        /// the whole point of the layout — the animals live outdoors and the supplies are sold
        /// indoors. Cells are computed from the live geometry rather than hard-coded, so the
        /// layout survives a change to the yard or shop size.
        /// </summary>
        public List<FurniturePlacement> StarterLayout(GridManager grid)
        {
            var list = new List<FurniturePlacement>();
            Vector2Int shop = grid.WorldToGrid(ShopCentre);
            int halfX = Mathf.FloorToInt(RoomWidth  * 0.5f / GridManager.CellSize);
            int halfZ = Mathf.FloorToInt(RoomDepth * 0.5f / GridManager.CellSize);

            // ── Indoors: counter facing the door, shelves along the back and side walls ──
            list.Add(new FurniturePlacement(BuildCatalog.Counter,
                     new Vector2Int(shop.x - 1, shop.y - halfZ), null, 0f));

            string[] categories = { "Food", "Food", "Toy", "Accessory", "Medicine", "Toy" };
            int c = 0;
            for (int x = shop.x - halfX; x < shop.x + halfX; x++)
            {
                // back wall, skipping the counter's two cells
                if (x >= shop.x - 1 && x <= shop.x) continue;
                list.Add(new FurniturePlacement(BuildCatalog.ShelfSmall,
                         new Vector2Int(x, shop.y - halfZ), categories[c++ % categories.Length], 0f));
            }

            list.Add(new FurniturePlacement(BuildCatalog.ShelfLarge,
                     new Vector2Int(shop.x - halfX, shop.y - halfZ + 2), "Food", 90f));
            list.Add(new FurniturePlacement(BuildCatalog.ShelfLarge,
                     new Vector2Int(shop.x + halfX - 2, shop.y - halfZ + 2), "Accessory", 270f));

            // ── Outdoors: pens inside the fenced paddock ──
            Vector2Int yardStart = grid.WorldToGrid(new Vector3(PaddockArea.x + 3f, 0f,
                                                                PaddockArea.y + PaddockArea.height - 5f));
            // Only species with real models — placeholder blobs are worse than fewer pens.
            string[] species = { "Dog", "Cat", "Fox", "Chicken", "Penguin", "Deer" };
            for (int i = 0; i < species.Length; i++)
            {
                int row = i / 3, col = i % 3;
                list.Add(new FurniturePlacement(BuildCatalog.PetPen,
                         new Vector2Int(yardStart.x + col * 3, yardStart.y - row * 3),
                         species[i], 0f));
            }

            return list;
        }

        public readonly struct FurniturePlacement
        {
            public readonly string     CatalogId;
            public readonly Vector2Int Cell;
            public readonly string     Variant;
            public readonly float      Rotation;

            public FurniturePlacement(string catalogId, Vector2Int cell, string variant, float rotation)
            { CatalogId = catalogId; Cell = cell; Variant = variant; Rotation = rotation; }
        }
    }
}
