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
    public partial class SaveData
    {
        public int    Version = SaveMigrator.CurrentVersion;
        /// <summary>Permanent reputation tier reached (see ProgressionRules).</summary>
        public int    ProgressionTier;
        public float  Balance;
        public float  Reputation;
        public int    Day;
        public int    Staff = 1;
        public float  PriceMultiplier = 1f;
        public string SavedAt;
        /// <summary>Id of the pet entered in the pet show, or empty for none.</summary>
        public string ShowEntryPetId;
        /// <summary>Seed for the seasonal event rolls; 0 in older saves (a fresh one is picked).</summary>
        public int    EventSeed;
        /// <summary>Name of the running ShopEventKind; "None" when no event is running.</summary>
        public string ActiveEventId = "None";
        /// <summary>Days the running seasonal event still has to go.</summary>
        public int    EventDaysLeft;

        public List<StockEntry>   Stock         = new();
        /// <summary>Delivered units still in the stockroom, keyed by category name.</summary>
        public List<StockEntry>   Warehouse     = new();
        public List<PlacedItem>   PlacedObjects = new();
        /// <summary>Everyone on staff (save version 4+). Empty in older saves; see <see cref="Staff"/>.</summary>
        public List<StaffSaveData> StaffList    = new();

        /// <summary>One assistant as saved: who they are, their job, skill, wage and till speed.</summary>
        [Serializable]
        public class StaffSaveData
        {
            public string name;
            public string role;
            public int    skill;
            public float  wage;
            public float  serviceSeconds;
        }
        /// <summary>Every animal ever registered in the lineage registry.</summary>
        public List<LineageEntrySave> Lineage   = new();
        /// <summary>Auto-reorder rule per product category.</summary>
        public List<ReorderRuleSave> AutoReorder = new();
        /// <summary>Unplaced furniture the player owns, keyed by catalogue id (save version 5+).</summary>
        public List<StockEntry> FurnitureInventory = new();
        /// <summary>Furniture paid for but not yet collected from the forecourt (save version 5+).</summary>
        public List<FurnitureOrderSave> PendingFurnitureOrders = new();
        /// <summary>
        /// How many of each inventory id were packed away after being placed rather than delivered
        /// fresh; a subset of <see cref="FurnitureInventory"/>. Absent (empty) in older saves.
        /// </summary>
        public List<StockEntry> PackedFurniture = new();
        /// <summary>
        /// True once the shop room's walls have been laid as placed pieces; they then live in
        /// <see cref="PlacedObjects"/>. Older saves read false and get the walls laid once on load.
        /// </summary>
        public bool roomWallsSeeded;

        /// <summary>One furniture order as saved: what it is, when it lands, and whether it has.</summary>
        [Serializable]
        public class FurnitureOrderSave
        {
            public string catalogId;
            public float  arrivalProgress;
            public bool   arrived;
        }

        [Serializable]
        public class StockEntry { public string id; public int qty; }

        [Serializable]
        public class PlacedItem
        {
            public string catalogId;
            public int    cellX, cellY;
            public string variant;
            public float  rotation;
            /// <summary>True for a piece placed at a free world position (<see cref="posX"/>, <see cref="posZ"/>) and yaw.</summary>
            public bool   free;
            public float  posX, posZ;
            /// <summary>True when a hand-placed item occupies its catalogue footprint with x/y swapped.</summary>
            public bool   footprintRotated;
            public List<StockEntry>  shelfStock = new();
            public List<PetSaveData> pets       = new();
        }

        [Serializable]
        public class LineageEntrySave
        {
            public string id, petName, species, rarity, parentAId, parentBId;
            public float  coat_r, coat_g, coat_b;
            public int    generation, bornDay;
        }

        [Serializable]
        public class PetSaveData
        {
            public string id, parentAId, parentBId;
            public int    generation;
            public string species, petName, growthStage, rarity;
            public float  coat_r, coat_g, coat_b;
            public float  temperament, energyLevel, friendliness;
            public int    ageDays, daysToMature;
            public float  basePrice;
            public int    ribbons;
            public bool   Locked;
        }
    }

    /// <summary>What the title screen shows about one save slot, read without loading the game.</summary>
    public sealed class SlotSummary
    {
        /// <summary>The day the saved shop is on.</summary>
        public int Day;

        /// <summary>The saved shop's cash balance.</summary>
        public float Balance;

        /// <summary>When the save was written, as the ISO-8601 UTC string stored in the file.</summary>
        public string SavedAt;
    }

    /// <summary>
    /// Reads and writes saves as JSON under the persistent data folder. Saves live in
    /// <see cref="SlotCount"/> numbered slots; the parameterless calls act on <see cref="ActiveSlot"/>.
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>Number of save slots offered to the player.</summary>
        public const int SlotCount = 3;

        /// <summary>The slot used when none has been chosen.</summary>
        private const int DefaultSlot = 1;

        /// <summary>File name of the single save written before slots existed.</summary>
        private const string LegacyFileName = "petshop_save.json";

        /// <summary>Slot file name pattern; {0} is the slot number.</summary>
        private const string SlotFileFormat = "petshop_save_{0}.json";

        /// <summary>Suffix of the scratch file a save is written to before being swapped in.</summary>
        private const string TempSuffix = ".tmp";

        /// <summary>Suffix of the copy of the previous save kept after a successful swap.</summary>
        private const string BackupSuffix = ".bak";

        private static int _activeSlot = DefaultSlot;

        /// <summary>
        /// When non-empty (set by <c>-savepath</c>), replaces the default save slot so a
        /// playtest never touches the player's real save.
        /// </summary>
        public static string PathOverride;


        /// <summary>Folder the saves live in. Tests point this at a temp directory; null means the real folder.</summary>
        internal static string RootOverride;

        /// <summary>Folder the saves live in: <see cref="RootOverride"/> or Application.persistentDataPath.</summary>
        public static string Root => RootOverride ?? Application.persistentDataPath;

        /// <summary>The slot the parameterless Save/Load/Delete act on, clamped to 1..<see cref="SlotCount"/>.</summary>
        public static int ActiveSlot
        {
            get => _activeSlot;
            set => _activeSlot = Mathf.Clamp(value, DefaultSlot, SlotCount);
        }

        /// <summary>Path of the main save file for <paramref name="slot"/> (clamped to a valid slot); the default slot honours <see cref="PathOverride"/>.</summary>
        public static string SlotPath(int slot)
        {
            slot = Mathf.Clamp(slot, DefaultSlot, SlotCount);
            if (slot == DefaultSlot && !string.IsNullOrEmpty(PathOverride)) return PathOverride;
            return Path.Combine(Root, string.Format(SlotFileFormat, slot));
        }

        /// <summary>Path of the active slot's main save file.</summary>
        public static string SavePath => SlotPath(ActiveSlot);

        /// <summary>Path of the pre-slots save file.</summary>
        private static string LegacyPath => Path.Combine(Root, LegacyFileName);

        /// <summary>True when any slot has a main save or a backup to fall back on.</summary>
        public static bool HasSave()
        {
            for (int slot = DefaultSlot; slot <= SlotCount; slot++)
                if (HasSave(slot)) return true;
            return false;
        }

        /// <summary>True when <paramref name="slot"/> has a main save or a backup.</summary>
        public static bool HasSave(int slot) => HasSave(SlotPath(slot));

        /// <summary>True when <paramref name="path"/> or its backup exists.</summary>
        public static bool HasSave(string path) =>
            File.Exists(path) || File.Exists(path + BackupSuffix);

        /// <summary>Writes <paramref name="data"/> to the active save slot.</summary>
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

        /// <summary>Reads the active save slot; null when absent, unreadable or unmigratable.</summary>
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

        /// <summary>Removes the active save slot together with its backup and temp files.</summary>
        public static void Delete() => Delete(SavePath);

        /// <summary>Removes <paramref name="path"/> and its backup and temp files, where present.</summary>
        public static void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + BackupSuffix)) File.Delete(path + BackupSuffix);
            if (File.Exists(path + TempSuffix)) File.Delete(path + TempSuffix);
        }

        // ── Slots ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Day, balance and save time of <paramref name="slot"/>, read (with backup fallback and
        /// in-memory migration) without touching any file. Null when the slot is empty or unreadable.
        /// </summary>
        public static SlotSummary Peek(int slot)
        {
            if (!HasSave(slot)) return null;
            var data = Load(SlotPath(slot));
            if (data == null) return null;
            return new SlotSummary { Day = data.Day, Balance = data.Balance, SavedAt = data.SavedAt };
        }

        /// <summary>
        /// Moves a save written before slots existed (and its backup) into slot 1, unless slot 1
        /// already holds a save. Safe to call repeatedly.
        /// </summary>
        /// <returns>True when a legacy file was moved.</returns>
        public static bool MigrateLegacy()
        {
            string legacy = LegacyPath;
            if (!HasSave(legacy) || HasSave(DefaultSlot)) return false;
            string target = SlotPath(DefaultSlot);
            MoveIfPresent(legacy, target);
            MoveIfPresent(legacy + BackupSuffix, target + BackupSuffix);
            Debug.Log($"[SaveSystem] Moved legacy save {legacy} into slot {DefaultSlot}.");
            return true;
        }

        /// <summary>Moves <paramref name="from"/> to <paramref name="to"/> when the source exists.</summary>
        private static void MoveIfPresent(string from, string to)
        {
            if (File.Exists(from)) File.Move(from, to);
        }

        // ── Pet serialisation ───────────────────────────────────────────────────

        public static SaveData.PetSaveData PetToSaveData(Pet p) => new()
        {
            id           = p.id,
            parentAId    = p.parentAId,
            parentBId    = p.parentBId,
            generation   = p.generation,
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
            ribbons      = p.ribbons,
            Locked       = p.Locked,
        };

        public static Pet SaveDataToPet(SaveData.PetSaveData d)
        {
            var pet = ScriptableObject.CreateInstance<Pet>();
            Enum.TryParse(d.species,     out pet.species);
            Enum.TryParse(d.growthStage, out pet.growthStage);
            Enum.TryParse(d.rarity,      out pet.rarity);
            pet.id           = string.IsNullOrEmpty(d.id) ? BreedingSystem.NewId() : d.id;
            pet.parentAId    = d.parentAId;
            pet.parentBId    = d.parentBId;
            pet.generation   = d.generation;
            pet.petName      = d.petName;
            pet.name         = $"{pet.species}_{d.petName}";
            pet.coat         = new Color(d.coat_r, d.coat_g, d.coat_b);
            pet.temperament  = d.temperament;
            pet.energyLevel  = d.energyLevel;
            pet.friendliness = d.friendliness;
            pet.ageDays      = d.ageDays;
            pet.daysToMature = Mathf.Max(1, d.daysToMature);
            pet.basePrice    = d.basePrice > 0f ? d.basePrice : Pet.SpeciesBasePrice(pet.species);
            pet.ribbons      = Mathf.Max(0, d.ribbons);
            pet.Locked       = d.Locked;
            return pet;
        }
    }
}
