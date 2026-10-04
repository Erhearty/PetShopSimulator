using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Shop
{
    /// <summary>
    /// Maps each <see cref="BuildCatalog"/> id to its baked furniture prefab
    /// (Assets/Prefabs/Furniture/&lt;id&gt;.prefab). Referenced from the scene's
    /// <see cref="ShopLayout"/> and handed to <see cref="FurnitureFactory.Prefabs"/> at boot.
    /// </summary>
    [CreateAssetMenu(menuName = "PetShop/Furniture Prefabs", fileName = "FurniturePrefabs")]
    public class FurniturePrefabs : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("BuildCatalog id, e.g. 'shelf_small'.")]
            public string     Id;
            public GameObject Prefab;
        }

        public List<Entry> Entries = new();

        /// <summary>
        /// The prefab for a catalogue id, or null when none is assigned. Per-species pens
        /// without their own entry share the <see cref="BuildCatalog.PetPen"/> prefab.
        /// </summary>
        public GameObject Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var e in Entries)
                if (e.Id == id) return e.Prefab;
            return id.StartsWith(BuildCatalog.SpeciesPenPrefix) ? Get(BuildCatalog.PetPen) : null;
        }
    }
}
