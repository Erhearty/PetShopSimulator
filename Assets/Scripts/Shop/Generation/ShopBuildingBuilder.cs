using UnityEngine;
using PetShop.Core;

namespace PetShop.Shop
{
    /// <summary>
    /// Builds the yard the shop stands in (ground, forecourt, perimeter and street railing)
    /// and the shop building's shell and roof plant.
    /// </summary>
    internal class ShopBuildingBuilder
    {
        private readonly ShopBuildContext _ctx;

        public ShopBuildingBuilder(ShopBuildContext ctx)
        {
            _ctx = ctx;
        }

        // ── The yard ────────────────────────────────────────────────────────────

        /// <summary>
        /// The open lot. One continuous slab under everything — including the shop's
        /// footprint — so the NavMesh never has a seam at the shop door.
        /// </summary>
        public void BuildYard()
        {
            float hw = _ctx.YardWidth * 0.5f, hd = _ctx.YardDepth * 0.5f;

            var ground = MeshBuilder.CreateBox(_ctx.YardWidth, 0.2f, _ctx.YardDepth,
                MaterialFactory.Get("yard", new Color(0.38f, 0.42f, 0.31f), 0f, 0.12f), "YardGround");
            _ctx.Attach(ground, new Vector3(0f, -0.1f, 0f));

            // A paved forecourt strip in front of the shop, so the entrance reads
            var apron = MeshBuilder.CreateBox(_ctx.RoomWidth + 8f, 0.06f, 7f,
                MaterialFactory.Get("apron_paving", new Color(0.56f, 0.55f, 0.52f), 0f, 0.18f), "Forecourt");
            _ctx.Attach(apron, new Vector3(_ctx.ShopCentre.x + 2f, 0.01f, _ctx.ShopCentre.z + _ctx.RoomDepth * 0.5f + 3f));
            Object.Destroy(apron.GetComponent<Collider>());

            // Perimeter: solid wall on three sides, railing with a gate along the street
            var wallMat = MaterialFactory.Get("yard_wall", new Color(0.70f, 0.66f, 0.58f), 0f, 0.5f);
            SpawnBoundary("YardWallWest",  _ctx.YardDepth, new Vector3(-hw, 1.1f, 0f), 90f, wallMat);
            SpawnBoundary("YardWallEast",  _ctx.YardDepth, new Vector3( hw, 1.1f, 0f), 90f, wallMat);
            SpawnBoundary("YardWallBack",  _ctx.YardWidth, new Vector3(0f, 1.1f, -hd), 0f, wallMat);

            // Street frontage: railing either side of an entrance aligned with the shop door
            float gateCentre = _ctx.ShopCentre.x;
            const float gateWidth = 9f;
            float leftEnd  = gateCentre - gateWidth * 0.5f;
            float rightEnd = gateCentre + gateWidth * 0.5f;

            BuildRailing(-hw, leftEnd, hd);
            BuildRailing(rightEnd, hw, hd);

            foreach (float x in new[] { leftEnd, rightEnd })
            {
                var pier = MeshBuilder.CreateBox(0.5f, 2.2f, 0.5f, wallMat, "GatePier");
                _ctx.Attach(pier, new Vector3(x, 0f, hd));
            }
        }

        private void SpawnBoundary(string name, float length, Vector3 pos, float yRot, Material mat)
        {
            var go = MeshBuilder.CreateWall(length, 2.2f, 0.3f, mat, name);
            go.transform.position    = pos;
            go.transform.eulerAngles = new Vector3(0f, yRot, 0f);
            go.transform.SetParent(_ctx.ShopRoot, true);
        }

        /// <summary>Low railing along the street frontage: a kerb rail plus uprights.</summary>
        private void BuildRailing(float fromX, float toX, float z)
        {
            float length = toX - fromX;
            if (length <= 0.2f) return;

            var railMat = MaterialFactory.Get("yard_rail", new Color(0.32f, 0.36f, 0.34f), 0.2f, 0.5f);

            var plinth = MeshBuilder.CreateBox(length, 0.45f, 0.32f,
                MaterialFactory.Get("yard_wall", new Color(0.70f, 0.66f, 0.58f), 0f, 0.5f), "Plinth");
            _ctx.Attach(plinth, new Vector3((fromX + toX) * 0.5f, 0f, z));

            foreach (float y in new[] { 1.0f, 1.35f })
            {
                var rail = MeshBuilder.CreateBox(length, 0.07f, 0.07f, railMat, "Rail");
                _ctx.Attach(rail, new Vector3((fromX + toX) * 0.5f, y, z));
                Object.Destroy(rail.GetComponent<Collider>());
            }
            for (float x = fromX + 0.6f; x < toX; x += 1.6f)
            {
                var post = MeshBuilder.CreateBox(0.07f, 1.0f, 0.07f, railMat, "RailPost");
                _ctx.Attach(post, new Vector3(x, 0.45f, z));
                Object.Destroy(post.GetComponent<Collider>());
            }

            // Invisible barrier so nobody hops the railing
            var blocker = new GameObject("RailBlocker");
            blocker.transform.SetParent(_ctx.ShopRoot, false);
            blocker.transform.position = new Vector3((fromX + toX) * 0.5f, 1.2f, z);
            blocker.AddComponent<BoxCollider>().size = new Vector3(length, 2.4f, 0.3f);
            blocker.layer = GameLayers.Ghost;
        }

        // ── The shop building ───────────────────────────────────────────────────

        public void BuildShopBuilding()
        {
            Vector3 c = _ctx.ShopCentre;
            float hw = _ctx.RoomWidth * 0.5f, hd = _ctx.RoomDepth * 0.5f;

            var floor = MeshBuilder.CreateFloorTile(_ctx.RoomWidth, _ctx.RoomDepth, MaterialFactory.Floor, "ShopFloor");
            _ctx.Attach(floor, c + new Vector3(0f, 0.02f, 0f));

            var ceil = MeshBuilder.CreateWall(_ctx.RoomWidth, 0.1f, _ctx.RoomDepth, MaterialFactory.Wall, "Ceiling");
            _ctx.Attach(ceil, c + new Vector3(0f, _ctx.WallHeight, 0f));

            _ctx.SpawnWall("WallNorth", _ctx.RoomWidth, c + new Vector3(0f, _ctx.WallHeight * 0.5f, -hd));
            _ctx.SpawnWall("WallEast",  _ctx.RoomDepth, c + new Vector3(hw, _ctx.WallHeight * 0.5f, 0f), 90f);
            _ctx.SpawnWall("WallWest",  _ctx.RoomDepth, c + new Vector3(-hw, _ctx.WallHeight * 0.5f, 0f), 90f);

            _ctx.SpawnTrim("TrimN", _ctx.RoomWidth, c + new Vector3(0f, 0.09f, -hd + 0.12f));
            _ctx.SpawnTrim("TrimE", _ctx.RoomDepth, c + new Vector3(hw - 0.12f, 0.09f, 0f), 90f);
            _ctx.SpawnTrim("TrimW", _ctx.RoomDepth, c + new Vector3(-hw + 0.12f, 0.09f, 0f), 90f);

            var mat = MeshBuilder.CreateBox(_ctx.DoorWidth, 0.02f, 1.6f,
                MaterialFactory.Get("mat", new Color(0.35f, 0.28f, 0.24f)), "WelcomeMat");
            _ctx.Attach(mat, c + new Vector3(0f, 0.04f, hd - 1f));
            Object.Destroy(mat.GetComponent<Collider>());
        }

        /// <summary>
        /// Plant on the roof: a border trim, an air-conditioning unit and a vent pipe. From
        /// the street the roofline is visible above the facade, and a bare slab reads as an
        /// unfinished block.
        /// </summary>
        public void BuildRoofDetail(Vector3 c, float roofTop, float hw)
        {
            float hd = _ctx.RoomDepth * 0.5f;

            // Border trim round the edge of the roof
            var trim = MaterialFactory.Get("roof_trim", new Color(0.46f, 0.47f, 0.49f), 0f, 0.2f);
            foreach (var (size, offset) in new[]
            {
                (new Vector3(_ctx.RoomWidth + 0.3f, 0.34f, 0.28f), new Vector3(0f, 0f,  hd)),
                (new Vector3(_ctx.RoomWidth + 0.3f, 0.34f, 0.28f), new Vector3(0f, 0f, -hd)),
                (new Vector3(0.28f, 0.34f, _ctx.RoomDepth + 0.3f), new Vector3( hw, 0f, 0f)),
                (new Vector3(0.28f, 0.34f, _ctx.RoomDepth + 0.3f), new Vector3(-hw, 0f, 0f)),
            })
            {
                var edge = MeshBuilder.CreateBox(size.x, size.y, size.z, trim, "RoofTrim");
                _ctx.Attach(edge, new Vector3(c.x, roofTop + size.y * 0.5f, c.z) + offset);
                Object.Destroy(edge.GetComponent<Collider>());
            }

            // Air-conditioning unit on a short plinth
            Vector3 acAt = new(c.x + hw * 0.45f, roofTop, c.z - hd * 0.35f);

            var plinth = MeshBuilder.CreateBox(2.0f, 0.18f, 1.5f,
                MaterialFactory.Get("roof_plinth", new Color(0.38f, 0.38f, 0.40f), 0f, 0.15f), "AcPlinth");
            _ctx.Attach(plinth, acAt + new Vector3(0f, 0.09f, 0f));
            Object.Destroy(plinth.GetComponent<Collider>());

            var acBody = MeshBuilder.CreateBox(1.7f, 1.0f, 1.2f,
                MaterialFactory.Get("roof_ac", new Color(0.72f, 0.73f, 0.75f), 0.3f, 0.35f), "AcUnit");
            _ctx.Attach(acBody, acAt + new Vector3(0f, 0.18f + 0.5f, 0f));

            var grille = MeshBuilder.CreateBox(1.5f, 0.75f, 0.06f,
                MaterialFactory.Get("roof_grille", new Color(0.30f, 0.31f, 0.33f), 0.2f, 0.3f), "AcGrille");
            _ctx.Attach(grille, acAt + new Vector3(0f, 0.68f, 0.62f));
            Object.Destroy(grille.GetComponent<Collider>());

            var fanHousing = MeshBuilder.CreateCylinder(0.38f, 0.16f,
                MaterialFactory.Get("roof_fan", new Color(0.26f, 0.27f, 0.29f), 0.3f, 0.4f), "AcFan");
            fanHousing.transform.position = acAt + new Vector3(0f, 1.18f, 0f);
            fanHousing.transform.SetParent(_ctx.ShopRoot, true);
            Object.Destroy(fanHousing.GetComponent<Collider>());

            // Vent pipe with a cowl
            Vector3 ventAt = new(c.x - hw * 0.5f, roofTop, c.z + hd * 0.2f);
            var pipeMat = MaterialFactory.Get("roof_pipe", new Color(0.55f, 0.56f, 0.58f), 0.35f, 0.4f);

            var pipe = MeshBuilder.CreateCylinder(0.16f, 1.5f, pipeMat, "VentPipe");
            pipe.transform.position = ventAt;
            pipe.transform.SetParent(_ctx.ShopRoot, true);
            Object.Destroy(pipe.GetComponent<Collider>());

            var cowl = MeshBuilder.CreateCylinder(0.26f, 0.22f, pipeMat, "VentCowl");
            cowl.transform.position = ventAt + new Vector3(0f, 1.5f, 0f);
            cowl.transform.SetParent(_ctx.ShopRoot, true);
            Object.Destroy(cowl.GetComponent<Collider>());

            // A second, smaller flue
            var flue = MeshBuilder.CreateCylinder(0.1f, 0.9f, pipeMat, "Flue");
            flue.transform.position = ventAt + new Vector3(0.9f, 0f, 0.6f);
            flue.transform.SetParent(_ctx.ShopRoot, true);
            Object.Destroy(flue.GetComponent<Collider>());
        }
    }
}
