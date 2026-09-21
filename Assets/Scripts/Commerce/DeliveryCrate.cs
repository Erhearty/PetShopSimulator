using UnityEngine;
using PetShop.Core;
using PetShop.UI;

namespace PetShop.Commerce
{
    /// <summary>
    /// A pallet of ordered stock dropped on the forecourt by the wholesaler. The player
    /// walks out, presses E, and the units go into the stockroom ready to go on shelves.
    ///
    /// Deliberately a physical object rather than an instant credit: fetching the delivery
    /// is what gives the forecourt a job and what makes ordering early worth doing.
    /// </summary>
    public class DeliveryCrate : MonoBehaviour
    {
        public ProductCategory Category;
        public int             Units;

        private WorldLabel _label;

        /// <summary>Builds a crate stack sized to the order and labels it.</summary>
        public static DeliveryCrate Spawn(Vector3 position, ProductCategory category, int units)
        {
            var root = new GameObject($"Delivery_{category}");
            root.transform.position = position;

            var crate = root.AddComponent<DeliveryCrate>();
            crate.Category = category;
            crate.Units    = units;

            // One box per six units, stacked two to a row, so a big order looks like one.
            int boxes = Mathf.Clamp(Mathf.CeilToInt(units / 6f), 1, 6);
            var mat   = MaterialFactory.Get("delivery_crate", new Color(0.66f, 0.49f, 0.29f));

            for (int i = 0; i < boxes; i++)
            {
                float bx = (i % 2 == 0 ? -0.24f : 0.24f);
                float by = (i / 2) * 0.46f;
                var box = MeshBuilder.CreateBox(0.44f, 0.44f, 0.44f, mat, "Crate");
                box.transform.SetParent(root.transform, false);
                box.transform.localPosition    = new Vector3(bx, by, 0f);
                box.transform.localEulerAngles = new Vector3(0f, Random.Range(-9f, 9f), 0f);
            }

            crate._label = WorldLabel.Create(root.transform, new Vector3(0f, 1.32f, 0f),
                                             "", 0.09f, UIFactory.Accent, width: 1.7f);
            crate.RefreshLabel();
            return crate;
        }

        private void RefreshLabel() =>
            _label?.SetText($"delivery: {Category}\n<size=75%>{Units} units  ·  [E] to collect</size>");

        /// <summary>Moves the load into the stockroom and clears the forecourt.</summary>
        public int Collect(ShopManager shop)
        {
            if (shop != null) shop.AddToWarehouse(Category, Units);
            Destroy(gameObject);
            return Units;
        }

        public string Prompt => $"[E]  Collect {Units} {Category} units from the delivery";
    }
}
