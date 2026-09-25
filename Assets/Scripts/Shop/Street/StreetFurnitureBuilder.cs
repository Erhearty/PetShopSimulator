using UnityEngine;
using PetShop.Core;

namespace PetShop.Shop
{
    /// <summary>
    /// Dresses the street: lamp posts, traffic lights, trees, benches and other props along
    /// the pavements, and cars parked at both kerbs.
    /// </summary>
    internal class StreetFurnitureBuilder
    {
        private readonly StreetBuildContext _ctx;

        public StreetFurnitureBuilder(StreetBuildContext ctx)
        {
            _ctx = ctx;
        }

        // ── Street furniture ────────────────────────────────────────────────────

        public void BuildStreetFurniture()
        {
            float lampZ = _ctx.KerbZ - 1.4f;
            float treeZ = _ctx.KerbZ - 3.0f;

            for (float x = -_ctx.HalfLength + 8f; x < _ctx.HalfLength; x += 22f)
            {
                _ctx.Track(ModelLibrary.Spawn(StreetModels.LampPost, _ctx.StreetRoot, new Vector3(x, 0f, lampZ),
                                         0f, ModelLibrary.Fit.Height, 5.5f));
                _ctx.Track(ModelLibrary.Spawn(StreetModels.LampPost, _ctx.StreetRoot,
                                         new Vector3(x + 11f, 0f, _ctx.KerbZ + _ctx.RoadWidth + 1.4f), 180f,
                                         ModelLibrary.Fit.Height, 5.5f));
            }

            // Traffic lights belong at the crossing and the ends of the street, nowhere else.
            foreach (float x in new[] { -9f, 9f })
                _ctx.Track(ModelLibrary.Spawn(StreetModels.TrafficLight, _ctx.StreetRoot, new Vector3(x, 0f, _ctx.KerbZ - 1f),
                                         x < 0f ? 0f : 180f, ModelLibrary.Fit.Height, 5.5f));

            // Trees, skipping the stretch directly in front of the shop window.
            for (float x = -_ctx.HalfLength + 14f; x < _ctx.HalfLength; x += _ctx.Random(11f, 17f))
            {
                if (Mathf.Abs(x) < 8f) continue;

                string model = ModelLibrary.AnyAvailable(_ctx.Rng, StreetModels.Trees);
                if (model == null) break;

                _ctx.Track(ModelLibrary.Spawn(model, _ctx.StreetRoot, new Vector3(x, 0f, treeZ),
                                         _ctx.Random(0f, 360f), ModelLibrary.Fit.Height, _ctx.Random(5f, 7.5f)));
                _ctx.AddBlockingCollider(new Vector3(x, 1f, treeZ), new Vector3(1f, 2f, 1f));
            }

            // The bench model runs along its Z axis, so turn it to sit along the pavement.
            Prop("Packs/Street/StreetProps/Bench/Bench_A",   ModelLibrary.Furniture + "benchCushion", 1.0f,
                 new[] { -21f, 19f }, (_ctx.PavementBackZ + _ctx.KerbZ) * 0.5f - 1.5f, 90f);
            Prop("Packs/Street/StreetProps/MailBox/MailBox", null, 1.3f,
                 new[] { -13f }, _ctx.KerbZ - 1.2f, 0f);
            Prop("Packs/Street/StreetProps/Hidrant/Hidrant", null, 0.9f,
                 new[] { 12f }, _ctx.KerbZ - 1.2f, 0f);
            Prop("Packs/Street/StreetProps/NewsBoard/NewsBoard", null, 1.7f,
                 new[] { -7.5f }, _ctx.KerbZ - 1.6f, 0f);
            Prop("Packs/City/Props/Props_Dustbin", ModelLibrary.Furniture + "trashcan", 1.0f,
                 new[] { -6.5f, 7f }, _ctx.KerbZ - 1.6f, 0f);
        }

        private void Prop(string preferred, string fallback, float size, float[] xs, float z, float yRot)
        {
            string model = ModelLibrary.FirstAvailable(preferred, fallback);
            if (model == null) return;
            foreach (float x in xs)
                _ctx.Track(ModelLibrary.Spawn(model, _ctx.StreetRoot, new Vector3(x, 0f, z),
                                         yRot, ModelLibrary.Fit.Height, size));
        }

        public void BuildParkedCars()
        {
            foreach (var (z, heading) in new[] { (_ctx.KerbZ + 2.2f, 90f), (_ctx.KerbZ + _ctx.RoadWidth - 2.2f, 270f) })
            {
                for (float x = -_ctx.HalfLength + 10f; x < _ctx.HalfLength - 10f; x += _ctx.Random(11f, 26f))
                {
                    if (Mathf.Abs(x) < 7f) continue;   // keep the crossing clear

                    string model = ModelLibrary.AnyAvailable(_ctx.Rng, StreetModels.Cars);
                    if (model == null) return;

                    _ctx.Track(ModelLibrary.Spawn(model, _ctx.StreetRoot, new Vector3(x, 0f, z),
                                             heading, ModelLibrary.Fit.Depth, _ctx.Random(4.3f, 5.4f)));
                }
            }
        }
    }
}
