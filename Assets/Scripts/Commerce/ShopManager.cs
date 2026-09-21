using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PetShop.Commerce
{
    /// <summary>
    /// Central shop state: balance, reputation, stock ledger and the daily sales log.
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        [Header("Starting values")]
        public float StartingBalance = 1000f;
        [Range(0, 100)] public float StartingReputation = 40f;

        [Header("Pricing")]
        [Tooltip("Multiplier applied to every shelf price. Higher earns more per sale but puts people off.")]
        [Range(0.6f, 1.8f)] public float PriceMultiplier = 1f;

        [Header("Running costs")]
        public float BaseDailyRent   = 45f;
        public float RentGrowthPerDay = 6f;
        public float WagePerAssistant = 55f;

        /// <summary>How many assistants are on the payroll.</summary>
        public int Staff { get; private set; }

        public void SetStaff(int count)
        {
            Staff = Mathf.Max(0, count);
            OnStaffChanged.Invoke(Staff);
        }

        /// <summary>
        /// Actual payroll, summed from the people you hired. Falls back to the flat rate for
        /// a save loaded from before individual wages existed.
        /// </summary>
        public float DailyWages => _payroll > 0f ? _payroll : Staff * WagePerAssistant;

        private float _payroll;

        public void SetPayroll(float total)
        {
            _payroll = Mathf.Max(0f, total);
        }

        // Events
        public UnityEvent<float>       OnBalanceChanged    = new();
        public UnityEvent<float>       OnReputationChanged = new();
        public UnityEvent<int>         OnDayAdvanced       = new();
        public UnityEvent<SaleRecord>  OnSaleCompleted     = new();
        public UnityEvent<string, int> OnStockChanged      = new();
        public UnityEvent<float>       OnPriceChanged      = new();
        public UnityEvent<int>         OnStaffChanged      = new();
        public UnityEvent<SupplierOrder> OnOrderPlaced      = new();
        public UnityEvent<SupplierOrder> OnDeliveryArrived  = new();

        // Runtime state
        public float Balance    { get; private set; }
        public float Reputation { get; private set; }
        public int   Day        { get; private set; } = 1;

        /// <summary>Cash taken so far today. Drives the HUD ticker.</summary>
        public float EarnedToday { get; private set; }

        /// <summary>Cash paid out so far today (restocking, vet bills, wages already drawn).</summary>
        public float SpentToday  { get; private set; }

        public IReadOnlyDictionary<string, int> Stock => _stock;
        public IReadOnlyList<SaleRecord> TodaysSales  => _dayLog;

        /// <summary>What a customer actually pays for something listed at <paramref name="listed"/>.</summary>
        public float PriceOf(float listed) => listed * PriceMultiplier;

        /// <summary>
        /// How willing shoppers are to buy at the current markup. 1 at normal prices, falling
        /// away steeply as you get greedy and rising when you undercut.
        /// </summary>
        public float DemandFactor => Mathf.Clamp(Mathf.Pow(1f / PriceMultiplier, 1.6f), 0.25f, 1.6f);

        public void SetPriceMultiplier(float value)
        {
            PriceMultiplier = Mathf.Clamp(value, 0.6f, 1.8f);
            OnPriceChanged.Invoke(PriceMultiplier);
        }

        /// <summary>Rent charged at the end of the current day.</summary>
        public float DailyRent => BaseDailyRent + RentGrowthPerDay * (Day - 1);

        /// <summary>Everything owed at close of business.</summary>
        public float DailyOutgoings => DailyRent + DailyWages;

        private readonly Dictionary<string, int>          _stock     = new();
        private readonly List<SupplierOrder>              _orders    = new();
        private readonly Dictionary<ProductCategory, int> _warehouse = new();
        private readonly List<SaleRecord>        _dayLog = new();
        private float _dayOpeningBalance;

        private void Awake()
        {
            Balance            = StartingBalance;
            Reputation         = Mathf.Clamp(StartingReputation, 0f, 100f);
            _dayOpeningBalance = Balance;
        }

        // ── Balance ─────────────────────────────────────────────────────────────

        /// <summary>Applies a delta. Refuses a withdrawal that would overdraw the account.</summary>
        public bool ChangeBalance(float delta, string reason = "")
        {
            if (delta < 0f && Balance + delta < 0f)
            {
                Debug.Log($"[ShopManager] Insufficient funds for '{reason}': need {-delta:F2}, have {Balance:F2}");
                return false;
            }
            Balance += delta;
            if (delta >= 0f) EarnedToday += delta;
            else             SpentToday  += -delta;
            OnBalanceChanged.Invoke(Balance);
            return true;
        }

        /// <summary>Forces the balance, including into the red. Used by rent and save loading.</summary>
        public void SetBalance(float value)
        {
            Balance = value;
            OnBalanceChanged.Invoke(Balance);
        }

        public void SetReputation(float value)
        {
            Reputation = Mathf.Clamp(value, 0f, 100f);
            OnReputationChanged.Invoke(Reputation);
        }

        public void SetDay(int day)
        {
            Day = Mathf.Max(1, day);
            OnDayAdvanced.Invoke(Day);
        }

        public void ChangeReputation(float delta)
        {
            float before = Reputation;
            Reputation = Mathf.Clamp(Reputation + delta, 0f, 100f);
            if (!Mathf.Approximately(Reputation, before))
                OnReputationChanged.Invoke(Reputation);
        }

        // ── Stock ledger ────────────────────────────────────────────────────────

        public bool ChangeStock(string itemId, int delta)
        {
            _stock.TryGetValue(itemId, out int current);
            int next = current + delta;
            if (next < 0) return false;
            _stock[itemId] = next;
            OnStockChanged.Invoke(itemId, next);
            return true;
        }

        public int GetStock(string itemId) => _stock.TryGetValue(itemId, out int qty) ? qty : 0;

        public bool Restock(string itemId, int quantity, float unitCost)
        {
            if (!ChangeBalance(-unitCost * quantity, $"Restock {quantity}x {itemId}")) return false;
            ChangeStock(itemId, quantity);
            return true;
        }

        // ── Sales ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Credits a sale and adds it to today's log. Physical stock lives on the shelf,
        /// which has already been decremented by the time this is called.
        /// </summary>
        public SaleRecord RecordSale(string itemId, string label, int quantity, float unitPrice,
                                     string buyerName = "Customer")
        {
            float revenue = unitPrice * quantity;
            ChangeBalance(revenue, $"Sale of {quantity}x {itemId}");

            var record = new SaleRecord
            {
                Day       = Day,
                ItemId    = itemId,
                Label     = string.IsNullOrEmpty(label) ? itemId : label,
                Quantity  = quantity,
                UnitPrice = unitPrice,
                Revenue   = revenue,
                BuyerName = buyerName,
            };
            _dayLog.Add(record);
            OnSaleCompleted.Invoke(record);
            return record;
        }

        /// <summary>Sale that also draws down the stock ledger. Fails when stock is short.</summary>
        public bool TrySell(string itemId, int quantity, float unitPrice, string buyerName = "Customer")
        {
            if (!ChangeStock(itemId, -quantity)) return false;
            RecordSale(itemId, itemId, quantity, unitPrice, buyerName);
            ChangeReputation(0.5f);
            return true;
        }

        // ── Supplier orders ─────────────────────────────────────────────────────

        /// <summary>Orders placed today that have not yet been delivered.</summary>
        public IReadOnlyList<SupplierOrder> Orders => _orders;

        /// <summary>What a unit costs when ordered from the wholesaler, ahead of time.</summary>
        public const float WholesaleDiscount = 0.78f;

        /// <summary>
        /// Buying off the shelf at the cash-and-carry when you have run out. Deliberately
        /// dearer than ordering: planning ahead is supposed to be worth something.
        /// </summary>
        public const float EmergencyMarkup = 1.45f;

        /// <summary>Units delivered and waiting in the stockroom for this category.</summary>
        public int Warehouse(ProductCategory category) =>
            _warehouse.TryGetValue(category, out int units) ? units : 0;

        public int WarehouseTotal
        {
            get { int t = 0; foreach (var kv in _warehouse) t += kv.Value; return t; }
        }

        public void AddToWarehouse(ProductCategory category, int units)
        {
            if (units <= 0) return;
            _warehouse[category] = Warehouse(category) + units;
        }

        /// <summary>Draws up to <paramref name="units"/> from the stockroom; returns what it got.</summary>
        public int TakeFromWarehouse(ProductCategory category, int units)
        {
            int available = Warehouse(category);
            int taken     = Mathf.Min(available, Mathf.Max(0, units));
            if (taken > 0) _warehouse[category] = available - taken;
            return taken;
        }

        /// <summary>
        /// Pays for a delivery up front and schedules it to arrive later in the day. Returns
        /// null when the shop cannot afford it.
        /// </summary>
        public SupplierOrder PlaceOrder(ProductCategory category, int units, float unitCost,
                                        float dayProgressNow)
        {
            if (units <= 0) return null;

            float cost = unitCost * WholesaleDiscount * units;
            if (!ChangeBalance(-cost, $"Order {units}x {category}")) return null;

            var order = new SupplierOrder
            {
                Category        = category,
                Units           = units,
                Cost            = cost,
                ArrivalProgress = Mathf.Min(0.97f, dayProgressNow + UnityEngine.Random.Range(0.10f, 0.22f)),
            };
            _orders.Add(order);
            OnOrderPlaced.Invoke(order);
            return order;
        }

        /// <summary>Called each frame by the day clock; raises deliveries as they fall due.</summary>
        public void PollDeliveries(float dayProgress)
        {
            for (int i = _orders.Count - 1; i >= 0; i--)
            {
                var order = _orders[i];
                if (order.Delivered || dayProgress < order.ArrivalProgress) continue;

                order.Delivered = true;
                _orders.RemoveAt(i);
                OnDeliveryArrived.Invoke(order);
            }
        }

        // ── Day cycle ───────────────────────────────────────────────────────────

        /// <summary>
        /// Closes the books: charges rent, builds the summary, then rolls over to the next
        /// day. The summary reports the day that just ended.
        /// </summary>
        public DaySummary CloseDay()
        {
            float rent  = DailyRent;
            float wages = DailyWages;
            SetBalance(Balance - rent - wages);   // this can push you into the red — the fail state

            var summary = BuildSummary(rent, wages);

            // Anything still on the van turns up overnight rather than vanishing.
            foreach (var order in _orders) AddToWarehouse(order.Category, order.Units);
            _orders.Clear();

            SpentToday += rent + wages;

            _dayLog.Clear();
            Day++;
            EarnedToday = 0f;
            SpentToday  = 0f;
            _dayOpeningBalance = Balance;
            OnDayAdvanced.Invoke(Day);

            Debug.Log($"[ShopManager] Day {summary.Day} closed — revenue €{summary.TotalRevenue:F2}, " +
                      $"rent €{rent:F2}, wages €{wages:F2}");
            return summary;
        }

        public DaySummary GetCurrentDaySummary() => BuildSummary(0f, 0f);

        private DaySummary BuildSummary(float rent, float wages)
        {
            float revenue = 0f;
            int   units   = 0;
            foreach (var r in _dayLog) { revenue += r.Revenue; units += r.Quantity; }

            return new DaySummary
            {
                Day            = Day,
                TotalRevenue   = revenue,
                TotalUnits     = units,
                SaleCount      = _dayLog.Count,
                Rent           = rent,
                Wages          = wages,
                NetChange      = Balance - _dayOpeningBalance,
                Reputation     = Reputation,
                ClosingBalance = Balance,
                Spend          = SpentToday,
                Records        = new List<SaleRecord>(_dayLog),
            };
        }
    }

    /// <summary>A wholesale order paid for now and delivered to the forecourt later today.</summary>
    [Serializable]
    public class SupplierOrder
    {
        public ProductCategory Category;
        public int             Units;
        public float           Cost;
        /// <summary>Point in the trading day (0–1) at which the van pulls up.</summary>
        public float           ArrivalProgress;
        public bool            Delivered;
    }

    [Serializable]
    public class SaleRecord
    {
        public int    Day;
        public string ItemId;
        public string Label;
        public int    Quantity;
        public float  UnitPrice;
        public float  Revenue;
        public string BuyerName;
    }

    [Serializable]
    public class DaySummary
    {
        public int              Day;
        public float            TotalRevenue;
        public int              TotalUnits;
        public int              SaleCount;
        public float            Rent;
        public float            Wages;
        public float            NetChange;
        public float            Reputation;
        public float            ClosingBalance;
        public float            Spend;
        public List<SaleRecord> Records = new();
    }
}
