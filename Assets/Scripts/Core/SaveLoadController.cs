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
            foreach (var p in _game.Generator.StarterLayout(_game.Grid))
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
            _game.HireCandidate(inherited);
            _game.Notify("Welcome to your pet shop! B to build, E to interact, Enter to close up.");
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
        /// <returns>True when the save was written; false when it failed.</returns>
        public bool SaveGame()
        {
            if (SaveSystem.Save(BuildSaveData()))
            {
                _game.Notify("Game saved.");
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
                PriceMultiplier = shop.PriceMultiplier,
            };

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

            foreach (var item in data.PlacedObjects)
            {
                var def = BuildCatalog.Get(item.catalogId);
                if (def == null) continue;

                var go = _game.Build.Place(new Vector2Int(item.cellX, item.cellY), def,
                                           item.variant, item.rotation, charge: false);
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
                        pen.AddPet(SaveSystem.SaveDataToPet(petData));
            }

            for (int i = 0; i < Mathf.Max(1, data.Staff); i++) _game.HireAssistant();
            shop.SetPriceMultiplier(data.PriceMultiplier <= 0f ? 1f : data.PriceMultiplier);

            _game.Notify($"Save loaded — day {data.Day}, €{data.Balance:N0}");
        }
    }
}
