using UnityEngine;
using PetShop.Shop;
using PetShop.Commerce;
using PetShop.Pets;

namespace PetShop.Core
{
    /// <summary>
    /// Starts a fresh shop, and snapshots or restores the shop to and from the save slot.
    /// Plain class owned by <see cref="GameManager"/>; every system (Shop, Grid, Build,
    /// Generator, Catalog) is read through the manager at call time, because the
    /// bootstrapper wires those fields after Awake.
    /// </summary>
    internal sealed class SaveLoadController
    {
        private readonly GameManager _game;
        /// <summary>True once the room walls have been laid as placed pieces in this game.</summary>
        private bool _roomWallsSeeded;

        /// <summary>Creates the controller for <paramref name="game"/>; nothing is read yet.</summary>
        public SaveLoadController(GameManager game)
        {
            _game = game;
        }

        // ── New game / layout ─────────────────────────────────────────────────

        /// <summary>
        /// Starts a fresh game with an empty shop: the player orders, collects and places every
        /// shelf, pen, counter and decoration from the furniture catalogue.
        /// <paramref name="skipTutorial"/> marks every tutorial quest done (no rewards).
        /// </summary>
        public void NewGame(bool skipTutorial = false)
        {
            _game.Show.Withdraw();
            SaveQuests.ResetForNewGame(_game, skipTutorial);
            _game.Furniture.Clear();
            SaveReorder.ResetToDefaults(_game.AutoReorder);
            SeedRoomWallsOnce(false);
            _game.Notify($"Welcome to your pet shop! {InputBindings.Label(GameAction.BuildMode)} to build, " +
                         $"{InputBindings.Label(GameAction.Interact)} to interact, " +
                         $"{InputBindings.Label(GameAction.EndDay)} to close up.");
        }

        /// <summary>
        /// Lays the shop room's walls as placed pieces unless <paramref name="alreadySeeded"/> says this
        /// game already has them, then records that it does. Walls the player removed stay removed.
        /// </summary>
        private void SeedRoomWallsOnce(bool alreadySeeded)
        {
            _roomWallsSeeded = alreadySeeded;
            if (_roomWallsSeeded || _game.Layout == null || _game.Build == null) return;
            _game.Layout.SeedRoomWalls(_game.Build);
            _roomWallsSeeded = true;
        }

        // ── Save / load ───────────────────────────────────────────────────────

        /// <summary>
        /// Snapshots the shop and writes it to the save slot, notifying the player of the outcome.
        /// </summary>
        /// <param name="quiet">True for an automatic save: the success toast reads "Autosaved.".</param>
        /// <returns>True when the save was written; false when it failed.</returns>
        public bool SaveGame(bool quiet = false)
        {
            if (SaveSystem.Save(BuildSaveData()))
            {
                _game.Notify(quiet ? "Autosaved." : "Game saved.");
                return true;
            }
            _game.Notify("Save FAILED — progress not written. Check disk space/permissions.");
            return false;
        }

        /// <summary>Captures money, stock, warehouse and every placed object into a SaveData.</summary>
        private SaveData BuildSaveData()
        {
            var shop = _game.Shop;
            var data = new SaveData
            {
                Balance         = shop.Balance,
                Reputation      = shop.Reputation,
                Day             = shop.Day,
                Staff           = _game.StaffCount,
                StaffList       = _game.StaffToSave(),
                PriceMultiplier = shop.PriceMultiplier,
                ProgressionTier = _game.Progression?.Tier ?? 0,
                roomWallsSeeded = _roomWallsSeeded,
                layoutVersion   = ShopLayout.CurrentLayoutVersion,
                // Every game this build saves has its back door: seeded, migrated, or left as the player edited it.
                backDoorSeeded  = true,
            };
            _game.Events?.Capture(data);

            foreach (var kvp in shop.Stock)
                data.Stock.Add(new SaveData.StockEntry { id = kvp.Key, qty = kvp.Value });

            foreach (ProductCategory category in System.Enum.GetValues(typeof(ProductCategory)))
            {
                int units = shop.Warehouse(category);
                if (units > 0)
                    data.Warehouse.Add(new SaveData.StockEntry { id = category.ToString(), qty = units });
            }

            foreach (var entry in _game.Grid.GetAllPlaced())
                if (entry.Data != null)
                    data.PlacedObjects.Add(BuildPlacedItem(entry));

            SaveLineage.Capture(data);
            SaveReorder.Capture(data, _game.AutoReorder);
            SaveFurniture.Capture(data, _game.Furniture, _game.Build);
            data.ShowEntryPetId = _game.Show.EntryPetId;
            SaveQuests.Capture(data, _game);
            return data;
        }

        /// <summary>Serialises one grid entry, including shelf stock or pen residents.</summary>
        private static SaveData.PlacedItem BuildPlacedItem(GridEntry entry)
        {
            var item = new SaveData.PlacedItem
            {
                catalogId = entry.Data.Id,
                cellX     = entry.Root.x,
                cellY     = entry.Root.y,
                variant   = entry.Variant,
                rotation  = entry.Instance != null ? entry.Instance.transform.eulerAngles.y : 0f,
                footprintRotated = BuildMode.IsFootprintRotated(entry),
            };
            if (entry.Instance != null) AddInstanceContents(item, entry.Instance);
            return item;
        }

        /// <summary>Copies shelf stock or pen residents from a spawned object into its save entry.</summary>
        private static void AddInstanceContents(SaveData.PlacedItem item, GameObject instance)
        {
            var shelf = instance.GetComponent<ShelfUnit>();
            if (shelf != null)
            {
                item.variant = shelf.Category.ToString();
                foreach (var line in shelf.Lines)
                    if (line.Product != null)
                        item.shelfStock.Add(new SaveData.StockEntry { id = line.Product.id, qty = line.Units });
            }

            var pen = instance.GetComponent<PetPen>();
            if (pen != null)
            {
                item.variant = pen.PenSpecies.ToString();
                foreach (var pet in pen.Residents)
                    item.pets.Add(SaveSystem.PetToSaveData(pet));
            }
        }

        /// <summary>Re-enters the saved show pet if it was loaded; otherwise clears the entry.</summary>
        private void RestoreShowEntry(string petId, System.Collections.Generic.List<Pet> pets)
        {
            _game.Show.Withdraw();
            if (string.IsNullOrEmpty(petId)) return;
            var pet = pets.Find(p => p.id == petId);
            if (pet != null) _game.Show.Restore(pet);
        }

        public void LoadGame(SaveData data)
        {
            var shop = _game.Shop;
            shop.SetBalance(data.Balance);
            shop.SetReputation(data.Reputation);
            shop.SetDay(data.Day);

            foreach (var entry in data.Stock)
                shop.ChangeStock(entry.id, entry.qty);

            foreach (var entry in data.Warehouse)
                if (System.Enum.TryParse(entry.id, out ProductCategory category))
                    shop.AddToWarehouse(category, entry.qty);

            var loadedPets = new System.Collections.Generic.List<Pet>();

            // Unlocked lot stages must be buildable before saved furniture is placed on them.
            _game.Progression?.Restore(data.ProgressionTier);
            _game.Events?.Restore(data);

            var migration = RestorePlacedObjects(data, loadedPets);
            SaveLineage.Apply(data, loadedPets, data.Day);
            SaveReorder.Apply(data, _game.AutoReorder);
            SaveFurniture.Apply(data, _game.Furniture);
            RestoreShowEntry(data.ShowEntryPetId, loadedPets);
            SaveQuests.Apply(data, _game);

            _game.RestoreStaff(data);
            shop.SetPriceMultiplier(data.PriceMultiplier <= 0f ? 1f : data.PriceMultiplier);

            _game.Notify($"Save loaded — day {data.Day}, €{data.Balance:N0}");
            if (migration.HasChanges) _game.Notify(migration.Notice());
        }

        /// <summary>
        /// Moves an older layout's cells onto the current one (<see cref="SaveLayoutMigration"/>), places every
        /// saved piece, then lays the room walls if the save predates them. NavMesh bakes
        /// are suspended meanwhile, so the whole load costs one bake rather than one per wall.
        /// </summary>
        /// <returns>What the layout migration moved or packed, for the load's one combined notice.</returns>
        private LayoutMigrationReport RestorePlacedObjects(SaveData data, System.Collections.Generic.List<Pet> loadedPets)
        {
            var layout = _game.Layout;
            var migration = new LayoutMigrationReport();
            SaveLayoutMigration.Apply(data, layout, migration);
            foreach (var item in migration.Evicted) SettleEvicted(item);
            SaveLayoutMigration.SeedBackDoor(data, layout);
            layout?.SuspendNavMeshBakes();
            try
            {
                foreach (var item in data.PlacedObjects) RestorePlacedObject(item, loadedPets);
                // After the saved pieces, so a save's own walls (or furniture on the border) win.
                SeedRoomWallsOnce(data.roomWallsSeeded);
            }
            finally { layout?.ResumeNavMeshBakes(); }
            return migration;
        }

        /// <summary>
        /// Settles the contents of a piece the layout migration packed away instead of placing (it would have
        /// stood off the yard): shelf stock goes to the warehouse, as when the Remove tool packs a shelf. A packed
        /// pen is always empty: the migration relocates or empties an occupied pen rather than pack its pets.
        /// </summary>
        private void SettleEvicted(SaveData.PlacedItem item)
        {
            foreach (var line in item.shelfStock)
            {
                var product = _game.Catalog?.Get(line.id);
                if (product != null && line.qty > 0) _game.Shop.AddToWarehouse(product.category, line.qty);
            }
            Debug.LogWarning($"[SaveLoad] layout migration: action=pack id='{item.catalogId}' " +
                             $"cell=({item.cellX},{item.cellY}) reason=outside-yard shelfLines={item.shelfStock.Count}");
        }

        /// <summary>Places one saved piece and refills its shelf stock or pen residents.</summary>
        private void RestorePlacedObject(SaveData.PlacedItem item, System.Collections.Generic.List<Pet> loadedPets)
        {
            var def = BuildCatalog.Get(item.catalogId);
            if (def == null) return;

            // Saves from before lot stages could build anywhere in the yard; keep the
            // ground under each saved piece so it is not silently dropped on load.
            var cell = new Vector2Int(item.cellX, item.cellY);
            _game.Build.GridManager.EnsureFloor(cell, BuildMode.FootprintSize(def, item.footprintRotated));

            var go = _game.Build.Place(cell, def,
                                       item.variant, item.rotation, charge: false,
                                       footprintRotated: item.footprintRotated);
            if (go != null) RestoreContents(go, item, loadedPets);
        }

        /// <summary>Refills a loaded shelf's stock or a loaded pen's residents from its save entry.</summary>
        private void RestoreContents(GameObject go, SaveData.PlacedItem item, System.Collections.Generic.List<Pet> loadedPets)
        {
            var shelf = go.GetComponent<ShelfUnit>();
            if (shelf != null)
                foreach (var line in item.shelfStock)
                {
                    var product = _game.Catalog?.Get(line.id);
                    if (product != null) shelf.AddStock(product, line.qty);
                }

            var pen = go.GetComponent<PetPen>();
            if (pen != null)
                foreach (var petData in item.pets)
                {
                    var pet = SaveSystem.SaveDataToPet(petData);
                    loadedPets.Add(pet);
                    pen.AddPet(pet);
                }
        }
    }
}
