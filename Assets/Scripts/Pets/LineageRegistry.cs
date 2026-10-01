using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Pets
{
    /// <summary>One remembered animal. Survives after the pet is sold or dropped.</summary>
    [Serializable]
    public class LineageEntry
    {
        public string id;
        public string petName;
        public Pet.Species species;
        public Pet.Rarity rarity;
        public Color coat;
        public string parentAId;
        public string parentBId;
        public int generation;
        public int bornDay;
    }

    /// <summary>
    /// Static record of every pet ever bred or generated, keyed by id, so family trees
    /// can still be drawn for animals that have left the shop.
    /// </summary>
    public static class LineageRegistry
    {
        private static readonly List<LineageEntry> List = new List<LineageEntry>();
        private static readonly Dictionary<string, LineageEntry> ById = new Dictionary<string, LineageEntry>();

        /// <summary>A snapshot copy of all entries, in registration order.</summary>
        public static IReadOnlyList<LineageEntry> Entries => new List<LineageEntry>(List);

        /// <summary>Forget everything (new game / load).</summary>
        public static void Reset()
        {
            List.Clear();
            ById.Clear();
        }

        /// <summary>Record a pet (by its id) as born on <paramref name="day"/>. Re-registering replaces the entry.</summary>
        public static LineageEntry Register(Pet pet, int day)
        {
            if (pet == null || string.IsNullOrEmpty(pet.id)) return null;
            var entry = new LineageEntry
            {
                id = pet.id, petName = pet.petName, species = pet.species, rarity = pet.rarity,
                coat = pet.coat, parentAId = pet.parentAId, parentBId = pet.parentBId,
                generation = pet.generation, bornDay = day,
            };
            Add(entry);
            return entry;
        }

        /// <summary>Insert an already-built entry (used when loading a save).</summary>
        public static void Add(LineageEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id)) return;
            if (ById.TryGetValue(entry.id, out var old)) List.Remove(old);
            ById[entry.id] = entry;
            List.Add(entry);
        }

        /// <summary>The entry for <paramref name="id"/>, or null when unknown.</summary>
        public static LineageEntry Get(string id) =>
            !string.IsNullOrEmpty(id) && ById.TryGetValue(id, out var e) ? e : null;

        /// <summary>The known parents of <paramref name="id"/> (zero, one or two entries).</summary>
        public static IReadOnlyList<LineageEntry> Parents(string id)
        {
            var result = new List<LineageEntry>();
            var e = Get(id);
            if (e == null) return result;
            var a = Get(e.parentAId);
            var b = Get(e.parentBId);
            if (a != null) result.Add(a);
            if (b != null) result.Add(b);
            return result;
        }
    }
}
