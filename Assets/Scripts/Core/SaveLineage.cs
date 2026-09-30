using System;
using System.Collections.Generic;
using UnityEngine;
using PetShop.Pets;

namespace PetShop.Core
{
    /// <summary>Converts the static <see cref="LineageRegistry"/> to and from save data.</summary>
    public static class SaveLineage
    {
        /// <summary>Copies every registry entry into <paramref name="data"/>.Lineage.</summary>
        public static void Capture(SaveData data)
        {
            data.Lineage.Clear();
            foreach (var e in LineageRegistry.Entries) data.Lineage.Add(ToSave(e));
        }

        /// <summary>
        /// Resets the registry, loads the saved entries, then registers any of
        /// <paramref name="pets"/> not yet present (old saves).
        /// </summary>
        public static void Apply(SaveData data, IEnumerable<Pet> pets, int day)
        {
            LineageRegistry.Reset();
            if (data?.Lineage != null)
                foreach (var s in data.Lineage) LineageRegistry.Add(FromSave(s));
            if (pets == null) return;
            foreach (var pet in pets)
                if (pet != null && LineageRegistry.Get(pet.id) == null) LineageRegistry.Register(pet, day);
        }

        public static SaveData.LineageEntrySave ToSave(LineageEntry e) => new()
        {
            id = e.id, petName = e.petName,
            species = e.species.ToString(), rarity = e.rarity.ToString(),
            coat_r = e.coat.r, coat_g = e.coat.g, coat_b = e.coat.b,
            parentAId = e.parentAId, parentBId = e.parentBId,
            generation = e.generation, bornDay = e.bornDay,
        };

        public static LineageEntry FromSave(SaveData.LineageEntrySave s)
        {
            var e = new LineageEntry
            {
                id = s.id, petName = s.petName,
                coat = new Color(s.coat_r, s.coat_g, s.coat_b),
                parentAId = s.parentAId, parentBId = s.parentBId,
                generation = s.generation, bornDay = s.bornDay,
            };
            Enum.TryParse(s.species, out e.species);
            Enum.TryParse(s.rarity, out e.rarity);
            return e;
        }
    }
}
