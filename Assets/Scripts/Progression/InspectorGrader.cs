namespace PetShop.Progression
{
    /// <summary>The inspector's verdict and its reputation and cash consequences.</summary>
    public struct InspectionResult
    {
        /// <summary>The grade awarded: 'A', 'B', 'C' or 'F'.</summary>
        public char Grade;

        /// <summary>Reputation change, positive or negative.</summary>
        public float ReputationDelta;

        /// <summary>Cash change: a grant when positive, a fine when negative.</summary>
        public float CashDelta;

        /// <summary>One-line, player-facing description of the verdict.</summary>
        public string Summary;
    }

    /// <summary>
    /// Pure grading for the weekly welfare inspection. Empty inputs count as full marks:
    /// with no pets the caller passes 1 for pet health, with no occupied pens 1 for pen care,
    /// and with no shelves 0 for the empty-shelf fraction, so an empty shop is never penalised.
    /// </summary>
    public static class InspectorGrader
    {
        /// <summary>The inspector calls on every day that is a multiple of this.</summary>
        public const int InspectionIntervalDays = 7;

        /// <summary>Share of the score from average pet health.</summary>
        public const float HealthWeight = 0.4f;

        /// <summary>Share of the score from average pen care (min of food and cleanliness).</summary>
        public const float PenCareWeight = 0.4f;

        /// <summary>Share of the score from stocked shelves (1 − empty-shelf fraction).</summary>
        public const float ShelfWeight = 0.2f;

        /// <summary>Minimum 0..1 score for an A.</summary>
        public const float GradeAScore = 0.85f;

        /// <summary>Minimum 0..1 score for a B.</summary>
        public const float GradeBScore = 0.7f;

        /// <summary>Minimum 0..1 score for a C. Anything lower is an F.</summary>
        public const float GradeCScore = 0.5f;

        /// <summary>Reputation for an A.</summary>
        public const float GradeAReputation = 8f;
        /// <summary>Cash grant for an A.</summary>
        public const float GradeACash = 150f;
        /// <summary>Reputation for a B.</summary>
        public const float GradeBReputation = 3f;
        /// <summary>Cash grant for a B.</summary>
        public const float GradeBCash = 50f;
        /// <summary>Reputation for a C.</summary>
        public const float GradeCReputation = -3f;
        /// <summary>Fine for a C.</summary>
        public const float GradeCCash = -75f;
        /// <summary>Reputation for an F.</summary>
        public const float GradeFReputation = -8f;
        /// <summary>Fine for an F.</summary>
        public const float GradeFCash = -200f;

        /// <summary>True when the inspector visits at the close of <paramref name="day"/>.</summary>
        public static bool IsInspectionDay(int day) => day > 0 && day % InspectionIntervalDays == 0;

        /// <summary>
        /// Weighted 0..1 score. Inputs are clamped to 0..1. Pass 1/1/0 for a shop with no pets,
        /// pens or shelves to award full marks.
        /// </summary>
        public static float Score(float avgPetHealth, float avgPenCare, float emptyShelfFraction) =>
            HealthWeight  * Clamp01(avgPetHealth)
          + PenCareWeight * Clamp01(avgPenCare)
          + ShelfWeight   * (1f - Clamp01(emptyShelfFraction));

        /// <summary>
        /// Grades a shop. <paramref name="avgPenCare"/> is the mean of min(FoodLevel, Cleanliness)
        /// over occupied pens, computed by the caller. Empty inputs count as full marks (see class).
        /// </summary>
        public static InspectionResult Grade(float avgPetHealth, float avgPenCare, float emptyShelfFraction)
        {
            float score = Score(avgPetHealth, avgPenCare, emptyShelfFraction);
            if (score >= GradeAScore) return Result('A', GradeAReputation, GradeACash);
            if (score >= GradeBScore) return Result('B', GradeBReputation, GradeBCash);
            if (score >= GradeCScore) return Result('C', GradeCReputation, GradeCCash);
            return Result('F', GradeFReputation, GradeFCash);
        }

        private static InspectionResult Result(char grade, float reputation, float cash) => new InspectionResult
        {
            Grade           = grade,
            ReputationDelta = reputation,
            CashDelta       = cash,
            Summary         = $"Inspector's visit: grade {grade} — reputation {reputation:+0;-0}, cash {CashText(cash)}",
        };

        private static string CashText(float delta) =>
            delta >= 0f ? $"+€{delta:N0}" : $"−€{-delta:N0}";

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
