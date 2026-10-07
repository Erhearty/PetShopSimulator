using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using PetShop.Core;
using PetShop.Shop;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// The room's back wall is nothing but its seeded grid pieces: no scene cladding or other static wall
    /// stands along the back ring row, so the Remove tool really opens it; a back door leads into the yard,
    /// and old saves get that door once without undoing a player's edit.
    /// </summary>
    public class RoomBackWallPlayTests
    {
        private const int   Seed      = 2718;
        private const float TimeScale = 1f;
        private const float DayLength = 900f;
        /// <summary>Height band (m) a wall occupies and floor, rugs and trims do not.</summary>
        private const float BandBottom = 1f;
        private const float BandTop    = 3f;
        /// <summary>Metres past the room's back floor edge the band reaches into the room.</summary>
        private const float IntoRoom   = 0.5f;
        /// <summary>Metres behind the back wall ring the yard must be walkable.</summary>
        private const float YardBehind = 3f;
        private const float NavSampleRadius = 1f;
        /// <summary>Metres the emptied-cell probe keeps clear of each neighbouring piece's touching edge.</summary>
        private const float NeighbourInset = 0.3f;
        /// <summary>Queue places, from the front, that must stand clear of the back door.</summary>
        private const int QueuePlacesChecked = 3;

        [TearDown]
        public void TearDown() => PlaytestHarness.Teardown();

        /// <summary>
        /// Every renderer or solid collider along the back ring row and back floor edge belongs to a placed grid
        /// piece or a corner filler; removing a back wall piece leaves nothing standing in its cell.
        /// </summary>
        [UnityTest]
        public IEnumerator BackWall_IsOnlySeededPieces_AndRemovable()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            RectInt ring = game.Layout.RoomWallCells();
            float cs = GridManager.CellSize;

            // Between the corner cells: the corners are side-wall pieces, and the yard's boundary wall runs past them.
            var row = Band((ring.xMin + 1) * cs, (ring.xMax - 1) * cs, ring.yMin * cs, (ring.yMin + 1) * cs + IntoRoom);
            var strays = Strays(game, row).ToList();
            Assert.IsEmpty(strays, "Non-grid walls along the back row: " + string.Join(", ", strays));

            RectInt backDoor = game.Layout.BackDoorRingCells();
            var cell = Enumerable.Range(ring.xMin + 1, ring.width - 2).Select(x => new Vector2Int(x, ring.yMin))
                                 .First(c => !backDoor.Contains(c));
            Assert.IsTrue(game.Grid.TryGetObject(cell, out var entry) && entry.Data.Id == BuildCatalog.Wall,
                          $"No plain back wall at {cell}.");
            game.Build.RemoveAtWorldPos(game.Grid.GridToWorld(cell));
            yield return null;   // Destroy lands at the end of the frame
            // The removal plays a visual-only shrinking clone of the piece (FeedbackFX); step it to the end
            // rather than waiting on real time, then give its Destroy a frame to land.
            var fx = Object.FindAnyObjectByType<FeedbackFX>();
            if (fx != null) fx.Tick(FeedbackFX.ShrinkDuration);
            Assert.IsTrue(fx == null || fx.ActiveFurnitureTweenCount == 0, "The removal shrink never finished.");
            yield return null;
            var gap = Band(cell.x * cs + NeighbourInset, (cell.x + 1) * cs - NeighbourInset,
                           cell.y * cs, (cell.y + 1) * cs + IntoRoom);
            var left = SolidTransforms(gap).Select(t => Describe(game, t)).ToList();
            Assert.IsEmpty(left, "The removed back wall's cell is still walled by: " + string.Join("; ", left));
        }

        /// <summary>A new game's back row has doorway pieces mid-row, and the yard behind is reachable on the NavMesh.</summary>
        [UnityTest]
        public IEnumerator NewGame_BackDoorOpensIntoWalkableYard()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            var doors = BackDoorPieces(game);
            Assert.AreEqual(game.Layout.BackDoorRingCells().width, doors.Count, "The back row has no full back door.");
            Assert.IsTrue(doors.All(game.Layout.IsDoorwayCell), "A back door piece is not on a doorway cell.");
            Assert.IsTrue(SaveAndRead(game).backDoorSeeded, "A new game should record its back door.");

            float cs = GridManager.CellSize;
            Vector3 door = game.Grid.GridToWorld(doors[0]);
            Vector3 behind = door + new Vector3(cs * 0.5f, 0f, -YardBehind);
            Assert.IsTrue(NavMesh.SamplePosition(behind, out var yardHit, NavSampleRadius, NavMesh.AllAreas),
                          $"The yard behind the back door ({behind}) is not on the NavMesh.");
            Assert.IsTrue(NavMesh.SamplePosition(game.Layout.TillPosition, out var tillHit, NavSampleRadius, NavMesh.AllAreas));
            var path = new NavMeshPath();
            NavMesh.CalculatePath(tillHit.position, yardHit.position, NavMesh.AllAreas, path);
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "No walkable path out through the back door.");
        }

        /// <summary>The back door's lane keeps clear of the till spot and the first places of its queue.</summary>
        [UnityTest]
        public IEnumerator BackDoor_KeepsClearOfTillAndQueue()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            RectInt back = game.Layout.BackDoorwayCells();
            var till = game.Grid.WorldToGrid(game.Layout.TillPosition);
            Assert.IsFalse(back.Contains(till), $"The till cell {till} lies in the back doorway {back}.");
            for (int place = 0; place < QueuePlacesChecked; place++)
            {
                var cell = game.Grid.WorldToGrid(game.Queue.StandingPosition(place));
                Assert.IsFalse(back.Contains(cell), $"Queue place {place} at {cell} lies in the back doorway {back}.");
            }
        }

        /// <summary>An old save with plain walls mid back row loads with the back door swapped in, once.</summary>
        [UnityTest]
        public IEnumerator OldSave_PlainBackWalls_BecomeBackDoorOnce()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            var data = SaveAndRead(game);
            RectInt back = game.Layout.BackDoorRingCells();
            data.backDoorSeeded = false;
            foreach (var item in data.PlacedObjects.Where(p => back.Contains(new Vector2Int(p.cellX, p.cellY))))
                item.catalogId = BuildCatalog.Wall;
            Reload(game, data);

            Assert.AreEqual(back.width, BackDoorPieces(game).Count, "The old save's back walls were not swapped for a door.");
            Assert.IsTrue(SaveAndRead(game).backDoorSeeded, "The migrated save should record its back door.");
        }

        /// <summary>An old save whose player removed the mid back walls keeps that gap: no door is forced in.</summary>
        [UnityTest]
        public IEnumerator OldSave_EditedBackWall_IsLeftAlone()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            var data = SaveAndRead(game);
            RectInt back = game.Layout.BackDoorRingCells();
            data.backDoorSeeded = false;
            data.PlacedObjects.RemoveAll(p => back.Contains(new Vector2Int(p.cellX, p.cellY)));
            Reload(game, data);

            foreach (var cell in back.allPositionsWithin)
                Assert.IsFalse(game.Grid.TryGetObject(cell, out _), $"The player's removed back wall at {cell} came back.");
        }

        /// <summary>World box over the wall height band between the given X and Z bounds.</summary>
        private static Bounds Band(float x0, float x1, float z0, float z1)
        {
            var b = new Bounds();
            b.SetMinMax(new Vector3(x0, BandBottom, z0), new Vector3(x1, BandTop, z1));
            return b;
        }

        /// <summary>Names of active renderers and solid colliders intersecting <paramref name="band"/>.</summary>
        private static IEnumerable<Transform> SolidTransforms(Bounds band)
        {
            Physics.SyncTransforms();
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                                  .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.bounds.Intersects(band))
                                  .Select(r => r.transform);
            var colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                                  .Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy && c.bounds.Intersects(band))
                                  .Select(c => c.transform);
            // The city backdrop is a hollow ring far outside the shop; its box bounds swallow the whole world.
            return renderers.Concat(colliders)
                            .Where(t => t.gameObject.layer != GameLayers.Character && t.name != CityBackdrop.ObjectName)
                            .Distinct();
        }

        /// <summary>Hierarchy path, world bounds and owning grid piece (if any) of <paramref name="t"/>.</summary>
        private static string Describe(GameManager game, Transform t)
        {
            string path = t.name;
            for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            var r = t.GetComponent<Renderer>();
            var c = t.GetComponent<Collider>();
            string bounds = r != null ? "R" + r.bounds : c != null ? "C" + c.bounds : "?";
            var owner = game.Grid.GetAllPlaced().FirstOrDefault(e => e.Instance != null && t.IsChildOf(e.Instance.transform));
            string own = owner != null ? $"{owner.Data?.Id}@{owner.Root}" : "no grid owner";
            return $"{path} {bounds} [{own}] destroyed-pending={(t.gameObject == null)}";
        }

        /// <summary>Solids in <paramref name="band"/> that are neither part of a placed grid piece nor a corner filler.</summary>
        private static IEnumerable<string> Strays(GameManager game, Bounds band)
        {
            var pieces = game.Grid.GetAllPlaced().Where(e => e.Instance != null).Select(e => e.Instance.transform).ToList();
            return SolidTransforms(band)
                .Where(t => t.name != ShopLayout.RoomWallCornerName && !pieces.Any(t.IsChildOf))
                .Select(t => t.name);
        }

        /// <summary>Roots of the doorway pieces standing on the back ring row.</summary>
        private static List<Vector2Int> BackDoorPieces(GameManager game)
        {
            RectInt back = game.Layout.BackDoorRingCells();
            return game.Grid.GetAllPlaced()
                       .Where(e => e.Data != null && e.Data.Id == BuildCatalog.WallDoor && back.Contains(e.Root))
                       .Select(e => e.Root).OrderBy(c => c.x).ToList();
        }

        private static SaveData SaveAndRead(GameManager game)
        {
            Assert.IsTrue(game.SaveGame(), "Saving failed.");
            var data = SaveSystem.Load();
            Assert.IsNotNull(data, "The save could not be read back.");
            return data;
        }

        /// <summary>Clears every placed piece through the Remove path, then loads <paramref name="data"/> as at boot.</summary>
        private static void Reload(GameManager game, SaveData data)
        {
            foreach (var entry in game.Grid.GetAllPlaced().ToList())
                game.Build.RemoveAtWorldPos(game.Grid.GridToWorld(entry.Root));
            var field = typeof(GameManager).GetField("_saveLoad", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "GameManager has no save/load controller.");
            ((SaveLoadController)field.GetValue(game)).LoadGame(data);
        }
    }
}
