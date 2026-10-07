using System.Collections.Generic;

namespace PetShop.Events
{
    /// <summary>The seasonal events that can run in the shop, one at a time.</summary>
    public enum ShopEventKind
    {
        /// <summary>No event running.</summary>
        None,
        /// <summary>The wholesaler discounts supplier orders.</summary>
        SupplierSale,
        /// <summary>Summer-only: pens use up feed and bedding faster.</summary>
        Heatwave,
        /// <summary>A festival outside brings more, and more frequent, customers.</summary>
        StreetFestival,
    }

    /// <summary>
    /// Pure seasonal-event rules: which season a day falls in, the deterministic daily roll,
    /// how long each event lasts and what it multiplies. No scene dependencies, so it is
    /// unit-testable.
    /// </summary>
    public static class ShopEventRules
    {
        /// <summary>Days in one season.</summary>
        public const int DaysPerSeason = 7;

        /// <summary>Seasons in one year.</summary>
        public const int SeasonsPerYear = 4;

        /// <summary>Index of summer, the only season a heatwave can happen in.</summary>
        public const int SummerSeasonIndex = 1;

        /// <summary>Chance that an event starts on a day with none running.</summary>
        public const double DailyEventChance = 0.25;

        /// <summary>Supplier cost multiplier during a supplier sale.</summary>
        public const float SupplierSaleMultiplier = 0.75f;

        /// <summary>Pen feed/bedding drain multiplier during a heatwave.</summary>
        public const float HeatwaveCareDrain = 1.5f;

        /// <summary>Customer spawn interval multiplier during a street festival.</summary>
        public const float FestivalIntervalMultiplier = 0.6f;

        /// <summary>Daily customer target multiplier during a street festival.</summary>
        public const float FestivalTargetMultiplier = 1.5f;

        /// <summary>Days a supplier sale lasts.</summary>
        public const int SupplierSaleDuration = 1;

        /// <summary>Days a heatwave lasts.</summary>
        public const int HeatwaveDuration = 2;

        /// <summary>Days a street festival lasts.</summary>
        public const int StreetFestivalDuration = 1;

        /// <summary>The first day of the game.</summary>
        private const int FirstDay = 1;

        /// <summary>Multiplier that leaves a value unchanged.</summary>
        private const float Neutral = 1f;

        /// <summary>Zero-based season index for <paramref name="day"/>; days below 1 count as day 1.</summary>
        public static int SeasonForDay(int day)
        {
            int clamped = day < FirstDay ? FirstDay : day;
            return ((clamped - FirstDay) / DaysPerSeason) % SeasonsPerYear;
        }

        /// <summary>True when <paramref name="day"/> falls in summer.</summary>
        public static bool IsSummer(int day) => SeasonForDay(day) == SummerSeasonIndex;

        /// <summary>
        /// Deterministic roll for (<paramref name="seed"/>, <paramref name="day"/>): usually
        /// <see cref="ShopEventKind.None"/>, otherwise a uniform pick among the kinds eligible
        /// that day. Heatwave is only eligible in summer.
        /// </summary>
        public static ShopEventKind Roll(int seed, int day)
        {
            var rng = new System.Random(seed ^ day);
            if (rng.NextDouble() >= DailyEventChance) return ShopEventKind.None;

            var eligible = EligibleKinds(day);
            return eligible[rng.Next(eligible.Count)];
        }

        /// <summary>The event kinds that can start on <paramref name="day"/>.</summary>
        public static List<ShopEventKind> EligibleKinds(int day)
        {
            var kinds = new List<ShopEventKind> { ShopEventKind.SupplierSale, ShopEventKind.StreetFestival };
            if (IsSummer(day)) kinds.Add(ShopEventKind.Heatwave);
            return kinds;
        }

        /// <summary>How many days <paramref name="kind"/> lasts; 0 for None.</summary>
        public static int DurationFor(ShopEventKind kind) => kind switch
        {
            ShopEventKind.SupplierSale   => SupplierSaleDuration,
            ShopEventKind.Heatwave       => HeatwaveDuration,
            ShopEventKind.StreetFestival => StreetFestivalDuration,
            _                            => 0,
        };

        /// <summary>Supplier cost multiplier while <paramref name="kind"/> runs.</summary>
        public static float PriceMultiplier(ShopEventKind kind) =>
            kind == ShopEventKind.SupplierSale ? SupplierSaleMultiplier : Neutral;

        /// <summary>Pen care drain multiplier while <paramref name="kind"/> runs.</summary>
        public static float CareDrainMultiplier(ShopEventKind kind) =>
            kind == ShopEventKind.Heatwave ? HeatwaveCareDrain : Neutral;

        /// <summary>Customer spawn interval multiplier while <paramref name="kind"/> runs.</summary>
        public static float IntervalMultiplier(ShopEventKind kind) =>
            kind == ShopEventKind.StreetFestival ? FestivalIntervalMultiplier : Neutral;

        /// <summary>Daily customer target multiplier while <paramref name="kind"/> runs.</summary>
        public static float TargetMultiplier(ShopEventKind kind) =>
            kind == ShopEventKind.StreetFestival ? FestivalTargetMultiplier : Neutral;

        /// <summary>Player-facing name of <paramref name="kind"/>.</summary>
        public static string DisplayName(ShopEventKind kind) => kind switch
        {
            ShopEventKind.SupplierSale   => Localization.Loc.T("event.name.supplier_sale"),
            ShopEventKind.Heatwave       => Localization.Loc.T("event.name.heatwave"),
            ShopEventKind.StreetFestival => Localization.Loc.T("event.name.street_festival"),
            _                            => Localization.Loc.T("event.name.none"),
        };

        /// <summary>Player-facing morning announcement for <paramref name="kind"/>.</summary>
        public static string Announcement(ShopEventKind kind) => kind switch
        {
            ShopEventKind.SupplierSale   => Localization.Loc.T("event.announce.supplier_sale"),
            ShopEventKind.Heatwave       => Localization.Loc.T("event.announce.heatwave"),
            ShopEventKind.StreetFestival => Localization.Loc.T("event.announce.street_festival"),
            _                            => "",
        };
    }
}
