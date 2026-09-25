using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Core
{
    /// <summary>
    /// Upgrades save JSON written by older builds to the current <see cref="SaveData"/> layout by
    /// running a chain of single-version steps, each keyed by the version it migrates FROM.
    /// </summary>
    public static class SaveMigrator
    {
        /// <summary>The save format version written by this build.</summary>
        public const int CurrentVersion = 2;

        /// <summary>Saves written before the Version field existed; they parse as 0.</summary>
        private const int LegacyVersion = 0;

        /// <summary>The first versioned save format.</summary>
        private const int FirstVersion = 1;

        /// <summary>Default staff count applied when a save carries none.</summary>
        private const int DefaultStaff = 1;

        /// <summary>Default price multiplier applied when a save carries a non-positive one.</summary>
        private const float DefaultPriceMultiplier = 1f;

        /// <summary>
        /// Only the version stamp of a save. No initializer, so JSON without a Version field
        /// reads as <see cref="LegacyVersion"/>.
        /// </summary>
        [Serializable]
        private class SaveHeader
        {
            public int Version;
        }

        /// <summary>Migration steps keyed by the version they upgrade FROM (to FROM + 1).</summary>
        private static readonly Dictionary<int, Action<SaveData>> Steps = new()
        {
            { LegacyVersion, NormaliseDefaults },
            { FirstVersion,  NormaliseDefaults },
        };

        /// <summary>
        /// Parses <paramref name="json"/> and migrates it step by step to <see cref="CurrentVersion"/>.
        /// </summary>
        /// <returns>
        /// The migrated save; null when the JSON is malformed, from a newer build, or has no
        /// migration path (the reason is logged).
        /// </returns>
        public static SaveData Migrate(string json)
        {
            var header = ParseHeader(json);
            if (header == null) return null;
            if (header.Version > CurrentVersion)
            {
                Debug.LogWarning($"[SaveMigrator] Save version {header.Version} is from a newer build " +
                                 $"(current {CurrentVersion}) — cannot load.");
                return null;
            }
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return null;
            return ApplySteps(data, header.Version) ? data : null;
        }

        /// <summary>Reads just the version header; null (error logged) when the JSON is malformed.</summary>
        private static SaveHeader ParseHeader(string json)
        {
            try
            {
                var header = JsonUtility.FromJson<SaveHeader>(json);
                if (header == null) Debug.LogError("[SaveMigrator] Save is empty — cannot load.");
                return header;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveMigrator] Save is malformed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Runs every step from <paramref name="fromVersion"/> up to <see cref="CurrentVersion"/>,
        /// stamping <paramref name="data"/> after each. False (error logged) when a step is missing.
        /// </summary>
        private static bool ApplySteps(SaveData data, int fromVersion)
        {
            data.Version = fromVersion;
            for (int v = fromVersion; v < CurrentVersion; v++)
            {
                if (!Steps.TryGetValue(v, out var step))
                {
                    Debug.LogError($"[SaveMigrator] No migration from save version {v} — cannot load.");
                    return false;
                }
                step(data);
                data.Version = v + 1;
                Debug.Log($"[SaveMigrator] Migrated save from version {v} to {v + 1}.");
            }
            return true;
        }

        /// <summary>
        /// Fills in fields older saves may lack or hold invalid values for: staff, price multiplier,
        /// and every list (including each placed item's shelf stock and pets).
        /// </summary>
        private static void NormaliseDefaults(SaveData data)
        {
            if (data.Staff < DefaultStaff) data.Staff = DefaultStaff;
            if (data.PriceMultiplier <= 0f) data.PriceMultiplier = DefaultPriceMultiplier;
            data.Stock         ??= new List<SaveData.StockEntry>();
            data.Warehouse     ??= new List<SaveData.StockEntry>();
            data.PlacedObjects ??= new List<SaveData.PlacedItem>();
            foreach (var item in data.PlacedObjects)
            {
                if (item == null) continue;
                item.shelfStock ??= new List<SaveData.StockEntry>();
                item.pets       ??= new List<SaveData.PetSaveData>();
            }
        }
    }
}
