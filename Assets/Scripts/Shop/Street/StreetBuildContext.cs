using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Shop
{
    /// <summary>
    /// Shared state for one <see cref="StreetGenerator.Generate"/> run: the street root, the
    /// single seeded random stream, the street geometry the builders read, and the helpers
    /// that place and track what they spawn.
    /// </summary>
    internal class StreetBuildContext
    {
        public Transform StreetRoot { get; }
        public System.Random Rng => _rng;

        public readonly float PavementBackZ;
        public readonly float KerbZ;
        public readonly float RoadWidth;
        public readonly float FarPavementZ;
        public readonly float HalfLength;
        public readonly float ShopHalfWidth;
        public readonly float CityHalfWidth;
        public readonly float BlockWidth;
        public readonly int   CityRows;

        private readonly List<GameObject> _props;
        private readonly List<GameObject> _barriers;
        private readonly System.Random _rng;

        public StreetBuildContext(StreetGenerator street, Transform streetRoot, System.Random rng,
                                  List<GameObject> props, List<GameObject> barriers)
        {
            StreetRoot    = streetRoot;
            _rng          = rng;
            _props        = props;
            _barriers     = barriers;

            PavementBackZ = street.PavementBackZ;
            KerbZ         = street.KerbZ;
            RoadWidth     = street.RoadWidth;
            FarPavementZ  = street.FarPavementZ;
            HalfLength    = street.HalfLength;
            ShopHalfWidth = street.ShopHalfWidth;
            CityHalfWidth = street.CityHalfWidth;
            BlockWidth    = street.BlockWidth;
            CityRows      = street.CityRows;
        }

        public void AddBlockingCollider(Vector3 centre, Vector3 size, string name = "Blocker")
        {
            var go = new GameObject(name);
            go.transform.SetParent(StreetRoot, false);
            go.transform.position = centre;
            go.AddComponent<BoxCollider>().size = size;
            _barriers.Add(go);
        }

        // ── Utilities ───────────────────────────────────────────────────────────

        public void Place(GameObject go, Vector3 position, bool collider = true)
        {
            go.transform.SetParent(StreetRoot, false);
            go.transform.position = position;
            if (!collider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
            _props.Add(go);
        }

        public void Track(GameObject go)
        {
            if (go != null) _props.Add(go);
        }

        public float Random(float min, float max) => (float)(_rng.NextDouble() * (max - min) + min);
    }
}
