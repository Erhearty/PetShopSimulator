using UnityEngine;
using PetShop.Core;

namespace PetShop.Shop
{
    /// <summary>
    /// Fills the buildable floor grid, lights the scene, and dresses the shop interior.
    /// </summary>
    internal class ShopInteriorBuilder
    {
        private readonly ShopBuildContext _ctx;

        public ShopInteriorBuilder(ShopBuildContext ctx)
        {
            _ctx = ctx;
        }

        // ── Grid ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Every cell inside the yard is buildable — that is the open red area in the brief,
        /// including the shop's own footprint, so shelves go indoors and pens outdoors
        /// through exactly the same placement path.
        /// </summary>
        public void FillFloorGrid()
        {
            float cs = GridManager.CellSize;
            int halfX = Mathf.FloorToInt(_ctx.YardWidth * 0.5f / cs);
            int halfZ = Mathf.FloorToInt(_ctx.YardDepth * 0.5f / cs);
            _ctx.Grid.FillFloorRect(new Vector2Int(-halfX, -halfZ), new Vector2Int(halfX * 2, halfZ * 2));

            // Keep the shop doorway and the path through it clear of furniture
            Vector2Int door = _ctx.Grid.WorldToGrid(_ctx.DoorPosition);
            for (int dz = -1; dz <= 2; dz++)
                for (int dx = -1; dx <= 0; dx++)
                    _ctx.Grid.SetFloor(new Vector2Int(door.x + dx, door.y + dz), false);
        }

        // ── Lighting ────────────────────────────────────────────────────────────

        public void SetupLighting()
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
            sun.transform.SetParent(_ctx.ShopRoot, true);
            RenderSettings.sun = sun;

            if (sky != null) DynamicGI.UpdateEnvironment();

            // Interior fill so the shop is not a cave once you step away from the window
            Vector3 shop = _ctx.ShopCentre;
            foreach (float ox in new[] { -_ctx.RoomWidth * 0.28f, _ctx.RoomWidth * 0.28f })
            foreach (float oz in new[] { -_ctx.RoomDepth * 0.25f, _ctx.RoomDepth * 0.25f })
            {
                float x = shop.x + ox, z = shop.z + oz;
                var lamp = new GameObject("CeilingLight").AddComponent<Light>();
                lamp.type      = LightType.Point;
                lamp.range     = 9f;
                lamp.intensity = 0.45f;
                lamp.color     = new Color(1f, 0.95f, 0.84f);
                lamp.shadows   = LightShadows.None;
                lamp.transform.position = new Vector3(x, _ctx.WallHeight - 0.5f, z);
                lamp.transform.SetParent(_ctx.ShopRoot, true);

                var fitting = ModelLibrary.Spawn(ModelLibrary.Furniture + "lampSquareCeiling", _ctx.ShopRoot,
                    new Vector3(x, _ctx.WallHeight, z), 0f, ModelLibrary.Fit.Height, 0.55f);
                if (fitting != null)
                {
                    // The model hangs from its top, so drop it under the ceiling.
                    fitting.transform.position = new Vector3(x, _ctx.WallHeight - 0.55f, z);
                    ModelLibrary.SetLayer(fitting, GameLayers.Scenery);
                }
                else
                {
                    var shade = MeshBuilder.CreateBox(0.7f, 0.08f, 0.7f,
                        MaterialFactory.Get("lamp", new Color(0.95f, 0.93f, 0.85f)), "Shade");
                    shade.transform.position = new Vector3(x, _ctx.WallHeight - 0.2f, z);
                    shade.transform.SetParent(_ctx.ShopRoot, true);
                    Object.Destroy(shade.GetComponent<Collider>());
                }
            }
        }

        // ── Interior dressing ───────────────────────────────────────────────────

        /// <summary>
        /// Decorative props that make the shop look lived in. None of this is interactive or
        /// grid-registered, and every model is optional — the shop still works without the
        /// Kenney kits present.
        /// </summary>
        public void DressInterior()
        {
            var dressing = new GameObject("Dressing").transform;
            dressing.SetParent(_ctx.ShopRoot, false);
            dressing.position = _ctx.ShopCentre;   // interior props are laid out shop-relative

            float hw = _ctx.RoomWidth * 0.5f, hd = _ctx.RoomDepth * 0.5f;

            // Wall shelving down both side walls — the shop's stock room look
            foreach (float z in new[] { -3.5f, -1f, 1.5f })
            foreach (float side in new[] { -1f, 1f })
                _ctx.Dress(ShopBuildContext.Pick("Packs/Furniture/Book Shelve/BookShelve", ModelLibrary.Furniture + "bookcaseClosed"),
                      dressing, new Vector3(side * (hw - 0.55f), 0f, z), side > 0 ? 90f : -90f,
                      ModelLibrary.Fit.Height, 2.0f);

            // Mats: one at the door, one in the middle of the floor
            _ctx.Dress(ShopBuildContext.Pick("Packs/Furniture/Floor Tiles/FloorTile_1", ModelLibrary.Furniture + "rugRectangle"),
                  dressing, new Vector3(0f, 0.01f, hd - 2.2f), 0f, ModelLibrary.Fit.Width, 4.2f);
            _ctx.Dress(ModelLibrary.FirstAvailable(ModelLibrary.Furniture + "rugRound"),
                  dressing, new Vector3(0f, 0.01f, -0.5f), 0f, ModelLibrary.Fit.Width, 4.5f);

            // Stockroom clutter behind the counter — feed sacks and boxes, not café furniture
            _ctx.Dress(ModelLibrary.FirstAvailable(ModelLibrary.Furniture + "cardboardBoxClosed"), dressing,
                  new Vector3(-hw + 1.5f, 0f, -hd + 2.4f), 20f, ModelLibrary.Fit.Height, 0.6f);
            _ctx.Dress(ModelLibrary.FirstAvailable(ModelLibrary.Furniture + "cardboardBoxOpen"), dressing,
                  new Vector3(-hw + 2.3f, 0f, -hd + 3.1f), -12f, ModelLibrary.Fit.Height, 0.6f);
            _ctx.Dress(ShopBuildContext.Pick("Packs/City/Props/Props_Dustbin", ModelLibrary.Furniture + "trashcan"), dressing,
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
                _ctx.Dress(sack, parent, new Vector3(x, y, 1.35f + (i % 2) * 0.45f),
                      _ctx.Random(-12f, 12f), ModelLibrary.Fit.Height, 0.42f);
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
            Object.Destroy(water.GetComponent<Collider>());

            var gravel = MeshBuilder.CreateBox(1.3f, 0.08f, 0.42f,
                MaterialFactory.Get("tank_gravel", new Color(0.52f, 0.45f, 0.34f), 0f, 0.2f), "TankGravel");
            gravel.transform.SetParent(parent, false);
            gravel.transform.localPosition = new Vector3(hw - 1.1f, 0.79f, 2.4f);
            Object.Destroy(gravel.GetComponent<Collider>());

            // Notice board by the door
            var board = MeshBuilder.CreateBox(1.2f, 0.85f, 0.06f,
                MaterialFactory.Get("notice_board", new Color(0.45f, 0.32f, 0.20f), 0f, 0.2f), "NoticeBoard");
            board.transform.SetParent(parent, false);
            board.transform.localPosition = new Vector3(-hw + 0.25f, 1.6f, hd - 2.6f);
            board.transform.localEulerAngles = new Vector3(0f, 90f, 0f);
            Object.Destroy(board.GetComponent<Collider>());

            var noticeMat = MaterialFactory.Get("notice", new Color(0.93f, 0.91f, 0.85f), 0f, 0.1f);
            for (int i = 0; i < 5; i++)
            {
                var notice = MeshBuilder.CreateBox(0.22f, 0.28f, 0.02f, noticeMat, "Notice");
                notice.transform.SetParent(parent, false);
                notice.transform.localPosition = new Vector3(
                    -hw + 0.21f, 1.35f + (i / 3) * 0.42f, hd - 3.0f + (i % 3) * 0.36f);
                notice.transform.localEulerAngles = new Vector3(0f, 90f, _ctx.Random(-5f, 5f));
                Object.Destroy(notice.GetComponent<Collider>());
            }

            // Leads and collars hanging on a rail behind the counter
            var rail = MeshBuilder.CreateBox(2.2f, 0.05f, 0.05f,
                MaterialFactory.Get("lead_rail", new Color(0.40f, 0.42f, 0.44f), 0.3f, 0.4f), "LeadRail");
            rail.transform.SetParent(parent, false);
            rail.transform.localPosition = new Vector3(2.4f, 1.85f, -hd + 0.35f);
            Object.Destroy(rail.GetComponent<Collider>());

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
                Object.Destroy(lead.GetComponent<Collider>());
            }
        }
    }
}
