using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PetShop.Commerce
{
    /// <summary>
    /// The line at the till. Customers with a basket join it and wait to be served; the
    /// player serves the front of the queue by interacting with the counter.
    ///
    /// This is what turns the shop from a vending machine into something you run: takings
    /// only arrive if you are behind the counter when people are ready to pay.
    /// </summary>
    public class CheckoutQueue : MonoBehaviour
    {
        public interface IShopper
        {
            float  BasketValue { get; }
            int    BasketCount { get; }
            string ShopperName { get; }
            void   OnServed();
            void   OnGaveUp();
        }

        [Header("Layout")]
        public Transform TillPoint;
        [Tooltip("Spacing between people in the line, in metres.")]
        public float Spacing = 0.9f;
        [Tooltip("Direction the queue runs away from the till.")]
        public Vector3 QueueDirection = Vector3.forward;

        [Header("Patience")]
        public float PatienceSeconds = 42f;
        [Tooltip("Extra patience for each person ahead — people expect to wait in a line.")]
        public float PatiencePerPlace = 12f;

        public UnityEvent<int> OnQueueChanged = new();

        private readonly List<IShopper>      _queue = new();
        private readonly Dictionary<IShopper, float> _joinedAt = new();

        public int  Length      => _queue.Count;
        public bool AnyWaiting  => _queue.Count > 0;
        public IShopper Front   => _queue.Count > 0 ? _queue[0] : null;

        /// <summary>Total money standing in the queue right now.</summary>
        public float WaitingValue
        {
            get
            {
                float total = 0f;
                foreach (var s in _queue) total += s.BasketValue;
                return total;
            }
        }

        public void Join(IShopper shopper)
        {
            if (shopper == null || _queue.Contains(shopper)) return;
            _queue.Add(shopper);
            _joinedAt[shopper] = Time.time;
            OnQueueChanged.Invoke(_queue.Count);
        }

        public void Leave(IShopper shopper)
        {
            if (shopper == null || !_queue.Remove(shopper)) return;
            _joinedAt.Remove(shopper);
            OnQueueChanged.Invoke(_queue.Count);
        }

        /// <summary>Where the shopper at <paramref name="place"/> should stand.</summary>
        public Vector3 StandingPosition(int place)
        {
            Vector3 origin = TillPoint != null ? TillPoint.position : transform.position;
            return origin + QueueDirection.normalized * (0.7f + place * Spacing);
        }

        public int PlaceOf(IShopper shopper) => _queue.IndexOf(shopper);

        /// <summary>0 = just arrived, 1 = about to walk out. Drives the patience bar.</summary>
        public float Impatience(IShopper shopper)
        {
            int place = _queue.IndexOf(shopper);
            if (place < 0 || !_joinedAt.TryGetValue(shopper, out float joined)) return 0f;

            float allowance = PatienceSeconds + place * PatiencePerPlace;
            return Mathf.Clamp01((Time.time - joined) / allowance);
        }

        /// <summary>Serve the front of the queue. Returns the shopper served, or null.</summary>
        public IShopper ServeFront()
        {
            if (_queue.Count == 0) return null;

            var shopper = _queue[0];
            Leave(shopper);
            shopper.OnServed();
            return shopper;
        }

        private void Update()
        {
            // Walk out anyone who has waited too long, worst offender first.
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                var shopper = _queue[i];
                if (shopper == null) { _queue.RemoveAt(i); continue; }
                if (Impatience(shopper) < 1f) continue;

                Leave(shopper);
                shopper.OnGaveUp();
            }
        }
    }
}
