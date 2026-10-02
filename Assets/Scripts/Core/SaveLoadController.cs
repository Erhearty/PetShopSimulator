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

        /// <summary>Creates the controller for <paramref name="game"/>; nothing is read yet.</summary>
        public SaveLoadController(GameManager game)
        {
            _game = game;
        }

        // ── New game / layout ─────────────────────────────────────────────────

        public void NewGame()
        {
            _game.Show.Withdraw();
            SaveReorder.ResetToDefaults(_game.AutoReorder);
            foreach (var p in _game.Layout.StarterLayout(_game.Grid))
            {
                var def = BuildCatalog.Get(p.CatalogId);
                var go  = _game.Build.Place(p.Cell, def, p.Variant, p.Rotation, charge: false);
                if (go == null) continue;

                var shelf = go.GetComponent<ShelfUnit>();
                if (shelf != null) SeedShelf(shelf);

                var pen = go.GetComponent<PetPen>();
                if (pen != null)
                {
                    pen.AddPet(BreedingSystem.GenerateRandom(pen.PenSpecies));
                    pen.AddPet(BreedingSystem.GenerateRandom(pen.PenSpecies));
                }
            }
            // You inherit one member of staff — without anyone on the till a new shop cannot
            // trade at all while you are out in the yard. Inherited, so no sign-on fee.
            var inherited = StaffCandidate.Generate();
            inherited.SignOnFee = 0f;
            inherited.Role      = StaffRole.Cashier;
            _game.HireCandidate(inherited);
            _game.Notify($"Welcome to your pet shop! {InputBindings.Label(GameAction.BuildMode)} to build, " +
                         $"{InputBindings.Label(GameAction.Interact)} to interact, " +
                         $"{InputBindings.Label(GameAction.EndDay)} to close up.");
        }

        /// <summary>Stocks a fresh shelf for free — the starter inventory.</summary>
        public void SeedShelf(ShelfUnit shelf)
        {
            var catalog = _game.Catalog;
            if (catalog == null) return;
            var products = catalog.GetByCategory(shelf.Category);
            for (int i = 0; i < products.Count && i < shelf.MaxLines; i++)
                shelf.AddStock(products[i], shelf.MaxPerLine);
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
            data.ShowEntryPetId = _game.Show.EntryPetId;
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

            foreach (var item in data.PlacedObjects)
            {
                var def = BuildCatalog.Get(item.catalogId);
                if (def == null) continue;

                // Saves from before lot stages could build anywhere in the yard; keep the
                // ground under each saved piece so it is not silently dropped on load.
                var cell = new Vector2Int(item.cellX, item.cellY);
                _game.Build.GridManager.EnsureFloor(cell, BuildMode.FootprintSize(def, item.footprintRotated));

                var go = _game.Build.Place(cell, def,
                                           item.variant, item.rotation, charge: false,
                                           footprintRotated: item.footprintRotated);
                if (go == null) continue;

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
            SaveLineage.Apply(data, loadedPets, data.Day);
            SaveReorder.Apply(data, _game.AutoReorder);
            RestoreShowEntry(data.ShowEntryPetId, loadedPets);

            _game.RestoreStaff(data);
            shop.SetPriceMultiplier(data.PriceMultiplier <= 0f ? 1f : data.PriceMultiplier);

            _game.Notify($"Save loaded — day {data.Day}, €{data.Balance:N0}");
        }
    }
}
