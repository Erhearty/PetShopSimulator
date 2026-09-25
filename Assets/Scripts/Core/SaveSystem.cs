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
        public int    Version = SaveMigrator.CurrentVersion;
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
        /// <summary>Suffix of the scratch file a save is written to before being swapped in.</summary>
        private const string TempSuffix = ".tmp";

        /// <summary>Suffix of the copy of the previous save kept after a successful swap.</summary>
        private const string BackupSuffix = ".bak";

        public static string SavePath =>
            Path.Combine(Application.persistentDataPath, "petshop_save.json");

        /// <summary>True when the default slot has a main save or a backup to fall back on.</summary>
        public static bool HasSave() => HasSave(SavePath);

        /// <summary>True when <paramref name="path"/> or its backup exists.</summary>
        public static bool HasSave(string path) =>
            File.Exists(path) || File.Exists(path + BackupSuffix);

        /// <summary>Writes <paramref name="data"/> to the default save slot.</summary>
        /// <returns>True when the file was written; false when the write failed (error is logged).</returns>
        public static bool Save(SaveData data) => Save(data, SavePath);

        /// <summary>
        /// Writes <paramref name="data"/> as JSON to <paramref name="path"/>, stamping SavedAt.
        /// The JSON goes to a temp file first and is then swapped in, keeping the previous save
        /// as a backup; a failed write leaves the existing save untouched.
        /// </summary>
        /// <returns>True when the file was written; false when serialisation or the write threw (error is logged).</returns>
        public static bool Save(SaveData data, string path)
        {
            string tmp = path + TempSuffix;
            try
            {
                data.SavedAt = DateTime.UtcNow.ToString("o");
                File.WriteAllText(tmp, JsonUtility.ToJson(data, prettyPrint: true));
                SwapIn(tmp, path);
                Debug.Log($"[SaveSystem] Saved to {path}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Save failed: {e.Message}");
                TryDeleteQuietly(tmp);
                return false;
            }
        }

        /// <summary>
        /// Moves the freshly written <paramref name="tmp"/> onto <paramref name="path"/>. When a save
        /// already exists it becomes the backup; falls back to copy/delete/move where File.Replace
        /// is unsupported or fails.
        /// </summary>
        private static void SwapIn(string tmp, string path)
        {
            if (!File.Exists(path))
            {
                File.Move(tmp, path);
                return;
            }
            string bak = path + BackupSuffix;
            try
            {
                File.Replace(tmp, path, bak);
            }
            catch (Exception e) when (e is NotSupportedException || e is IOException)
            {
                // Replace may have already moved main to .bak before failing.
                if (File.Exists(path))
                {
                    File.Copy(path, bak, overwrite: true);
                    File.Delete(path);
                }
                File.Move(tmp, path);
            }
        }

        /// <summary>Best-effort removal of a leftover file; any exception is swallowed.</summary>
        private static void TryDeleteQuietly(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
                // Best effort only — a stale temp file is overwritten by the next save.
            }
        }

        /// <summary>Reads the default save slot; null when absent, unreadable or unmigratable.</summary>
        public static SaveData Load() => Load(SavePath);

        /// <summary>
        /// Reads the save at <paramref name="path"/>, falling back to its backup when the main file
        /// is absent, unreadable or unmigratable. Older saves are migrated in memory via
        /// <see cref="SaveMigrator"/>. Null when neither yields a usable save.
        /// </summary>
        public static SaveData Load(string path)
        {
            var data = TryRead(path);
            if (data != null) return data;
            string bak = path + BackupSuffix;
            data = TryRead(bak);
            if (data != null)
                Debug.LogWarning($"[SaveSystem] Main save unusable — loaded backup {bak}.");
            return data;
        }

        /// <summary>
        /// Parses and migrates the save at <paramref name="path"/>; null when absent, unreadable or
        /// unmigratable. Migrated data is not written back.
        /// </summary>
        private static SaveData TryRead(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                return SaveMigrator.Migrate(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Load failed: {e.Message}");
                return null;
            }
        }

        /// <summary>Removes the default save slot together with its backup and temp files.</summary>
        public static void Delete() => Delete(SavePath);

        /// <summary>Removes <paramref name="path"/> and its backup and temp files, where present.</summary>
        public static void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + BackupSuffix)) File.Delete(path + BackupSuffix);
            if (File.Exists(path + TempSuffix)) File.Delete(path + TempSuffix);
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
