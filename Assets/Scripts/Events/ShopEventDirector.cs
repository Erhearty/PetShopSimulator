using System;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Customer;

namespace PetShop.Events
{
    /// <summary>
    /// Runs the seasonal events: rolls one each morning when none is running, pushes its
    /// multipliers into the shop and the customer spawner, counts it down at night and
    /// persists it with the save. Rules live in <see cref="ShopEventRules"/>.
    /// </summary>
    public class ShopEventDirector : MonoBehaviour
    {
        /// <summary>Multiplier that leaves a system unchanged.</summary>
        private const float Neutral = 1f;

        /// <summary>Seed used when the tick count happens to be zero.</summary>
        private const int FallbackSeed = 1;

        /// <summary>Seed for the deterministic daily rolls. Never 0 once initialised.</summary>
        public int Seed { get; private set; }

        /// <summary>The event currently running, or None.</summary>
        public ShopEventKind Active { get; private set; } = ShopEventKind.None;

        /// <summary>Days the active event still runs, counting today.</summary>
        public int DaysLeft { get; private set; }

        /// <summary>Pen feed/bedding drain multiplier for tonight.</summary>
        public float CareDrainMultiplier => ShopEventRules.CareDrainMultiplier(Active);

        private GameManager     _game;
        private ShopManager     _shop;
        private CustomerSpawner _spawner;

        /// <summary>Stores the systems the events act on (any may be null) and picks a seed.</summary>
        public void Init(GameManager game, ShopManager shop, CustomerSpawner spawner)
        {
            _game    = game;
            _shop    = shop;
            _spawner = spawner;
            if (Seed == 0) Seed = FreshSeed();
        }

        /// <summary>Rolls today's event if none is running, applies it and announces it.</summary>
        public void BeginDay(int day)
        {
            if (Active == ShopEventKind.None)
            {
                Active   = ShopEventRules.Roll(Seed, day);
                DaysLeft = ShopEventRules.DurationFor(Active);
            }
            ApplyMultipliers();
            if (Active != ShopEventKind.None) _game?.Notify(ShopEventRules.Announcement(Active));
        }

        /// <summary>Adds a headline for the running event, counts it down and clears it when over.</summary>
        public void EndDay(DaySummary summary)
        {
            if (Active == ShopEventKind.None) return;

            DaysLeft--;
            summary?.Headlines?.Add(Headline(Active, DaysLeft));
            if (DaysLeft <= 0) Clear();
        }

        /// <summary>Writes the seed and the running event into <paramref name="data"/>.</summary>
        public void Capture(SaveData data)
        {
            if (data == null) return;
            data.EventSeed     = Seed;
            data.ActiveEventId = Active.ToString();
            data.EventDaysLeft = DaysLeft;
        }

        /// <summary>Restores the seed and running event from <paramref name="data"/> and applies it.</summary>
        public void Restore(SaveData data)
        {
            if (data == null) return;
            Seed     = data.EventSeed != 0 ? data.EventSeed : FreshSeed();
            Active   = ParseKind(data.ActiveEventId);
            DaysLeft = data.EventDaysLeft;
            if (Active != ShopEventKind.None && DaysLeft <= 0) Clear();
            ApplyMultipliers();
        }

        /// <summary>Ends the running event and puts every multiplier back to normal.</summary>
        private void Clear()
        {
            Active   = ShopEventKind.None;
            DaysLeft = 0;
            ApplyMultipliers();
        }

        /// <summary>Pushes the active event's multipliers into the shop and spawner.</summary>
        private void ApplyMultipliers()
        {
            if (_shop != null) _shop.SupplierPriceMultiplier = ShopEventRules.PriceMultiplier(Active);
            if (_spawner == null) return;
            _spawner.IntervalMultiplier = ShopEventRules.IntervalMultiplier(Active);
            _spawner.TargetMultiplier   = ShopEventRules.TargetMultiplier(Active);
        }

        /// <summary>Night-summary line: days remaining, or that the event has ended.</summary>
        private static string Headline(ShopEventKind kind, int daysLeft)
        {
            string name = ShopEventRules.DisplayName(kind);
            if (daysLeft <= 0) return $"{name} ended";
            return $"{name}: {daysLeft} day{(daysLeft == 1 ? "" : "s")} left";
        }

        /// <summary>Parses a saved event id; null, empty or unknown ids become None.</summary>
        private static ShopEventKind ParseKind(string id)
        {
            if (string.IsNullOrEmpty(id)) return ShopEventKind.None;
            if (!Enum.TryParse(id, out ShopEventKind kind)) return ShopEventKind.None;
            return Enum.IsDefined(typeof(ShopEventKind), kind) ? kind : ShopEventKind.None;
        }

        /// <summary>A nonzero seed from the system tick count.</summary>
        private static int FreshSeed()
        {
            int tick = Environment.TickCount;
            return tick != 0 ? tick : FallbackSeed;
        }
    }
}
