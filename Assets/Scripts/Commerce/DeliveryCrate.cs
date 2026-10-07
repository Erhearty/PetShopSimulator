using UnityEngine;
using PetShop.Core;
using PetShop.Shop;
using PetShop.UI;

namespace PetShop.Commerce
{
    /// <summary>
    /// A pallet of ordered stock dropped on the forecourt by the wholesaler. The player
    /// walks out, presses E, and the units go into the stockroom ready to go on shelves.
    /// A crate can instead carry one ordered piece of furniture (see <see cref="SpawnFurniture"/>),
    /// which E unpacks into the furniture inventory.
    ///
    /// Deliberately a physical object rather than an instant credit: fetching the delivery
    /// is what gives the forecourt a job and what makes ordering early worth doing.
    /// </summary>
    public class DeliveryCrate : MonoBehaviour
    {
        public ProductCategory Category;
        public int             Units;

        /// <summary>Edge of one furniture crate, in metres — bigger than a stock box.</summary>
        private const float FurnitureCrateSize = 0.9f;
        private const float LabelHeight        = 1.32f;
        private const float LabelFontSize      = 0.09f;
        private const float LabelWidth         = 1.7f;
        private static readonly Color CrateColour = new Color(0.66f, 0.49f, 0.29f);

        private WorldLabel _label;

        /// <summary>The furniture order this crate carries, or null for a stock pallet.</summary>
        public FurnitureOrder FurnitureOrder { get; private set; }

        /// <summary>True when this crate carries furniture rather than stock.</summary>
        public bool IsFurniture => FurnitureOrder != null;

        /// <summary>Display name of the furniture inside, or null for a stock pallet.</summary>
        public string FurnitureName =>
            IsFurniture ? BuildCatalog.Get(FurnitureOrder.CatalogId)?.DisplayName ?? FurnitureOrder.CatalogId : null;

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
            var mat   = MaterialFactory.Get("delivery_crate", CrateColour);

            for (int i = 0; i < boxes; i++)
            {
                float bx = (i % 2 == 0 ? -0.24f : 0.24f);
                float by = (i / 2) * 0.46f;
                var box = MeshBuilder.CreateBox(0.44f, 0.44f, 0.44f, mat, "Crate");
                box.transform.SetParent(root.transform, false);
                box.transform.localPosition    = new Vector3(bx, by, 0f);
                box.transform.localEulerAngles = new Vector3(0f, Random.Range(-9f, 9f), 0f);
            }

            crate.AttachLabel();
            SetLayerRecursive(root, GameLayers.Furniture);
            return crate;
        }

        /// <summary>
        /// Builds a single large crate holding one ordered piece of furniture. Null when
        /// <paramref name="order"/> is null.
        /// </summary>
        public static DeliveryCrate SpawnFurniture(Vector3 position, FurnitureOrder order)
        {
            if (order == null) return null;

            var root = new GameObject($"FurnitureDelivery_{order.CatalogId}");
            root.transform.position = position;

            var crate = root.AddComponent<DeliveryCrate>();
            crate.FurnitureOrder = order;

            var mat = MaterialFactory.Get("delivery_crate", CrateColour);
            var box = MeshBuilder.CreateBox(FurnitureCrateSize, FurnitureCrateSize, FurnitureCrateSize, mat, "Crate");
            box.transform.SetParent(root.transform, false);

            crate.AttachLabel();
            SetLayerRecursive(root, GameLayers.Furniture);
            return crate;
        }

        /// <summary>
        /// Puts the crate on the Furniture layer (what <see cref="GameLayers.InteractMask"/> casts against)
        /// and makes sure every box has a collider.
        /// </summary>
        private static void SetLayerRecursive(GameObject root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
                if (t.gameObject != root && t.GetComponent<MeshFilter>() != null && t.GetComponent<Collider>() == null)
                    t.gameObject.AddComponent<BoxCollider>();
            }
        }

        private void AttachLabel()
        {
            _label = WorldLabel.Create(transform, new Vector3(0f, LabelHeight, 0f),
                                       "", LabelFontSize, UIFactory.Accent, width: LabelWidth);
            RefreshLabel();
        }

        private void RefreshLabel() =>
            _label?.SetText(IsFurniture
                ? Localization.Loc.F("crate.label.furniture", FurnitureName, InputBindings.Label(GameAction.Interact))
                : Localization.Loc.F("crate.label.stock", Localization.LocNames.Category(Category), Units,
                                     InputBindings.Label(GameAction.Interact)));

        /// <summary>Moves the load into the stockroom and clears the forecourt. A furniture crate is left alone.</summary>
        public int Collect(ShopManager shop)
        {
            if (IsFurniture) return 0;
            if (shop != null) shop.AddToWarehouse(Category, Units);
            Destroy(gameObject);
            return Units;
        }

        /// <summary>
        /// Unpacks a furniture crate into <paramref name="supply"/>'s inventory and clears the
        /// forecourt. False when this is a stock pallet or the order could not be collected; a
        /// crate whose order is no longer pending (stale) is cleared away all the same.
        /// </summary>
        public bool CollectFurniture(FurnitureSupply supply)
        {
            if (!IsFurniture || supply == null) return false;
            bool collected = supply.Collect(FurnitureOrder);
            if (collected || !IsPending(supply, FurnitureOrder)) RemoveFromWorld();
            if (collected) GameManager.Instance?.Quests?.RaiseFlag(PetShop.Progression.Quests.QuestFlags.CrateCollected);
            return collected;
        }

        private static bool IsPending(FurnitureSupply supply, FurnitureOrder order)
        {
            foreach (var o in supply.Pending)
                if (o == order) return true;
            return false;
        }

        private void RemoveFromWorld()
        {
            if (Application.isPlaying) Destroy(gameObject);
            else                       DestroyImmediate(gameObject);
        }

        /// <summary>The interact prompt shown while the player looks at this crate.</summary>
        public string Prompt => IsFurniture
            ? Localization.Loc.F("crate.prompt.furniture", InputBindings.Label(GameAction.Interact), FurnitureName)
            : Localization.Loc.F("crate.prompt.stock", InputBindings.Label(GameAction.Interact), Units,
                                 Localization.LocNames.Category(Category));
    }
}

