using PetShop.Core;
using PetShop.Customer;
using UnityEngine;

namespace PetShop.Dev
{
    /// <summary>
    /// Per-customer outcome lines and per-day reason counts: each empty walkout and each navigation
    /// timeout bumps its day counters and appends one <see cref="OutcomeLine"/> to the soak file.
    /// </summary>
    public partial class DayTelemetry
    {
        /// <summary>Reason written on an outcome line for a navigation timeout.</summary>
        public const string NavTimeoutReason = "navTimeout";

        /// <summary>One customer outcome, written as its own JSON line next to the day lines.</summary>
        [System.Serializable]
        public class OutcomeLine
        {
            /// <summary>Always true, so a reader can tell this line from a day or summary line.</summary>
            public bool walkout = true;
            /// <summary>Shop day the outcome happened on.</summary>
            public int day;
            /// <summary>Gameplay seed, or <see cref="SoakBands.NoSeed"/>.</summary>
            public int seed;
            /// <summary>Customer GameObject name.</summary>
            public string customer;
            /// <summary>A <see cref="WalkoutReason"/> name, or <see cref="NavTimeoutReason"/>.</summary>
            public string reason;
            /// <summary>The customer's <see cref="CustomerArchetype"/> name.</summary>
            public string archetype;
            /// <summary>The <see cref="NavLeg"/> name of the walk involved (the timed-out leg, or None).</summary>
            public string target;
            /// <summary>Game seconds since the customer spawned.</summary>
            public float elapsed;
        }

        /// <summary>Reused for every line, so writing one allocates only its JSON string.</summary>
        private readonly OutcomeLine _line = new();

        private void OnWalkedOutEmpty(CustomerAI customer)
        {
            WalkoutReason reason = customer != null ? customer.WalkoutReason : WalkoutReason.None;
            CountWalkout(_current, reason);
            WriteOutcome(customer, reason.ToString(), NavLeg.None);
        }

        private void OnNavigationTimedOut(CustomerAI customer)
        {
            NavLeg leg = customer != null ? customer.TimedOutLeg : NavLeg.None;
            CountNavTimeout(_current, leg);
            WriteOutcome(customer, NavTimeoutReason, leg);
        }

        private void WriteOutcome(CustomerAI customer, string reason, NavLeg leg)
        {
            int day = _shop != null ? _shop.Day : 0;
            FillOutcome(_line, customer, reason, leg, day, PlaytestOptions.Seed ?? SoakBands.NoSeed);
            AppendLine(JsonUtility.ToJson(_line));
        }

        /// <summary>Fills <paramref name="line"/> in place for <paramref name="customer"/> (which may be null).</summary>
        public static OutcomeLine FillOutcome(OutcomeLine line, CustomerAI customer, string reason, NavLeg leg,
                                              int day, int seed)
        {
            line.day       = day;
            line.seed      = seed;
            line.customer  = customer != null ? customer.name : string.Empty;
            line.reason    = reason;
            line.archetype = customer != null ? customer.Archetype.ToString() : string.Empty;
            line.target    = leg.ToString();
            line.elapsed   = customer != null ? customer.SecondsSinceSpawn : 0f;
            return line;
        }

        /// <summary>Counts one empty walkout on <paramref name="r"/>: the band total and its reason.</summary>
        public static void CountWalkout(DayRecord r, WalkoutReason reason)
        {
            r.walkoutsEmpty++;
            switch (reason)
            {
                case WalkoutReason.NoStockForWant:     r.walkoutNoStockForWant++;     break;
                case WalkoutReason.TooExpensive:       r.walkoutTooExpensive++;       break;
                case WalkoutReason.CouldNotReachShelf: r.walkoutCouldNotReachShelf++; break;
                case WalkoutReason.NotTempted:         r.walkoutNotTempted++;         break;
            }
        }

        /// <summary>Counts one navigation timeout on <paramref name="r"/>: the band total and its leg.</summary>
        public static void CountNavTimeout(DayRecord r, NavLeg leg)
        {
            r.navTimeouts++;
            switch (leg)
            {
                case NavLeg.ForecourtIn:  r.navTimeoutForecourtIn++;  break;
                case NavLeg.Browse:       r.navTimeoutBrowse++;       break;
                case NavLeg.Register:     r.navTimeoutRegister++;     break;
                case NavLeg.Queue:        r.navTimeoutQueue++;        break;
                case NavLeg.StepAside:    r.navTimeoutStepAside++;    break;
                case NavLeg.ForecourtOut: r.navTimeoutForecourtOut++; break;
                case NavLeg.Exit:         r.navTimeoutExit++;         break;
            }
        }
    }
}
