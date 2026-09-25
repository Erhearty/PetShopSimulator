using UnityEngine;
using PetShop.Core;

namespace PetShop.Shop
{
    /// <summary>
    /// Shared state for one <see cref="ShopGenerator.Generate"/> run: the owning generator
    /// (for its public geometry and computed positions), the grid, the shop root, and the
    /// placement helpers the shop builders share.
    /// </summary>
    internal class ShopBuildContext
    {
        public ShopGenerator Gen  { get; }
        public GridManager   Grid { get; }

        public ShopBuildContext(ShopGenerator gen, GridManager grid)
        {
            Gen  = gen;
            Grid = grid;
        }

        // Read through to the generator, so every access sees the live value exactly as
        // the generator's own code did.
        public Transform ShopRoot         => Gen.ShopRoot;

        public float YardWidth            => Gen.YardWidth;
        public float YardDepth            => Gen.YardDepth;
        public float RoomWidth            => Gen.RoomWidth;
        public float RoomDepth            => Gen.RoomDepth;
        public float WallHeight           => Gen.WallHeight;
        public float DoorWidth            => Gen.DoorWidth;
        public float ShopFrontMargin      => Gen.ShopFrontMargin;

        public Vector3 ShopCentre         => Gen.ShopCentre;
        public Vector3 DoorPosition       => Gen.DoorPosition;
        public Vector3 ForecourtPosition  => Gen.ForecourtPosition;

        public Rect  PaddockArea          => Gen.PaddockArea;
        public float PathZ                => Gen.PathZ;

        // ── Placement helpers ───────────────────────────────────────────────────

        public void SpawnWall(string name, float width, Vector3 pos, float yRot = 0f)
        {
            var go = MeshBuilder.CreateWall(width, WallHeight, 0.2f, MaterialFactory.Wall, name);
            go.transform.position    = pos;
            go.transform.eulerAngles = new Vector3(0f, yRot, 0f);
            go.transform.SetParent(ShopRoot, true);
        }

        public void SpawnTrim(string name, float length, Vector3 pos, float yRot = 0f)
        {
            var go = MeshBuilder.CreateBox(length, 0.18f, 0.06f, MaterialFactory.WallTrim, name);
            go.transform.position    = pos;
            go.transform.eulerAngles = new Vector3(0f, yRot, 0f);
            go.transform.SetParent(ShopRoot, true);
            Object.Destroy(go.GetComponent<Collider>());
        }

        public void Attach(GameObject go, Vector3 pos)
        {
            go.transform.position = pos;
            go.transform.SetParent(ShopRoot, true);
        }

        /// <summary>First of these models that is installed        /// <summary>First of these models that is installed — pack model preferred, kit fallback.</summary>
        public static string Pick(string preferred, string fallback) =>
            ModelLibrary.FirstAvailable(preferred, fallback);

        public void Dress(string model, Transform parent, Vector3 position, float yRot,
                          ModelLibrary.Fit fit, float size)
        {
            if (model == null) return;
            var go = ModelLibrary.Spawn(model, parent, parent.position + position, yRot, fit, size);
            if (go == null) return;
            ModelLibrary.SetLayer(go, GameLayers.Scenery);
        }

        // Deliberately the unseeded Unity stream, as before the split.
        public float Random(float min, float max) => UnityEngine.Random.Range(min, max);
    }
}
