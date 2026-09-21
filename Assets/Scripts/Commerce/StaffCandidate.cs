using UnityEngine;

namespace PetShop.Commerce
{
    /// <summary>
    /// Someone looking for work behind the till. Candidates are generated fresh each morning,
    /// so a slow expensive pool today may be a fast cheap one tomorrow — checking the board is
    /// worth doing.
    ///
    /// Speed and wage are correlated but noisy: the trade-off is the decision, and the odd
    /// bargain (or dud) is what makes reading the cards worthwhile.
    /// </summary>
    public class StaffCandidate
    {
        public string Name;
        /// <summary>Seconds to ring up one customer; the player is instant.</summary>
        public float  ServiceSeconds;
        public float  DailyWage;
        /// <summary>One-off cost to take them on.</summary>
        public float  SignOnFee;

        private static readonly string[] FirstNames =
        {
            "Mara", "Dev", "Otto", "Priya", "Sam", "Lena", "Kofi", "Rosa",
            "Nils", "Ines", "Tomas", "Ada", "Ravi", "Greta", "Yusuf", "Bea",
        };

        private static readonly string[] Surnames =
        {
            "Hale", "Okoro", "Berg", "Nunes", "Cole", "Farag", "Lindqvist", "Adeyemi",
            "Novak", "Reyes", "Duval", "Keane",
        };

        /// <summary>A plausible applicant: faster hands cost more, with room for luck.</summary>
        public static StaffCandidate Generate()
        {
            // 0 = plodding, 1 = quick. Wage tracks it, plus noise in both directions.
            float quality = Random.value;

            float service = Mathf.Lerp(6.5f, 2.2f, quality);
            float wage    = Mathf.Lerp(38f, 86f, quality) * Random.Range(0.85f, 1.15f);

            return new StaffCandidate
            {
                Name           = $"{FirstNames[Random.Range(0, FirstNames.Length)]} " +
                                 $"{Surnames[Random.Range(0, Surnames.Length)]}",
                ServiceSeconds = Mathf.Round(service * 10f) / 10f,
                DailyWage      = Mathf.Round(wage),
                SignOnFee      = Mathf.Round(wage * Random.Range(0.8f, 1.6f)),
            };
        }

        /// <summary>Plain-language speed, so the player need not reason about seconds.</summary>
        public string SpeedWord =>
            ServiceSeconds <= 3.0f ? "very quick"
          : ServiceSeconds <= 4.0f ? "quick"
          : ServiceSeconds <= 5.2f ? "steady"
          :                          "slow";

        /// <summary>
        /// Customers served per minute of *game* time. A trading day is compressed into a few
        /// minutes, so this reads absurdly high to a player — use <see cref="ServiceSeconds"/>
        /// in the UI and keep this for balance maths.
        /// </summary>
        public float ServedPerMinute => 60f / Mathf.Max(0.5f, ServiceSeconds);
    }
}
