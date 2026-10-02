using System;
using System.Collections.Generic;
using UnityEngine;
using PetShop.Core;
using PetShop.Commerce;

namespace PetShop.Shop
{
    /// <summary>One piece of furniture paid for and on its way, or waiting on the forecourt as a crate.</summary>
    [Serializable]
    public class FurnitureOrder
    {
        /// <summary>The <see cref="BuildCatalog"/> id ordered.</summary>
        public string CatalogId;
        /// <summary>Point in the trading day (0–1) at which the van drops the crate.</summary>
        public float  ArrivalProgress;
        /// <summary>True once the crate is on the forecourt, waiting to be collected.</summary>
        public bool   Arrived;
    }

    /// <summary>
    /// The furniture supply chain: the player orders a catalogue item (paid up front), it arrives
    /// later in the day as a crate on the forecourt, and collecting the crate puts it into the
    /// furniture inventory, from which it is placed for free. Removing placed furniture returns it
    /// to the inventory. Plain class so it can be tested without a scene.
    /// </summary>
    public sealed class FurnitureSupply
    {
        /// <summary>Earliest delivery, as a fraction of a trading day after ordering.</summary>
        public const float MinDeliveryDelay = 0.10f;
        /// <summary>Latest delivery, as a fraction of a trading day after ordering.</summary>
        public const float MaxDeliveryDelay = 0.22f;
        /// <summary>Last point in the day a van can still arrive; later orders land just before close.</summary>
        public const float LatestArrival = 0.97f;

        private readonly Dictionary<string, int> _owned   = new();
        private readonly List<FurnitureOrder>    _pending = new();

        /// <summary>Raised when an order's crate lands on the forecourt.</summary>
        public event Action<FurnitureOrder> OnArrived;
        /// <summary>Raised whenever the inventory or the pending orders change.</summary>
        public event Action OnChanged;

        /// <summary>Units of each catalogue id in the furniture inventory, ready to place.</summary>
        public IReadOnlyDictionary<string, int> Owned => _owned;
        /// <summary>Orders in transit or waiting on the forecourt.</summary>
        public IReadOnlyList<FurnitureOrder> Pending => _pending;

        /// <summary>How many of <paramref name="id"/> are in the inventory.</summary>
        public int OwnedCount(string id) => id != null && _owned.TryGetValue(id, out int n) ? n : 0;

        /// <summary>
        /// Orders one <paramref name="id"/>, charging its catalogue price now. Null when the id is
        /// unknown or the shop cannot afford it.
        /// </summary>
        public FurnitureOrder Order(string id, ShopManager shop, float dayProgressNow)
        {
            var def = BuildCatalog.Get(id);
            if (def == null || shop == null) return null;
            if (!shop.ChangeBalance(-def.Cost, $"Order {def.DisplayName}")) return null;

            var order = new FurnitureOrder
            {
                CatalogId       = id,
                ArrivalProgress = Mathf.Min(LatestArrival,
                    dayProgressNow + UnityEngine.Random.Range(MinDeliveryDelay, MaxDeliveryDelay)),
            };
            _pending.Add(order);
            OnChanged?.Invoke();
            return order;
        }

        /// <summary>Lands every in-transit order whose arrival time has passed.</summary>
        public void Tick(float dayProgress)
        {
            foreach (var order in _pending.ToArray())
                if (!order.Arrived && dayProgress >= order.ArrivalProgress) Arrive(order);
        }

        /// <summary>Lands every in-transit order — orders still on the road at close arrive overnight.</summary>
        public void ArriveAll()
        {
            foreach (var order in _pending.ToArray())
                if (!order.Arrived) Arrive(order);
        }

        /// <summary>Re-announces every crate already on the forecourt, e.g. after loading a save.</summary>
        public void RespawnArrived()
        {
            foreach (var order in _pending.ToArray())
                if (order.Arrived) OnArrived?.Invoke(order);
        }

        /// <summary>Marks <paramref name="order"/> as on the forecourt and announces it.</summary>
        private void Arrive(FurnitureOrder order)
        {
            order.Arrived = true;
            OnArrived?.Invoke(order);
            OnChanged?.Invoke();
        }

        /// <summary>Collects an arrived crate into the inventory. False when it is not a waiting crate.</summary>
        public bool Collect(FurnitureOrder order)
        {
            if (order == null || !order.Arrived || !_pending.Remove(order)) return false;
            AddOwned(order.CatalogId);
            return true;
        }

        /// <summary>Puts <paramref name="count"/> of <paramref name="id"/> into the inventory.</summary>
        public void AddOwned(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return;
            _owned[id] = OwnedCount(id) + count;
            OnChanged?.Invoke();
        }

        /// <summary>Takes one <paramref name="id"/> out of the inventory. False when none is owned.</summary>
        public bool TakeOwned(string id)
        {
            int n = OwnedCount(id);
            if (n <= 0) return false;
            if (n == 1) _owned.Remove(id);
            else        _owned[id] = n - 1;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Empties the inventory and drops every pending order (new game).</summary>
        public void Clear()
        {
            _owned.Clear();
            _pending.Clear();
            OnChanged?.Invoke();
        }

        // ── Save / load ─────────────────────────────────────────────────────────

        /// <summary>Writes the inventory and pending orders into <paramref name="data"/>.</summary>
        public void Capture(SaveData data)
        {
            data.FurnitureInventory.Clear();
            foreach (var kvp in _owned)
                data.FurnitureInventory.Add(new SaveData.StockEntry { id = kvp.Key, qty = kvp.Value });

            data.PendingFurnitureOrders.Clear();
            foreach (var o in _pending)
                data.PendingFurnitureOrders.Add(new SaveData.FurnitureOrderSave
                    { catalogId = o.CatalogId, arrivalProgress = o.ArrivalProgress, arrived = o.Arrived });
        }

        /// <summary>
        /// Replaces the inventory and pending orders with those in <paramref name="data"/>, skipping
        /// unknown catalogue ids. Raises no arrival events; call <see cref="RespawnArrived"/> after.
        /// </summary>
        public void Restore(SaveData data)
        {
            _owned.Clear();
            _pending.Clear();
            foreach (var e in data.FurnitureInventory ?? new List<SaveData.StockEntry>())
                if (e != null && BuildCatalog.Get(e.id) != null && e.qty > 0) _owned[e.id] = OwnedCount(e.id) + e.qty;

            foreach (var o in data.PendingFurnitureOrders ?? new List<SaveData.FurnitureOrderSave>())
                if (o != null && BuildCatalog.Get(o.catalogId) != null)
                    _pending.Add(new FurnitureOrder
                        { CatalogId = o.catalogId, ArrivalProgress = o.arrivalProgress, Arrived = o.arrived });
            OnChanged?.Invoke();
        }
    }
}
