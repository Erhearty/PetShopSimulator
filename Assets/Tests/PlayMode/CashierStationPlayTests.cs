using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Shop;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// A hired cashier works from behind the counter — the side away from the queue — wherever the
    /// counter stands or faces; nobody can be hired as a cashier before a counter exists; and a
    /// cashier whose counter is packed away stops serving until a new one is placed.
    /// </summary>
    public class CashierStationPlayTests
    {
        private const int   Seed      = 2468;
        private const float TimeScale = 1f;
        /// <summary>Long enough that the day cannot close mid-test.</summary>
        private const float DayLength = 900f;
        /// <summary>Furthest a cashier may stand from the counter's body and still reach the till.</summary>
        private const float CashierReach = 1.5f;
        /// <summary>How far from the cashier the NavMesh may be and still count as standing on it.</summary>
        private const float NavTolerance = 0.5f;
        /// <summary>Height at which a standing body is checked against the counter collider.</summary>
        private const float BodyHeight = 0.5f;
        /// <summary>Frames a cashier with instant service is given to (not) serve a waiting shopper.</summary>
        private const int ServeFrames = 5;
        private const float BasketValue = 20f;
        private const float QuarterTurn = 90f;
        private const float HalfTurn    = 180f;
        private const float NoTurn      = 0f;
        /// <summary>Columns in from the room's side wall where the search for a back-wall counter spot starts.</summary>
        private const int WallCounterColumn = 1;
        /// <summary>The notice build mode shows for a counter with no room behind it.</summary>
        private const string NoRoomBehindNotice = "Leave room behind the counter for the cashier";
        /// <summary>Columns sideways the replacement counter is placed from the first one.</summary>
        private const int ReplacementColumnShift = -3;
        /// <summary>The reason the hire button shows when there is no counter.</summary>
        private const string NoCounterReason = "Place a counter first";

        private GameManager _game;

        [TearDown]
        public void TearDown()
        {
            _game = null;
            PlaytestHarness.Teardown();
        }

        /// <summary>A free-standing counter in the middle of the room.</summary>
        [UnityTest]
        public IEnumerator Hire_FreeStandingCounter_CashierBehindCounter()
        {
            yield return BootWorld();
            yield return AssertCashierBehind(PlaceCounter(MiddleCell(), NoTurn));
        }

        /// <summary>
        /// A counter with its back to the room's back wall leaves the cashier nowhere to stand, so build mode
        /// refuses it — though the same spot takes a counter turned to put its back into the room.
        /// </summary>
        [UnityTest]
        public IEnumerator Place_CounterBackToBackWall_Refused()
        {
            yield return BootWorld();
            Assert.AreEqual(NoRoomBehindNotice, BuildMode.CounterNoRoomNotice);
            RectInt room = _game.Layout.RoomCells();
            var def = BuildCatalog.Get(BuildCatalog.Counter);

            // A back-row spot that fits a counter whose back faces into the room (clear of the doorways).
            Vector2Int? spot = null;
            for (int x = room.xMin + WallCounterColumn; x + def.Size.x <= room.xMax && spot == null; x++)
            {
                var cell = new Vector2Int(x, room.yMin);
                if (_game.Build.CanPlaceItem(def, cell, def.Size, HalfTurn)) spot = cell;
            }
            Assert.IsTrue(spot.HasValue, "No back-row spot fits a counter facing the back wall.");
            Assert.IsTrue(_game.Layout.IsRoomWallCell(spot.Value + Vector2Int.down),
                          $"The cell behind back-row spot {spot.Value} is not the back wall.");

            Assert.IsFalse(_game.Build.CanPlaceItem(def, spot.Value, def.Size, NoTurn),
                           $"A counter at {spot.Value} with its back to the back wall was allowed.");
        }

        /// <summary>A counter whose end touches a side wall, facing into the room, still has its cashier behind it.</summary>
        [UnityTest]
        public IEnumerator Hire_CounterSideAgainstSideWall_CashierBehindCounter()
        {
            yield return BootWorld();
            RectInt room = _game.Layout.RoomCells();
            var cell = new Vector2Int(room.xMin, MiddleCell().y);
            Assert.IsTrue(_game.Layout.IsRoomWallCell(cell + Vector2Int.left), $"{cell} is not next to a side wall.");
            var def = BuildCatalog.Get(BuildCatalog.Counter);
            Assert.IsTrue(_game.Build.CanPlaceItem(def, cell, def.Size, NoTurn),
                          $"A counter at {cell} beside the side wall, facing into the room, was refused.");
            yield return AssertCashierBehind(PlaceCounter(cell, NoTurn));
        }

        /// <summary>A counter turned a quarter turn.</summary>
        [UnityTest]
        public IEnumerator Hire_CounterTurned90_CashierBehindCounter()
        {
            yield return BootWorld();
            yield return AssertCashierBehind(PlaceCounter(MiddleCell(), QuarterTurn));
        }

        /// <summary>A counter turned round to face the back of the shop.</summary>
        [UnityTest]
        public IEnumerator Hire_CounterTurned180_CashierBehindCounter()
        {
            yield return BootWorld();
            yield return AssertCashierBehind(PlaceCounter(MiddleCell(), HalfTurn));
        }

        /// <summary>No counter: the cashier's hire button is disabled with a reason, and hiring is refused.</summary>
        [UnityTest]
        public IEnumerator Hire_NoCounter_CashierRefused()
        {
            yield return BootWorld();
            Assert.IsFalse(_game.Spawner.HasCounter, "A new game should have no counter.");

            _game.RefreshCandidates();
            foreach (var c in _game.Candidates) { c.Role = StaffRole.Cashier; c.SignOnFee = 0f; }

            var board = Object.FindAnyObjectByType<GameUI>().StaffBoard;
            board.Show();
            var hire = board.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Hire_0");
            Assert.IsNotNull(hire, "The staff board has no hire button for the first applicant.");
            Assert.IsFalse(hire.interactable, "A cashier's hire button must be disabled without a counter.");
            StringAssert.Contains(NoCounterReason, hire.GetComponentInChildren<TMP_Text>(true).text);
            board.Hide();

            Assert.IsFalse(_game.HireCandidate(_game.Candidates[0]), "Hiring a cashier without a counter succeeded.");
            Assert.AreEqual(0, _game.StaffCount, "Nobody should be on the payroll.");
        }

        /// <summary>Packing the counter away stops the cashier; a new counter puts them back to work behind it.</summary>
        [UnityTest]
        public IEnumerator RemoveCounter_CashierStopsThenResumesBehindNewCounter()
        {
            yield return BootWorld();
            var first = PlaceCounter(MiddleCell(), NoTurn);
            Assistant cashier = HireCashier();
            cashier.ServiceSeconds = 0f;   // serves on the first frame a shopper is ready

            _game.Build.RemoveAtWorldPos(first.transform.position);
            yield return null;
            Assert.IsFalse(_game.Spawner.HasCounter, "The counter was not removed.");

            var shopper = new StandingShopper();
            _game.Queue.Join(shopper);
            for (int i = 0; i < ServeFrames; i++) yield return null;
            Assert.IsFalse(shopper.Served, "A cashier with no counter served a customer.");
            Assert.AreEqual(1, _game.StaffCount, "Removing the counter must keep the staff on the payroll.");

            var second = PlaceCounter(MiddleCell() + new Vector2Int(ReplacementColumnShift, 0), NoTurn);
            AssertBehind(second, cashier.transform.position);
            for (int i = 0; i < ServeFrames; i++) yield return null;
            Assert.IsTrue(shopper.Served, "The cashier did not resume serving once a counter was placed.");
        }

        /// <summary>
        /// With two cashiers, packing the last counter away shows the no-counter notice once for the shop, not
        /// once per cashier; placing a counter and losing it again shows it once more.
        /// </summary>
        [UnityTest]
        public IEnumerator RemoveCounter_TwoCashiers_OneNoCounterNotice()
        {
            yield return BootWorld();
            var counter = PlaceCounter(MiddleCell(), NoTurn);
            HireCashier();
            HireCashier();

            int notices = 0;
            UnityAction<string> listen = m => { if (m == Assistant.NoCounterNotice) notices++; };
            _game.OnNotification.AddListener(listen);
            try
            {
                _game.Build.RemoveAtWorldPos(counter.transform.position);
                for (int i = 0; i < ServeFrames; i++) yield return null;
                Assert.AreEqual(1, notices, "Two cashiers losing the last counter should give exactly one notice.");

                var again = PlaceCounter(MiddleCell(), NoTurn);
                for (int i = 0; i < ServeFrames; i++) yield return null;
                _game.Build.RemoveAtWorldPos(again.transform.position);
                for (int i = 0; i < ServeFrames; i++) yield return null;
                Assert.AreEqual(2, notices, "Losing the last counter again should give exactly one more notice.");
            }
            finally { _game.OnNotification.RemoveListener(listen); }
        }

        private IEnumerator BootWorld()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            _game = PlaytestHarness.Game;
        }

        private Vector2Int MiddleCell()
        {
            RectInt room = _game.Layout.RoomCells();
            return new Vector2Int(room.x + room.width / 2, room.y + room.height / 2);
        }

        /// <summary>Places a counter the way build mode does, turned <paramref name="yaw"/> degrees.</summary>
        private GameObject PlaceCounter(Vector2Int cell, float yaw)
        {
            bool sideways = BuildMode.SwapsFootprint(yaw);
            var def  = BuildCatalog.Get(BuildCatalog.Counter);
            var size = sideways ? new Vector2Int(def.Size.y, def.Size.x) : def.Size;
            cell = NearestFreeCell(cell, size, yaw);
            var counter = _game.Build.Place(cell, def, null, yaw, charge: false, footprintRotated: sideways);
            Assert.IsNotNull(counter, $"Could not place a counter at {cell} turned {yaw}.");
            Physics.SyncTransforms();
            return counter;
        }

        /// <summary>Furthest a counter is slid along its row, then across rows, to find free floor.</summary>
        private const int FreeCellSearch = 4;

        /// <summary>
        /// The preferred cell when a counter of the given size fits there; otherwise the nearest cell that
        /// fits, sliding along the same row first (so a counter meant for the back wall stays against it).
        /// The room's reserved lanes (doorways, till lane) move with the layout.
        /// </summary>
        private Vector2Int NearestFreeCell(Vector2Int preferred, Vector2Int size, float yaw)
        {
            var def = BuildCatalog.Get(BuildCatalog.Counter);
            for (int dy = 0; dy <= FreeCellSearch; dy++)
            for (int dx = 0; dx <= FreeCellSearch; dx++)
            foreach (int sy in new[] { 1, -1 })
            foreach (int sx in new[] { -1, 1 })
            {
                var cell = preferred + new Vector2Int(sx * dx, sy * dy);
                if (_game.Build.CanPlaceItem(def, cell, size, yaw)) return cell;
            }
            return preferred;
        }

        /// <summary>Hires a free cashier and returns them.</summary>
        private Assistant HireCashier()
        {
            var candidate = StaffCandidate.Generate();
            candidate.SignOnFee = 0f;
            candidate.Role      = StaffRole.Cashier;
            Assert.IsTrue(_game.HireCandidate(candidate), "Hiring a cashier with a counter placed failed.");
            return _game.Staff[_game.StaffCount - 1];
        }

        /// <summary>Hires a cashier and checks where they stand, then that they stand on the rebaked NavMesh.</summary>
        private IEnumerator AssertCashierBehind(GameObject counter)
        {
            Assistant cashier = HireCashier();
            yield return null;
            Vector3 at = cashier.transform.position;
            AssertBehind(counter, at);

            bool ignored = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;   // editor-only pack-mesh read warnings during the bake
            try { _game.Layout.BakeNavMesh(); }
            finally { LogAssert.ignoreFailingMessages = ignored; }

            Assert.IsTrue(NavMesh.SamplePosition(at, out NavMeshHit hit, NavTolerance, NavMesh.AllAreas),
                          $"The cashier at {at} is off the NavMesh.");
            Assert.LessOrEqual(Flat(hit.position - at).magnitude, NavTolerance, "The cashier is off the NavMesh.");
        }

        /// <summary>
        /// <paramref name="at"/> is on the far side of <paramref name="counter"/> from the front of the
        /// queue, clear of its body and within reach of it.
        /// </summary>
        private void AssertBehind(GameObject counter, Vector3 at)
        {
            Vector3 centre = Flat(counter.transform.position);
            Vector3 queue  = Flat(_game.Queue.StandingPosition(0)) - centre;
            Assert.Greater(queue.sqrMagnitude, 0f, "The front of the queue sits on the counter's centre.");
            Assert.Less(Vector3.Dot(Flat(at) - centre, queue), 0f,
                        $"The cashier at {at} is on the queue's side of the counter, not behind it.");

            Vector3 body = Flat(at) + Vector3.up * BodyHeight;
            float nearest = float.MaxValue;
            foreach (var col in counter.GetComponentsInChildren<Collider>())
            {
                Assert.IsFalse(col.bounds.Contains(body), $"The cashier at {at} is inside the counter.");
                nearest = Mathf.Min(nearest, Flat(col.ClosestPoint(body) - body).magnitude);
            }
            Assert.LessOrEqual(nearest, CashierReach, $"The cashier at {at} is out of reach of the counter.");
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        /// <summary>A shopper with no body, always standing at the till.</summary>
        private sealed class StandingShopper : CheckoutQueue.IShopper
        {
            public bool Served { get; private set; }

            public float  BasketValue => CashierStationPlayTests.BasketValue;
            public int    BasketCount => 1;
            public string ShopperName => "Test shopper";
            public bool   IsAtTill    => true;

            public void OnServed() => Served = true;
            public void OnGaveUp() { }
        }
    }
}
