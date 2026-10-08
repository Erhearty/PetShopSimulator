using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Player;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// Reproduces "E at the counter does nothing": in the real world a counter is placed through
    /// <see cref="BuildMode.Place"/>, a shopper joins the <see cref="CheckoutQueue"/> and stands on
    /// <see cref="CheckoutQueue.StandingPosition"/>(0), and the player presses interact facing the
    /// counter. The shopper must be served and the takings booked.
    /// </summary>
    public class ServeAtCounterPlayTests
    {
        private const int   Seed        = 4711;
        private const float TimeScale   = 1f;
        /// <summary>Long enough that the day cannot close mid-test.</summary>
        private const float DayLength   = 900f;
        private const float BasketValue = 25f;
        /// <summary>The distance the brief names: a player facing the counter from 1.5 m.</summary>
        private const float FacingDistance = 1.5f;
        /// <summary>Pressed right up against the counter: the sphere cast starts inside its collider.</summary>
        private const float PressedDistance = 0.6f;
        /// <summary>CustomerAI's reach for "standing at the till".</summary>
        private const float AtTillReach = 1.5f;
        /// <summary>Height a standing customer's body is checked at, against the counter collider.</summary>
        private const float BodyHeight = 0.5f;

        private GameManager _game;
        private string      _lastNotice;

        [TearDown]
        public void TearDown()
        {
            if (_game != null) _game.OnNotification.RemoveListener(OnNotice);
            _game = null;
            PlaytestHarness.Teardown();
        }

        /// <summary>E from 1.5 m in front of the counter serves the shopper standing at the till.</summary>
        [UnityTest]
        public IEnumerator Interact_FacingCounter_ServesShopperAtTill()
        {
            yield return ServeFrom(FacingDistance);
        }

        /// <summary>Standing pressed against the counter still serves (overlap fallback).</summary>
        [UnityTest]
        public IEnumerator Interact_PressedAgainstCounter_ServesShopperAtTill()
        {
            yield return ServeFrom(PressedDistance);
        }

        /// <summary>E at the counter with nobody in line says so instead of doing nothing.</summary>
        [UnityTest]
        public IEnumerator Interact_EmptyQueue_NotifiesNobodyWaiting()
        {
            yield return BootWorld();
            var counter = PlaceCounter();
            yield return null;

            Assert.AreEqual(0, _game.Queue.Length, "The queue should start empty.");
            FacePlayerAtCounter(counter, FacingDistance).TryInteract();

            Assert.AreEqual(ShopFloorActions.NobodyWaitingNotice, _lastNotice);
        }

        private IEnumerator ServeFrom(float distance)
        {
            yield return BootWorld();
            var counter = PlaceCounter();
            yield return null;

            var shopper = new StandingShopper(_game.Queue, _game.Shop, BasketValue);
            _game.Queue.Join(shopper);
            shopper.Position = _game.Queue.StandingPosition(0);   // walked to the front of the line
            AssertClearOfCounter(counter, shopper.Position);
            Assert.IsTrue(shopper.IsAtTill, "A shopper on the front spot must count as at the till.");

            var player = FacePlayerAtCounter(counter, distance);
            float before = _game.Shop.Balance;
            player.TryInteract();

            Assert.AreEqual(0, _game.Queue.Length, "Interacting at the counter did not serve the queue.");
            Assert.IsTrue(shopper.Served, "The shopper was not told they were served.");
            Assert.Greater(_game.Shop.Balance, before, "Serving did not raise the balance.");
        }

        private IEnumerator BootWorld()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            _game = PlaytestHarness.Game;
            _game.OnNotification.AddListener(OnNotice);
        }

        private void OnNotice(string message) => _lastNotice = message;

        /// <summary>Places a counter in the middle of the shop room the way build mode does.</summary>
        private GameObject PlaceCounter()
        {
            RectInt room = _game.Layout.RoomCells();
            var cell = new Vector2Int(room.x + room.width / 2, room.y + room.height / 2);
            var counter = _game.Build.Place(cell, BuildCatalog.Get(BuildCatalog.Counter), null, 0f, charge: false);
            Assert.IsNotNull(counter, $"Could not place a counter at {cell}.");
            Physics.SyncTransforms();
            return counter;
        }

        /// <summary>Stands the player <paramref name="distance"/> behind the counter, facing it.</summary>
        private InteractionSystem FacePlayerAtCounter(GameObject counter, float distance)
        {
            var player = _game.Layout.Player;
            Assert.IsNotNull(player, "The scene has no player.");
            var interaction = player.GetComponent<InteractionSystem>();
            Assert.IsNotNull(interaction, "The player has no InteractionSystem.");

            Vector3 forward = -Flat(counter.transform.forward).normalized;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.SetPositionAndRotation(Flat(counter.transform.position) - forward * distance, // cashier side: counter +Z
                                          Quaternion.LookRotation(forward, Vector3.up));
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
            return interaction;
        }

        /// <summary>The front-of-line spot lies outside the counter, on its customer (-forward) side.</summary>
        private static void AssertClearOfCounter(GameObject counter, Vector3 spot)
        {
            Vector3 body = spot + Vector3.up * BodyHeight;
            foreach (var col in counter.GetComponentsInChildren<Collider>())
                Assert.IsFalse(col.bounds.Contains(body), $"The till spot {spot} is inside the counter.");
            float ahead = Vector3.Dot(Flat(spot - counter.transform.position), counter.transform.forward);
            Assert.Less(ahead, 0f, "The till spot is behind the counter, not on its customer side.");
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        /// <summary>
        /// A CustomerAI-like shopper with no body: it is "at the till" by CustomerAI's own rule
        /// (within reach of the front spot) and pays on being served, as CustomerAI's checkout does.
        /// </summary>
        private sealed class StandingShopper : CheckoutQueue.IShopper
        {
            private readonly CheckoutQueue _queue;
            private readonly ShopManager   _shop;

            public StandingShopper(CheckoutQueue queue, ShopManager shop, float value)
            {
                _queue = queue; _shop = shop; BasketValue = value;
            }

            public Vector3 Position { get; set; }
            public bool    Served   { get; private set; }

            public float  BasketValue { get; }
            public int    BasketCount => 1;
            public string ShopperName => "Test shopper";

            public bool IsAtTill
            {
                get
                {
                    Vector3 offset = _queue.StandingPosition(0) - Position;
                    offset.y = 0f;
                    return offset.magnitude <= AtTillReach;
                }
            }

            public void OnServed()
            {
                Served = true;
                _shop.ChangeBalance(BasketValue, "Test sale");
            }

            public void OnGaveUp() { }
        }
    }
}
