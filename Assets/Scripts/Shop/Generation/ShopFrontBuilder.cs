using UnityEngine;
using PetShop.Core;

namespace PetShop.Shop
{
    /// <summary>
    /// Builds the street face of the shop: the glazed shopfront, awning and sign, and the
    /// storeys above that make the shop read as the ground floor of a building.
    /// </summary>
    internal class ShopFrontBuilder
    {
        private readonly ShopBuildContext    _ctx;
        private readonly ShopBuildingBuilder _building;

        public ShopFrontBuilder(ShopBuildContext ctx, ShopBuildingBuilder building)
        {
            _ctx      = ctx;
            _building = building;
        }

        // ── Shopfront ───────────────────────────────────────────────────────────

        /// <summary>
        /// Replaces the flat south wall with a glazed shopfront: a stallriser, full-height
        /// glass between mullions, a transom, and an open doorway in the middle. This is what
        /// makes the street visible from inside.
        /// </summary>
        public void BuildShopFront()
        {
            Vector3 c = _ctx.ShopCentre;
            float hd = c.z + _ctx.RoomDepth * 0.5f;
            const float riserH  = 0.55f;   // solid base under the glass
            const float glassT  = 0.10f;
            const float frameT  = 0.26f;
            float transomY      = _ctx.WallHeight - 0.85f;

            var frameMat = MaterialFactory.Get("front_frame", new Color(0.30f, 0.34f, 0.38f), 0.25f, 0.55f);
            var riserMat = MaterialFactory.Get("front_riser", new Color(0.36f, 0.46f, 0.42f), 0f, 0.45f);
            var glassMat = MaterialFactory.Glass;

            float halfDoor = _ctx.DoorWidth * 0.5f;

            foreach (float side in new[] { -1f, 1f })
            {
                float inner = c.x + side * halfDoor;
                float outer = c.x + side * _ctx.RoomWidth * 0.5f;
                float width = Mathf.Abs(outer - inner);
                float centre = (inner + outer) * 0.5f;

                // Stallriser
                var riser = MeshBuilder.CreateBox(width, riserH, frameT, riserMat, "Stallriser");
                _ctx.Attach(riser, new Vector3(centre, 0f, hd));

                // Glazing
                var glass = MeshBuilder.CreateBox(width - 0.04f, transomY - riserH, glassT, glassMat, "Glass");
                _ctx.Attach(glass, new Vector3(centre, (riserH + transomY) * 0.5f, hd));

                // Transom above the glass
                var transom = MeshBuilder.CreateBox(width, _ctx.WallHeight - transomY, frameT, frameMat, "Transom");
                _ctx.Attach(transom, new Vector3(centre, (transomY + _ctx.WallHeight) * 0.5f, hd));

                // Mullions every ~2 m, plus the posts that close each end
                int bays = Mathf.Max(1, Mathf.RoundToInt(width / 2f));
                for (int i = 0; i <= bays; i++)
                {
                    float x = Mathf.Lerp(inner, outer, i / (float)bays);
                    var mullion = MeshBuilder.CreateBox(0.14f, transomY - riserH, frameT + 0.02f, frameMat, "Mullion");
                    _ctx.Attach(mullion, new Vector3(x, (riserH + transomY) * 0.5f, hd));
                }
            }

            // Door reveal: posts either side and a head over the opening
            foreach (float side in new[] { -1f, 1f })
            {
                var post = MeshBuilder.CreateBox(0.20f, transomY, frameT + 0.06f, frameMat, "DoorPost");
                _ctx.Attach(post, new Vector3(c.x + side * halfDoor, transomY * 0.5f, hd));
            }
            var head = MeshBuilder.CreateBox(_ctx.DoorWidth + 0.4f, _ctx.WallHeight - transomY, frameT + 0.06f, frameMat, "DoorHead");
            _ctx.Attach(head, new Vector3(c.x, (transomY + _ctx.WallHeight) * 0.5f, hd));

            BuildAwningAndSign(hd);
        }

        private void BuildAwningAndSign(float hd)
        {
            var stripeA = MaterialFactory.Get("awning_a", new Color(0.82f, 0.34f, 0.30f), 0f, 0.35f);
            var stripeB = MaterialFactory.Get("awning_b", new Color(0.95f, 0.93f, 0.88f), 0f, 0.35f);

            // Striped awning over the doorway
            float awningW = _ctx.DoorWidth + 2.4f;
            int stripes = 10;
            for (int i = 0; i < stripes; i++)
            {
                float w = awningW / stripes;
                float x = -awningW * 0.5f + w * (i + 0.5f);
                var stripe = MeshBuilder.CreateBox(w, 0.08f, 1.5f, i % 2 == 0 ? stripeA : stripeB, "AwningStripe");
                _ctx.Attach(stripe, new Vector3(_ctx.ShopCentre.x + x, _ctx.WallHeight - 0.95f, hd + 0.78f));
                stripe.transform.localEulerAngles = new Vector3(-14f, 0f, 0f);
                Object.Destroy(stripe.GetComponent<Collider>());
            }
            foreach (float side in new[] { -1f, 1f })
            {
                var stay = MeshBuilder.CreateBox(0.06f, 0.06f, 1.5f,
                    MaterialFactory.Get("awning_stay", new Color(0.25f, 0.25f, 0.27f), 0.3f, 0.5f), "AwningStay");
                _ctx.Attach(stay, new Vector3(_ctx.ShopCentre.x + side * awningW * 0.5f, _ctx.WallHeight - 1.02f, hd + 0.78f));
                Object.Destroy(stay.GetComponent<Collider>());
            }

            // Sign board and lettering above the awning
            var board = MeshBuilder.CreateBox(_ctx.RoomWidth * 0.55f, 1.25f, 0.22f,
                MaterialFactory.Get("sign_board", new Color(0.16f, 0.26f, 0.30f), 0f, 0.4f), "SignBoard");
            _ctx.Attach(board, new Vector3(_ctx.ShopCentre.x, _ctx.WallHeight + 0.75f, hd + 0.12f));

            var trim = MeshBuilder.CreateBox(_ctx.RoomWidth * 0.55f + 0.18f, 0.12f, 0.3f,
                MaterialFactory.Get("sign_trim", new Color(0.88f, 0.72f, 0.32f), 0.3f, 0.6f), "SignTrim");
            _ctx.Attach(trim, new Vector3(_ctx.ShopCentre.x, _ctx.WallHeight + 0.12f, hd + 0.12f));

            CreateSignText("PAWS & WHISKERS", new Vector3(_ctx.ShopCentre.x, _ctx.WallHeight + 1.45f, hd + 0.26f), 1.0f);
            CreateSignText("pet shop",        new Vector3(_ctx.ShopCentre.x, _ctx.WallHeight + 0.78f, hd + 0.26f), 0.5f,
                           new Color(0.75f, 0.84f, 0.88f));
        }

        private void CreateSignText(string text, Vector3 position, float size, Color? color = null)
        {
            var go = new GameObject($"Sign_{text}");
            go.transform.SetParent(_ctx.ShopRoot, true);
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
            rt.sizeDelta = new Vector2(_ctx.RoomWidth * 0.55f, size * 1.4f);
        }

        /// <summary>
        /// The storeys above the shop, so from the street the shop reads as the ground floor of
        /// a building rather than a lone box. Exterior only — nothing up here is enterable.
        /// </summary>
        public void BuildFacade()
        {
            Vector3 c = _ctx.ShopCentre;
            float hd = c.z + _ctx.RoomDepth * 0.5f;
            float hw = _ctx.RoomWidth * 0.5f;
            const float storeyH = 3.2f;
            const int   storeys = 2;

            var brick  = MaterialFactory.Get("facade", new Color(0.62f, 0.45f, 0.40f), 0f, 0.55f);
            var band   = MaterialFactory.Get("facade_band", new Color(0.82f, 0.79f, 0.74f), 0f, 0.5f);
            var window = MaterialFactory.Get("facade_window", new Color(0.20f, 0.30f, 0.36f), 0.35f, 0.75f);

            float top = _ctx.WallHeight;
            for (int s = 0; s < storeys; s++)
            {
                float y0 = top + s * storeyH;

                var wall = MeshBuilder.CreateBox(_ctx.RoomWidth, storeyH, 0.35f, brick, $"FacadeStorey{s}");
                _ctx.Attach(wall, new Vector3(c.x, y0 + storeyH * 0.5f, hd));

                // Window openings
                for (int i = 0; i < 4; i++)
                {
                    float x = Mathf.Lerp(c.x - hw + 2.2f, c.x + hw - 2.2f, i / 3f);
                    var pane = MeshBuilder.CreateBox(1.5f, 1.8f, 0.12f, window, "FacadeWindow");
                    _ctx.Attach(pane, new Vector3(x, y0 + storeyH * 0.5f, hd + 0.2f));
                    Object.Destroy(pane.GetComponent<Collider>());

                    var sill = MeshBuilder.CreateBox(1.75f, 0.14f, 0.3f, band, "Sill");
                    _ctx.Attach(sill, new Vector3(x, y0 + storeyH * 0.5f - 0.97f, hd + 0.22f));
                    Object.Destroy(sill.GetComponent<Collider>());
                }

                var course = MeshBuilder.CreateBox(_ctx.RoomWidth + 0.2f, 0.22f, 0.5f, band, "StringCourse");
                _ctx.Attach(course, new Vector3(c.x, y0, hd + 0.08f));
                Object.Destroy(course.GetComponent<Collider>());
            }

            // Parapet and side returns so the building is not a flat card
            var parapet = MeshBuilder.CreateBox(_ctx.RoomWidth + 0.4f, 0.7f, 0.7f, band, "Parapet");
            _ctx.Attach(parapet, new Vector3(c.x, top + storeys * storeyH, hd));

            foreach (float side in new[] { -1f, 1f })
            {
                // CreateBox is centred once its position is overridden, so the upper storeys
                // have to be placed at their mid-height or they swallow the shopfront.
                var flank = MeshBuilder.CreateBox(0.35f, storeys * storeyH, _ctx.RoomDepth, brick, "FacadeFlank");
                _ctx.Attach(flank, new Vector3(c.x + side * hw, top + storeys * storeyH * 0.5f, c.z));

                // The side elevations were blank slabs. Windows, a string course and a
                // downpipe give them the same language as the front.
                for (int st = 0; st < storeys; st++)
                for (int i = 0; i < 3; i++)
                {
                    float z = Mathf.Lerp(c.z - _ctx.RoomDepth * 0.35f, c.z + _ctx.RoomDepth * 0.35f, i / 2f);
                    var pane = MeshBuilder.CreateBox(0.12f, 1.5f, 1.2f, window, "SideWindow");
                    _ctx.Attach(pane, new Vector3(c.x + side * (hw + 0.18f), top + storeyH * (st + 0.5f), z));
                    Object.Destroy(pane.GetComponent<Collider>());
                }

                var course = MeshBuilder.CreateBox(0.5f, 0.2f, _ctx.RoomDepth, band, "SideCourse");
                _ctx.Attach(course, new Vector3(c.x + side * hw, top, c.z));
                Object.Destroy(course.GetComponent<Collider>());

                // Runs from the ground up to the gutter; centred once positioned, so sit it at mid-height.
                float pipeHeight = top + storeys * storeyH;
                var pipe = MeshBuilder.CreateCylinder(0.08f, pipeHeight,
                    MaterialFactory.Get("downpipe", new Color(0.36f, 0.37f, 0.38f), 0.2f, 0.3f), "Downpipe");
                pipe.transform.position = new Vector3(c.x + side * (hw + 0.2f), pipeHeight * 0.5f, c.z + _ctx.RoomDepth * 0.45f);
                pipe.transform.SetParent(_ctx.ShopRoot, true);
                Object.Destroy(pipe.GetComponent<Collider>());
            }

            // Ground-floor side walls in brick too, so the shop is not grey below the facade
            foreach (float side in new[] { -1f, 1f })
            {
                var plinth = MeshBuilder.CreateBox(0.14f, _ctx.WallHeight, _ctx.RoomDepth, brick, "SideCladding");
                _ctx.Attach(plinth, new Vector3(c.x + side * (hw + 0.1f), _ctx.WallHeight * 0.5f, c.z));
                Object.Destroy(plinth.GetComponent<Collider>());
            }

            var rearClad = MeshBuilder.CreateBox(_ctx.RoomWidth, _ctx.WallHeight, 0.14f, brick, "RearCladding");
            _ctx.Attach(rearClad, new Vector3(c.x, _ctx.WallHeight * 0.5f, c.z - _ctx.RoomDepth * 0.5f - 0.1f));
            Object.Destroy(rearClad.GetComponent<Collider>());

            float roofY = top + storeys * storeyH;
            var roof = MeshBuilder.CreateBox(_ctx.RoomWidth, 0.3f, _ctx.RoomDepth,
                MaterialFactory.Get("roof", new Color(0.34f, 0.35f, 0.37f), 0f, 0.15f), "Roof");
            _ctx.Attach(roof, new Vector3(c.x, roofY + 0.15f, c.z));

            _building.BuildRoofDetail(c, roofY + 0.3f, hw);

            // Back wall of the upper storeys, so the building is not open from behind
            var back = MeshBuilder.CreateBox(_ctx.RoomWidth, storeys * storeyH, 0.35f, brick, "FacadeBack");
            _ctx.Attach(back, new Vector3(c.x, top + storeys * storeyH * 0.5f, c.z - _ctx.RoomDepth * 0.5f));
        }
    }
}
