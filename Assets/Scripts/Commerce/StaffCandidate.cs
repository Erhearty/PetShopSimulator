using UnityEngine;

namespace PetShop.Commerce
{
    /// <summary>
    /// Someone looking for work in the shop. Candidates are generated fresh each morning,
    /// so a green expensive pool today may be a skilled cheap one tomorrow — checking the board
    /// is worth doing.
    ///
    /// Skill and wage are correlated but noisy: the trade-off is the decision, and the odd
    /// bargain (or dud) is what makes reading the cards worthwhile.
    /// </summary>
    public class StaffCandidate
    {
        /// <summary>Lowest skill level an assistant can have.</summary>
        public const int MinSkill = 1;
        /// <summary>Highest skill level an assistant can have.</summary>
        public const int MaxSkill = 5;

        /// <summary>Seconds per customer at <see cref="MinSkill"/>.</summary>
        private const float SlowestServiceSeconds = 6.5f;
        /// <summary>Seconds per customer at <see cref="MaxSkill"/>.</summary>
        private const float FastestServiceSeconds = 2.2f;
        /// <summary>Daily wage asked at <see cref="MinSkill"/>, before jitter.</summary>
        private const float LowestWage = 38f;
        /// <summary>Daily wage asked at <see cref="MaxSkill"/>, before jitter.</summary>
        private const float HighestWage = 86f;
        /// <summary>Wage noise either side of the skill rate (0.15 = ±15%).</summary>
        private const float WageJitter = 0.15f;
        /// <summary>Sign-on fee range as a multiple of the daily wage.</summary>
        private const float MinSignOnFactor = 0.8f;
        private const float MaxSignOnFactor = 1.6f;

        /// <summary>Full name shown on the card and in notifications.</summary>
        public string Name;
        /// <summary>Seconds to ring up one customer; the player is instant.</summary>
        public float  ServiceSeconds;
        /// <summary>Wage drawn at every close of business.</summary>
        public float  DailyWage;
        /// <summary>One-off cost to take them on.</summary>
        public float  SignOnFee;
        /// <summary>How good they are at the job, <see cref="MinSkill"/>..<see cref="MaxSkill"/>.</summary>
        public int    Skill = MinSkill;
        /// <summary>The job they applied for; can be changed after hiring.</summary>
        public StaffRole Role = StaffRole.Cashier;

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

        /// <summary>A plausible applicant: more skilled hands cost more, with room for luck.</summary>
        public static StaffCandidate Generate()
        {
            // 0 = plodding, 1 = expert. Wage tracks skill, plus noise in both directions.
            int   skill = SkillFromQuality(Random.value);
            float wage  = WageForSkill(skill) * Random.Range(1f - WageJitter, 1f + WageJitter);
            var   roles = (StaffRole[])System.Enum.GetValues(typeof(StaffRole));

            return new StaffCandidate
            {
                Name           = $"{FirstNames[Random.Range(0, FirstNames.Length)]} " +
                                 $"{Surnames[Random.Range(0, Surnames.Length)]}",
                Skill          = skill,
                Role           = roles[Random.Range(0, roles.Length)],
                ServiceSeconds = ServiceSecondsForSkill(skill),
                DailyWage      = Mathf.Round(wage),
                SignOnFee      = Mathf.Round(wage * Random.Range(MinSignOnFactor, MaxSignOnFactor)),
            };
        }

        /// <summary>Maps a 0..1 quality roll onto a whole skill level.</summary>
        public static int SkillFromQuality(float quality) =>
            Mathf.Clamp(MinSkill + Mathf.FloorToInt(Mathf.Clamp01(quality) * MaxSkill), MinSkill, MaxSkill);

        /// <summary>Seconds per customer for <paramref name="skill"/>, rounded to a tenth.</summary>
        public static float ServiceSecondsForSkill(int skill) =>
            Mathf.Round(Mathf.Lerp(SlowestServiceSeconds, FastestServiceSeconds, SkillFraction(skill)) * 10f) / 10f;

        /// <summary>Daily wage asked at <paramref name="skill"/>, before jitter.</summary>
        public static float WageForSkill(int skill) =>
            Mathf.Lerp(LowestWage, HighestWage, SkillFraction(skill));

        /// <summary>0 at <see cref="MinSkill"/>, 1 at <see cref="MaxSkill"/>.</summary>
        private static float SkillFraction(int skill) =>
            (Mathf.Clamp(skill, MinSkill, MaxSkill) - MinSkill) / (float)(MaxSkill - MinSkill);

        /// <summary>
        /// Skill as a row of dots, the earned ones bright and the rest dimmed (TextMeshPro rich text).
        /// Dots rather than stars: the UI font has no ★/☆ glyphs, so stars drew as empty boxes.
        /// </summary>
        public static string Stars(int skill)
        {
            int filled = Mathf.Clamp(skill, MinSkill, MaxSkill);
            return new string('•', filled) + "<alpha=#40>" + new string('•', MaxSkill - filled) + "<alpha=#FF>";
        }

        /// <summary>Plain-language skill level, for cards and the payroll.</summary>
        public static string SkillWordFor(int skill) =>
            Localization.Loc.T($"staff.skill.{Mathf.Clamp(skill, MinSkill, MaxSkill)}");

        /// <summary>Plain-language skill level of this applicant.</summary>
        public string SkillWord => SkillWordFor(Skill);

        /// <summary>Plain-language speed, so the player need not reason about seconds.</summary>
        public string SpeedWord =>
            Localization.Loc.T(ServiceSeconds <= 3.0f ? "staff.speed.very_quick"
                             : ServiceSeconds <= 4.0f ? "staff.speed.quick"
                             : ServiceSeconds <= 5.2f ? "staff.speed.steady"
                             :                          "staff.speed.slow");

        /// <summary>
        /// Customers served per minute of *game* time. A trading day is compressed into a few
        /// minutes, so this reads absurdly high to a player — use <see cref="ServiceSeconds"/>
        /// in the UI and keep this for balance maths.
        /// </summary>
        public float ServedPerMinute => 60f / Mathf.Max(0.5f, ServiceSeconds);
    }
}
