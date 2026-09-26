using System;
using System.Collections.Generic;

namespace PetShop.Commerce
{
    /// <summary>One sale in the daily sales log.</summary>
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

    /// <summary>The closed books of one trading day, shown on the day results panel.</summary>
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
        /// <summary>Notable events of the day — inspection results, reputation milestones.</summary>
        public List<string>     Headlines = new();
    }
}
