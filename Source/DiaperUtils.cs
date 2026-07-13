using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    public static class DiaperUtils
    {
        // Accident base penalty (before kind multiplier)
        public const float AccidentBase = -0.07f;
        // Ruin multiplier on top of accident base
        public const float RuinMultiplier = 1.25f;

        // Hourly threshold penalties (before kind multiplier)
        // Tuned so a standard fresh diaper takes ~15 in-game days to cross Adult→Pre-Teen
        public const float PenaltyFresh       = -0.0007f; // HP > 50%
        public const float PenaltyLightlyUsed = -0.0010f; // HP 20-50%
        public const float PenaltyHeavilyUsed = -0.0020f; // HP < 20%

        /// <summary>Returns the first worn diaper, or null.</summary>
        public static Apparel GetWornDiaper(Pawn pawn)
        {
            if (pawn?.apparel == null) return null;
            foreach (Apparel a in pawn.apparel.WornApparel)
                if (a.def?.apparel?.tags != null && a.def.apparel.tags.Contains("Diaper"))
                    return a;
            return null;
        }

        /// <summary>Kind-based regression multiplier by ThingDef (for ruin detection).</summary>
        public static float KindMultiplier(ThingDef def)
        {
            if (def == null) return 1f;
            return def.defName switch
            {
                "Apparel_Diaper_Night"      => 0.50f,
                "Apparel_Diaper_Flimsy"     => 1.00f,
                "Apparel_Diaper"            => 1.00f,
                "Apparel_Diaper_Disposable" => 1.00f,
                "Apparel_Premium_Diaper"    => 1.00f,
                "Apparel_Diaper_BabyDiaper" => 1.50f,
                _                           => 1.00f
            };
        }

        /// <summary>Kind-based regression multiplier.</summary>
        public static float KindMultiplier(Apparel diaper)
        {
            if (diaper == null) return 1f;
            return diaper.def.defName switch
            {
                "Apparel_Diaper_Night"      => 0.50f,
                "Apparel_Diaper_Flimsy"     => 0.75f,
                "Apparel_Diaper"            => 1.00f,
                "Apparel_Diaper_Disposable" => 1.00f,
                "Apparel_Premium_Diaper"    => 1.25f,
                "Apparel_Diaper_BabyDiaper" => 1.50f,
                _                           => 1.00f
            };
        }

        /// <summary>Hourly threshold penalty based on diaper HP fraction.</summary>
        public static float ThresholdPenalty(Apparel diaper)
        {
            if (diaper == null) return 0f;
            float hpFrac = (float)diaper.HitPoints / diaper.MaxHitPoints;
            float raw = hpFrac switch
            {
                > 0.50f => PenaltyFresh,
                > 0.20f => PenaltyLightlyUsed,
                _       => PenaltyHeavilyUsed
            };
            return raw * KindMultiplier(diaper);
        }
    }
}
