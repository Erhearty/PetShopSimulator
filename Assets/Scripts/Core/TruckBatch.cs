using System;
using System.Collections.Generic;

namespace PetShop.Core
{
    /// <summary>
    /// Collects the loads that fall due together (orders placed in one Tab-menu session share an arrival
    /// time, because the clock is paused while the book is open) so they ride on ONE delivery truck.
    /// Plain class so it can be tested without a scene.
    /// </summary>
    public sealed class TruckBatch
    {
        private readonly List<Action> _drops = new();

        /// <summary>Loads waiting for the next truck.</summary>
        public int Count => _drops.Count;

        /// <summary>Adds one load; <paramref name="drop"/> runs when the batch's truck pulls up.</summary>
        public void Add(Action drop)
        {
            if (drop != null) _drops.Add(drop);
        }

        /// <summary>
        /// Sends everything collected so far on a single truck. <paramref name="dispatch"/> gets one callback
        /// that drops every load and returns whether a truck took it; when it did not, the loads drop now.
        /// Returns the number of loads sent (0 means no truck was needed).
        /// </summary>
        public int Flush(Func<Action, bool> dispatch)
        {
            if (_drops.Count == 0) return 0;
            var loads = _drops.ToArray();
            _drops.Clear();
            int count = loads.Length;
            Action all = () => { foreach (var load in loads) load(); };
            if (dispatch == null || !dispatch(all)) all();
            return count;
        }

        /// <summary>Drops every waiting load immediately, with no truck (end of day, nothing is lost).</summary>
        public void DropAllNow() => Flush(null);
    }
}
