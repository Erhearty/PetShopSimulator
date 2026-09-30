using UnityEngine;
using PetShop.Pets;

namespace PetShop.Core
{
    /// <summary>
    /// Static factory for every piece of runtime geometry in the game.
    /// Returns plain GameObjects with only a renderer/collider — callers attach
    /// behaviour components (ShelfUnit, PetPen, ...) themselves.
    /// </summary>
    public static class MeshBuilder
    {
        /// <summary>
        /// Top surface of each board a shelf exposes, bottom-up, in local Y. Must stay in
        /// step with CreateShelf or stock floats above the boards or sinks into them.
        /// </summary>
        public static float[] ShelfBoardHeights(float height, bool twoShelves)
        {
            const float board = 0.06f;
            const float kick  = 0.14f;

            int shelfCount = twoShelves ? 2 : 1;
            var heights = new float[shelfCount + 1];
            heights[0] = kick + board;                            // base board
            for (int i = 0; i < shelfCount; i++)
                heights[i + 1] = height * (i + 1f) / (shelfCount + 1f) + board * 0.5f;
            return heights;
        }

        // ── Primitives ──────────────────────────────────────────────────────────

        public static GameObject CreateFloorTile(float width, float depth, Material mat, string name = "Floor")
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale    = new Vector3(width, 0.1f, depth);
            go.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            Apply(go, mat);
            return go;
        }

        public static GameObject CreateWall(float width, float height, float thickness, Material mat, string name = "Wall")
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = new Vector3(width, height, thickness);
            Apply(go, mat);
            return go;
        }

        /// <summary>Box whose base sits on local Y = 0.</summary>
        public static GameObject CreateBox(float w, float h, float d, Material mat, string name = "Box")
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale    = new Vector3(w, h, d);
            go.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            Apply(go, mat);
            return go;
        }

        /// <summary>Cylinder whose base sits on local Y = 0.</summary>
        public static GameObject CreateCylinder(float radius, float height, Material mat, string name = "Cylinder")
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.localScale    = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            Apply(go, mat);
            return go;
        }

        /// <summary>Sphere whose base sits on local Y = 0.</summary>
        public static GameObject CreateSphere(float size, Material mat, string name = "Sphere")
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.localScale    = Vector3.one * size;
            go.transform.localPosition = new Vector3(0f, size * 0.5f, 0f);
            Apply(go, mat);
            return go;
        }

        // ── Furniture ───────────────────────────────────────────────────────────

        /// <summary>
        /// A shop shelving unit: solid sides and back, a top, a kick plate and thick boards.
        /// The earlier version used 4 cm panels, which from a few metres away read as bare
        /// rails with stock floating between them.
        /// </summary>
        public static GameObject CreateShelf(float width, float height, float depth, bool twoShelves = true)
        {
            var root  = new GameObject("Shelf");
            var wood  = MaterialFactory.Shelf;
            var back  = MaterialFactory.ShelfBack;

            const float panel = 0.07f;   // sides and top
            const float board = 0.06f;   // shelf boards
            const float kick  = 0.14f;   // plinth height

            // Plinth, so the unit sits on the floor rather than hovering
            var plinth = CreateBox(width - panel, kick, depth - 0.06f, back, "Plinth");
            plinth.transform.SetParent(root.transform, false);

            // Back panel, in a darker wood so stock reads against it
            var backPanel = CreateBox(width, height, 0.05f, back, "Back");
            backPanel.transform.SetParent(root.transform, false);
            backPanel.transform.localPosition = new Vector3(0f, height * 0.5f, -(depth * 0.5f - 0.025f));

            // Base board sits on the plinth
            var baseBoard = CreateBox(width - panel * 2f, board, depth, wood, "Base");
            baseBoard.transform.SetParent(root.transform, false);
            baseBoard.transform.localPosition = new Vector3(0f, kick + board * 0.5f, 0f);

            int shelfCount = twoShelves ? 2 : 1;
            for (int i = 0; i < shelfCount; i++)
            {
                float t = (i + 1f) / (shelfCount + 1f);
                var shelf = CreateBox(width - panel * 2f, board, depth, wood, $"Shelf{i}");
                shelf.transform.SetParent(root.transform, false);
                shelf.transform.localPosition = new Vector3(0f, height * t, 0f);

                // Front lip, which is what makes a board read as a shelf at a distance
                var lip = CreateBox(width - panel * 2f, 0.05f, 0.03f, back, "Lip");
                lip.transform.SetParent(root.transform, false);
                lip.transform.localPosition = new Vector3(0f, height * t + board * 0.5f + 0.02f, depth * 0.5f);
            }

            foreach (float sign in new[] { -1f, 1f })
            {
                var side = CreateBox(panel, height, depth, wood, "Side");
                side.transform.SetParent(root.transform, false);
                side.transform.localPosition = new Vector3(sign * (width - panel) * 0.5f, height * 0.5f, 0f);
            }

            var top = CreateBox(width + 0.04f, panel, depth + 0.04f, wood, "Top");
            top.transform.SetParent(root.transform, false);
            top.transform.localPosition = new Vector3(0f, height, 0f);

            return root;
        }

        public static GameObject CreateCounter(float width, float height, float depth)
        {
            var root = new GameObject("Counter");

            var body = CreateBox(width, height, depth, MaterialFactory.Counter, "Body");
            body.transform.SetParent(root.transform, false);

            var top = CreateBox(width + 0.08f, 0.06f, depth + 0.08f, MaterialFactory.CounterTrim, "Top");
            top.transform.SetParent(root.transform, false);
            top.transform.localPosition = new Vector3(0f, height, 0f);

            var trim = CreateBox(width, 0.08f, 0.04f, MaterialFactory.CounterTrim, "FrontTrim");
            trim.transform.SetParent(root.transform, false);
            trim.transform.localPosition = new Vector3(0f, height * 0.62f, depth * 0.5f);

            var reg = CreateBox(0.4f, 0.35f, 0.3f,
                MaterialFactory.Get("register", new Color(0.15f, 0.16f, 0.18f)), "Register");
            reg.transform.SetParent(root.transform, false);
            reg.transform.localPosition = new Vector3(width * 0.28f, height + 0.06f, 0f);

            var screen = CreateBox(0.3f, 0.2f, 0.03f,
                MaterialFactory.Get("register_screen", new Color(0.35f, 0.85f, 0.65f)), "Screen");
            screen.transform.SetParent(reg.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.9f, 0.55f);

            return root;
        }

        public static GameObject CreatePetPen(float size)
        {
            var root = new GameObject("PetPen");
            const float wallH = 0.6f;
            const float wallT = 0.08f;

            var floor = CreateBox(size, 0.04f, size, MaterialFactory.Pen, "PenFloor");
            floor.transform.SetParent(root.transform, false);

            AddFenceWall(root, size, wallH, wallT, new Vector3(0f, 0f, -size * 0.5f),  0f, "FenceBack");
            AddFenceWall(root, size, wallH, wallT, new Vector3(-size * 0.5f, 0f, 0f), 90f, "FenceLeft");
            AddFenceWall(root, size, wallH, wallT, new Vector3( size * 0.5f, 0f, 0f), 90f, "FenceRight");

            // Front wall with a gate gap in the middle
            float gateW = size * 0.35f;
            float sideW = (size - gateW) * 0.5f;
            float sideX = (size - sideW) * 0.5f;
            foreach (float sign in new[] { -1f, 1f })
                AddFenceWall(root, sideW, wallH, wallT,
                             new Vector3(sign * sideX, 0f, size * 0.5f), 0f, "FenceFrontSide");

            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                const float postH = wallH + 0.12f;
                var post = CreateBox(0.14f, postH, 0.14f, MaterialFactory.PenFence, "Post");
                post.transform.SetParent(root.transform, false);
                // Overwriting localPosition drops CreateBox's base lift, so re-apply it (half height).
                post.transform.localPosition = new Vector3(sx * size * 0.5f, postH * 0.5f, sz * size * 0.5f);
            }
            return root;
        }

        private static void AddFenceWall(GameObject parent, float width, float height, float thickness,
                                         Vector3 pos, float yRot, string name)
        {
            var wall = CreateBox(width, height, thickness, MaterialFactory.PenFence, name);
            wall.transform.SetParent(parent.transform, false);
            // pos is the wall's footing; lift by half its height so its base stands on it.
            wall.transform.localPosition   = pos + Vector3.up * (height * 0.5f);
            wall.transform.localEulerAngles = new Vector3(0f, yRot, 0f);
        }

        // ── Characters & pets ───────────────────────────────────────────────────

        /// <summary>A humanoid stand-in: legs, torso, head, eyes. Base sits on local Y = 0.</summary>
        public static GameObject CreateHumanoid(Color skin, Color shirt, Color trousers, string name = "Humanoid")
        {
            var root     = new GameObject(name);
            var skinMat  = MaterialFactory.Get($"skin_{ColorKey(skin)}",   skin,     0f, 0.3f);
            var shirtMat = MaterialFactory.Get($"shirt_{ColorKey(shirt)}", shirt,    0f, 0.4f);
            var legMat   = MaterialFactory.Get($"leg_{ColorKey(trousers)}", trousers, 0f, 0.4f);
            var eyeMat   = MaterialFactory.Get("eye_dark", new Color(0.08f, 0.06f, 0.05f), 0f, 0.8f);

            foreach (float sx in new[] { -0.11f, 0.11f })
            {
                var leg = CreateCylinder(0.09f, 0.85f, legMat, "Leg");
                leg.transform.SetParent(root.transform, false);
                leg.transform.localPosition = new Vector3(sx, 0f, 0f);
            }

            var torso = CreateBox(0.48f, 0.72f, 0.28f, shirtMat, "Torso");
            torso.transform.SetParent(root.transform, false);
            torso.transform.localPosition = new Vector3(0f, 1.21f, 0f);

            foreach (float sx in new[] { -0.30f, 0.30f })
            {
                var arm = CreateCylinder(0.065f, 0.62f, skinMat, "Arm");
                arm.transform.SetParent(root.transform, false);
                arm.transform.localPosition = new Vector3(sx, 1.24f, 0f);
            }

            var head = CreateSphere(0.34f, skinMat, "Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.72f, 0f);

            foreach (float sx in new[] { -0.08f, 0.08f })
            {
                var eye = CreateSphere(0.06f, eyeMat, "Eye");
                eye.transform.SetParent(root.transform, false);
                eye.transform.localPosition = new Vector3(sx, 1.76f, 0.14f);
            }
            return root;
        }

        /// <summary>A small procedural animal. Base sits on local Y = 0.</summary>
        /// <summary>A small procedural animal, roughly 0.6 m tall at scale 1. Base on local Y = 0.</summary>
        public static GameObject CreatePet(Pet.Species species, Color coat, float scale = 1f)
        {
            var root    = new GameObject($"Pet_{species}");
            var bodyMat = MaterialFactory.Get($"coat_{ColorKey(coat)}", coat, 0f, 0.25f);
            var darkMat = MaterialFactory.Get("pet_eye", new Color(0.06f, 0.05f, 0.05f), 0f, 0.8f);

            switch (species)
            {
                case Pet.Species.Fish:
                {
                    var body = CreateSphere(0.24f, bodyMat, "Body");
                    body.transform.SetParent(root.transform, false);
                    body.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                    body.transform.localScale    = new Vector3(0.34f, 0.20f, 0.18f);
                    var tail = CreateBox(0.12f, 0.16f, 0.03f, bodyMat, "Tail");
                    tail.transform.SetParent(root.transform, false);
                    tail.transform.localPosition = new Vector3(0f, 0.10f, -0.20f);
                    break;
                }
                case Pet.Species.Parrot:
                {
                    var body = CreateSphere(0.26f, bodyMat, "Body");
                    body.transform.SetParent(root.transform, false);
                    body.transform.localPosition = new Vector3(0f, 0.14f, 0f);
                    var head = CreateSphere(0.17f, bodyMat, "Head");
                    head.transform.SetParent(root.transform, false);
                    head.transform.localPosition = new Vector3(0f, 0.38f, 0.04f);
                    var beak = CreateBox(0.06f, 0.06f, 0.10f,
                        MaterialFactory.Get("beak", new Color(0.95f, 0.72f, 0.20f)), "Beak");
                    beak.transform.SetParent(root.transform, false);
                    beak.transform.localPosition = new Vector3(0f, 0.42f, 0.16f);
                    AddEyes(root, darkMat, 0.045f, 0.06f, 0.48f, 0.12f);
                    break;
                }
                default:
                {
                    bool longEars = species == Pet.Species.Rabbit;
                    bool tallLegs = species == Pet.Species.Dog;

                    float legH = tallLegs ? 0.18f : 0.11f;
                    foreach (float sx in new[] { -0.11f, 0.11f })
                    foreach (float sz in new[] { -0.13f, 0.13f })
                    {
                        var leg = CreateCylinder(0.045f, legH, bodyMat, "Leg");
                        leg.transform.SetParent(root.transform, false);
                        leg.transform.localPosition = new Vector3(sx, 0f, sz);
                    }

                    var body = CreateSphere(0.30f, bodyMat, "Body");
                    body.transform.SetParent(root.transform, false);
                    body.transform.localPosition = new Vector3(0f, legH + 0.13f, 0f);
                    body.transform.localScale    = new Vector3(0.28f, 0.26f, 0.40f);

                    float headY = legH + 0.26f;
                    var head = CreateSphere(0.22f, bodyMat, "Head");
                    head.transform.SetParent(root.transform, false);
                    head.transform.localPosition = new Vector3(0f, headY, 0.20f);

                    float earH = longEars ? 0.26f : 0.10f;
                    float earW = longEars ? 0.06f : 0.09f;
                    foreach (float sx in new[] { -0.07f, 0.07f })
                    {
                        var ear = CreateBox(earW, earH, 0.04f, bodyMat, "Ear");
                        ear.transform.SetParent(root.transform, false);
                        ear.transform.localPosition = new Vector3(sx, headY + 0.09f, 0.18f);
                    }

                    var tail = CreateSphere(0.10f, bodyMat, "Tail");
                    tail.transform.SetParent(root.transform, false);
                    tail.transform.localPosition = new Vector3(0f, legH + 0.16f, -0.24f);

                    AddEyes(root, darkMat, 0.045f, 0.07f, headY + 0.05f, 0.30f);
                    break;
                }
            }

            root.transform.localScale = Vector3.one * scale;
            return root;
        }

        private static void AddEyes(GameObject root, Material mat, float size, float spread, float y, float z)
        {
            foreach (float sx in new[] { -spread, spread })
            {
                var eye = CreateSphere(size, mat, "Eye");
                eye.transform.SetParent(root.transform, false);
                eye.transform.localPosition = new Vector3(sx, y, z);
            }
        }

        // ── Utilities ───────────────────────────────────────────────────────────

        /// <summary>Namespace every game script lives in; any other script in a model came with a pack.</summary>
        private const string GameNamespace = "PetShop";

        /// <summary>
        /// Removes every collider in a hierarchy — characters use their own controller/agent.
        /// Pack scripts (e.g. the animal pack's CreatureMover / MovePlayerInput) are disabled first
        /// so they cannot drive the model. A CharacterController is disabled rather than destroyed:
        /// those scripts [RequireComponent] it, and destroying it logs an error.
        /// </summary>
        public static void StripColliders(GameObject root)
        {
            DisablePackScripts(root);
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                if (col is CharacterController controller) controller.enabled = false;
                else Object.Destroy(col);
            }
        }

        /// <summary>Disables every MonoBehaviour in the hierarchy that is not one of the game's own.</summary>
        private static void DisablePackScripts(GameObject root)
        {
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // A missing-script slot comes back null.
                if (script == null || IsGameScript(script)) continue;
                script.enabled = false;
            }
        }

        /// <summary>True when <paramref name="script"/>'s type lives in the game's own namespace or one below it.</summary>
        private static bool IsGameScript(MonoBehaviour script)
        {
            string ns = script.GetType().Namespace;
            return ns != null && (ns == GameNamespace || ns.StartsWith(GameNamespace + ".", System.StringComparison.Ordinal));
        }

        public static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        public static void SetMaterialRecursive(GameObject root, Material mat)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = mat;
        }

        private static string ColorKey(Color c) =>
            $"{Mathf.RoundToInt(c.r * 32)}_{Mathf.RoundToInt(c.g * 32)}_{Mathf.RoundToInt(c.b * 32)}";

        private static void Apply(GameObject go, Material mat)
        {
            if (mat == null) return;
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = mat;
        }
    }
}
