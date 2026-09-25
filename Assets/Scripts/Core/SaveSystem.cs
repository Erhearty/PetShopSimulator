using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PetShop.Pets;

namespace PetShop.Core
{
    /// <summary>
    /// The whole persisted game: money, reputation, day, and every placed piece of
    /// furniture together with its shelf stock or pen residents.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int    Version = 2;
        public float  Balance;
        public float  Reputation;
        public int    Day;
        public int    Staff = 1;
        public float  PriceMultiplier = 1f;
        public string SavedAt;

        public List<StockEntry>   Stock         = new();
        /// <summary>Delivered units still in the stockroom, keyed by category name.</summary>
        public List<StockEntry>   Warehouse     = new();
        public List<PlacedItem>   PlacedObjects = new();

        [Serializable]
        public class StockEntry { public string id; public int qty; }

        [Serializable]
        public class PlacedItem
        {
            public string catalogId;
            public int    cellX, cellY;
            public string variant;
            public float  rotation;
            public List<StockEntry>  shelfStock = new();
            public List<PetSaveData> pets       = new();
        }

        [Serializable]
        public class PetSaveData
        {
            public string species, petName, growthStage, rarity;
            public float  coat_r, coat_g, coat_b;
            public float  temperament, energyLevel, friendliness;
            public int    ageDays, daysToMature;
            public float  basePrice;
        }
    }

    public static class SaveSystem
    {
        public static string SavePath =>
            Path.Combine(Application.persistentDataPath, "petshop_save.json");

        public static bool HasSave() => File.Exists(SavePath);

        /// <summary>Writes <paramref name="data"/> to the default save slot.</summary>
        /// <returns>True when the file was written; false when the write failed (error is logged).</returns>
        public static bool Save(SaveData data) => Save(data, SavePath);

        /// <summary>Writes <paramref name="data"/> as JSON to <paramref name="path"/>, stamping SavedAt.</summary>
        /// <returns>True when the file was written; false when serialisation or the write threw (error is logged).</returns>
        public static bool Save(SaveData data, string path)
        {
            try
            {
                data.SavedAt = DateTime.UtcNow.ToString("o");
                File.WriteAllText(path, JsonUtility.ToJson(data, prettyPrint: true));
                Debug.Log($"[SaveSystem] Saved to {path}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Save failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Reads the default save slot; null when absent, unreadable or outdated.</summary>
        public static SaveData Load() => Load(SavePath);

        /// <summary>Reads the save at <paramref name="path"/>; null when absent, unreadable or outdated.</summary>
        internal static SaveData Load(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data == null || data.Version < 2)
                {
                    Debug.LogWarning("[SaveSystem] Save is from an older build — starting fresh.");
                    return null;
                }
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Load failed: {e.Message}");
                return null;
            }
        }

        public static void Delete()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }

        // ── Pet serialisation ───────────────────────────────────────────────────

        public static SaveData.PetSaveData PetToSaveData(Pet p) => new()
        {
            species      = p.species.ToString(),
            petName      = p.petName,
            growthStage  = p.growthStage.ToString(),
            rarity       = p.rarity.ToString(),
            coat_r       = p.coat.r,
            coat_g       = p.coat.g,
            coat_b       = p.coat.b,
            temperament  = p.temperament,
            energyLevel  = p.energyLevel,
            friendliness = p.friendliness,
            ageDays      = p.ageDays,
            daysToMature = p.daysToMature,
            basePrice    = p.basePrice,
        };

        public static Pet SaveDataToPet(SaveData.PetSaveData d)
        {
            var pet = ScriptableObject.CreateInstance<Pet>();
            Enum.TryParse(d.species,     out pet.species);
            Enum.TryParse(d.growthStage, out pet.growthStage);
            Enum.TryParse(d.rarity,      out pet.rarity);
            pet.petName      = d.petName;
            pet.name         = $"{pet.species}_{d.petName}";
            pet.coat         = new Color(d.coat_r, d.coat_g, d.coat_b);
            pet.temperament  = d.temperament;
            pet.energyLevel  = d.energyLevel;
            pet.friendliness = d.friendliness;
            pet.ageDays      = d.ageDays;
            pet.daysToMature = Mathf.Max(1, d.daysToMature);
            pet.basePrice    = d.basePrice > 0f ? d.basePrice : Pet.SpeciesBasePrice(pet.species);
            return pet;
        }
    }
}
