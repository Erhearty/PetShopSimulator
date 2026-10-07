using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PetShop.Core;
using PetShop.Pets;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// The layout migration's homeless-pen rule on save data alone: a pen with pets pushed off the yard, with no
    /// free spot and no room elsewhere, goes back to its unshifted cells only when they are a valid pen spot;
    /// otherwise its pets are sold back and the pen is packed.
    /// </summary>
    public class SaveLayoutMigrationPensTests
    {
        private const float StartBalance = 100f;
        private const int   StrayPets    = 2;
        private static readonly Vector2Int ExposedPenOldCell = new(6, 6);
        private static readonly Vector2Int FullPenCell       = new(12, -8);

        private GameObject _go;
        private ShopLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _go     = new GameObject("Layout");
            _layout = _go.AddComponent<ShopLayout>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void HomelessPenWithBlockedOldCells_SellsBackPetsAndPacksPen()
        {
            var data = Fixture(blockOldCells: true, out var pen);
            var strays = pen.pets.ToList();
            float value = strays.Sum(p => Value(p));
            var report = new LayoutMigrationReport();

            SaveLayoutMigration.Apply(data, _layout, report);

            Assert.IsFalse(data.PlacedObjects.Contains(pen), "The pen with no valid spot should not be placed.");
            Assert.IsTrue(report.Evicted.Contains(pen), "The emptied pen should be packed.");
            CollectionAssert.AreEquivalent(strays.Select(p => p.id), report.RefundedPets.Select(p => p.Id));
            CollectionAssert.AreEquivalent(strays.Select(p => p.petName), report.RefundedPets.Select(p => p.Name));
            Assert.AreEqual(value, report.RefundTotal, 0.01f);
            Assert.AreEqual(StartBalance + value, data.Balance, 0.01f);
            Assert.IsTrue(report.HasChanges);
            string notice = report.RefundNotice();
            StringAssert.StartsWith(LayoutMigrationReport.RefundNoticePrefix, notice);
            foreach (var p in strays) StringAssert.Contains(p.petName, notice);
            StringAssert.Contains($"€{value:N2}", notice);
            Assert.AreEqual(1, data.PackedFurniture.Where(e => e.id == BuildCatalog.PetPen).Sum(e => e.qty));
        }

        [Test]
        public void PenWithAFreeSpot_KeepsItsPetsAndRefundsNothing()
        {
            var data = Fixture(blockOldCells: false, out var pen);
            var report = new LayoutMigrationReport();

            SaveLayoutMigration.Apply(data, _layout, report);

            Assert.IsTrue(data.PlacedObjects.Contains(pen));
            Assert.AreEqual(StrayPets, pen.pets.Count);
            Assert.IsEmpty(report.RefundedPets);
            Assert.AreEqual(StartBalance, data.Balance, 0.001f);
            Assert.AreEqual(string.Empty, report.RefundNotice());
        }

        /// <summary>
        /// A layout-0 save: a full rabbit pen, a fence on every yard pen spot (all but the exposed pen's old cells
        /// when <paramref name="blockOldCells"/> is false), and the occupied pen the shift pushes off the yard.
        /// </summary>
        private SaveData Fixture(bool blockOldCells, out SaveData.PlacedItem pen)
        {
            var data = new SaveData { Balance = StartBalance, layoutVersion = 0 };
            var full = Pen(FullPenCell);
            for (int i = 0; i < BuildCatalog.PenCapacity; i++) full.pets.Add(NamedPet($"Full{i}"));
            data.PlacedObjects.Add(full);

            var oldCells = new RectInt(ExposedPenOldCell, BuildCatalog.Get(BuildCatalog.PetPen).Size);
            RectInt yard = _layout.LotStageCells(ShopLayout.FullYardLotStage);
            RectInt ring = _layout.RoomWallCells();
            var fullCells = new RectInt(FullPenCell, BuildCatalog.Get(BuildCatalog.PetPen).Size);
            for (int x = yard.xMin; x < yard.xMax; x += 2)
            for (int y = yard.yMin; y < yard.yMax; y += 2)
            {
                var cell = new RectInt(x, y, 1, 1);
                if (ring.Overlaps(cell) || _layout.IsInsideShop(cell.position, cell.size) ||
                    _layout.OverlapsDoorway(cell) || fullCells.Overlaps(cell)) continue;
                if (!blockOldCells && oldCells.Overlaps(cell)) continue;
                data.PlacedObjects.Add(new SaveData.PlacedItem { catalogId = BuildCatalog.Fence, cellX = x, cellY = y });
            }
            // Back to layout-0 cells; the migration shifts them forward again.
            foreach (var item in data.PlacedObjects)
            {
                item.cellX -= ShopLayout.LegacyLayoutCellShift.x;
                item.cellY -= ShopLayout.LegacyLayoutCellShift.y;
            }

            pen = Pen(ExposedPenOldCell);
            for (int i = 0; i < StrayPets; i++) pen.pets.Add(NamedPet($"Stray{i}"));
            data.PlacedObjects.Add(pen);
            return data;
        }

        private static SaveData.PlacedItem Pen(Vector2Int cell) => new()
        {
            catalogId = BuildCatalog.PetPen, variant = Pet.Species.Rabbit.ToString(), cellX = cell.x, cellY = cell.y,
        };

        private static SaveData.PetSaveData NamedPet(string name)
        {
            var pet   = BreedingSystem.GenerateRandom(Pet.Species.Rabbit);
            var saved = SaveSystem.PetToSaveData(pet);
            saved.petName = name;
            Object.DestroyImmediate(pet);
            return saved;
        }

        private static float Value(SaveData.PetSaveData saved)
        {
            var pet = SaveSystem.SaveDataToPet(saved);
            float value = pet.SellPrice();
            Object.DestroyImmediate(pet);
            return value;
        }
    }
}
