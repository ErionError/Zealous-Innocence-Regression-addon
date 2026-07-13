using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ZealousInnocenceRegression.Patches
{
    /// <summary>
    /// Postfixes on ZI's ThoughtWorker_Onesie_Dressed, ThoughtWorker_CribBed_Preferred,
    /// and ThoughtWorker_Stink. All redirect regressed adult observers/wearers to
    /// regression-indexed stages appended to the respective ThoughtDefs.
    ///
    /// Onesie  — appended stages 5–24.  Formula: 5  + traitIdx * 5 + rIdx
    /// CribBed — appended stages 3–22.  Formula: 3  + traitIdx * 5 + rIdx
    /// Stink   — appended stages 12–91. Formula: 12 + severityIdx * 20 + traitIdx * 5 + rIdx
    ///   (observer's trait + regression; Infant observers return Inactive)
    /// </summary>
    public static class Patch_ApparelThoughts
    {
        public static void Register(HarmonyLib.Harmony harmony)
        {
            RegisterOnesie(harmony);
            RegisterCribBed(harmony);
            RegisterStink(harmony);
        }

        // ── Onesie ────────────────────────────────────────────────────────────

        private static void RegisterOnesie(HarmonyLib.Harmony harmony)
        {
            var ziType = AccessTools.TypeByName("ZealousInnocence.ThoughtWorker_Onesie_Dressed");
            if (ziType == null)
            {
                Log.Warning("[ZIR] ThoughtWorker_Onesie_Dressed not found — onesie thought scaling skipped.");
                return;
            }
            var method = AccessTools.Method(ziType, "CurrentStateInternal");
            if (method == null) return;

            harmony.Patch(method,
                postfix: new HarmonyMethod(typeof(Patch_ApparelThoughts), nameof(OnesiePostfix)));
            Log.Message("[ZIR] Patched ThoughtWorker_Onesie_Dressed.CurrentStateInternal");
        }

        public static void OnesiePostfix(Pawn p, ref ThoughtState __result)
        {
            if (!__result.Active) return;
            if (p.DevelopmentalStage != DevelopmentalStage.Adult) return;

            var regression = p.needs?.TryGetNeed<Need_Regression>();
            if (regression == null) return;

            int traitIdx = ZIR_ThoughtUtility.GetTraitOffset(p);
            int rIdx     = (int)regression.CurStage;
            __result = ThoughtState.ActiveAtStage(5 + traitIdx * 5 + rIdx);
        }

        // ── CribBed ───────────────────────────────────────────────────────────

        private static void RegisterCribBed(HarmonyLib.Harmony harmony)
        {
            var ziType = AccessTools.TypeByName("ZealousInnocence.ThoughtWorker_CribBed_Preferred");
            if (ziType == null)
            {
                Log.Warning("[ZIR] ThoughtWorker_CribBed_Preferred not found — crib-bed thought scaling skipped.");
                return;
            }
            var method = AccessTools.Method(ziType, "CurrentStateInternal");
            if (method == null) return;

            harmony.Patch(method,
                postfix: new HarmonyMethod(typeof(Patch_ApparelThoughts), nameof(CribBedPostfix)));
            Log.Message("[ZIR] Patched ThoughtWorker_CribBed_Preferred.CurrentStateInternal");
        }

        public static void CribBedPostfix(Pawn p, ref ThoughtState __result)
        {
            if (!__result.Active) return;
            if (p.DevelopmentalStage != DevelopmentalStage.Adult) return;

            var regression = p.needs?.TryGetNeed<Need_Regression>();
            if (regression == null) return;

            int traitIdx = ZIR_ThoughtUtility.GetTraitOffset(p);
            int rIdx     = (int)regression.CurStage;
            __result = ThoughtState.ActiveAtStage(3 + traitIdx * 5 + rIdx);
        }

        // ── Stink ─────────────────────────────────────────────────────────────

        private static void RegisterStink(HarmonyLib.Harmony harmony)
        {
            var ziType = AccessTools.TypeByName("ZealousInnocence.ThoughtWorker_Stink");
            if (ziType == null)
            {
                Log.Warning("[ZIR] ThoughtWorker_Stink not found — stink thought scaling skipped.");
                return;
            }
            var method = AccessTools.Method(ziType, "CurrentStateInternal");
            if (method == null) return;

            // Use a PREFIX so we can replace ZI's result entirely for regressed adult
            // observers. ZI's formula (CurLevel - 0.4f) produces NEGATIVE contributions
            // for very dirty diapers (CurLevel < 0.4), which cancels out the stink score
            // and makes ThoughtState.Inactive fire exactly when the pawn smells worst.
            // Our HP-based score (0.6 - hpFrac, always positive when dirty) fixes this.
            harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(Patch_ApparelThoughts), nameof(StinkPrefix)));
            Log.Message("[ZIR] Patched ThoughtWorker_Stink.CurrentStateInternal (prefix)");
        }

        /// <summary>
        /// Replaces ZI's stink evaluation for regressed adult observers.
        /// Returns false (skip ZI) when the pawn is a regressed adult; true otherwise.
        /// </summary>
        public static bool StinkPrefix(Pawn p, ref ThoughtState __result)
        {
            var regression = p.needs?.TryGetNeed<Need_Regression>();
            if (regression == null || regression.CurStage == RegressionStage.Adult) return true;

            // Infant observers — suppress entirely, skip ZI too.
            if (regression.CurStage == RegressionStage.Infant)
            {
                __result = ThoughtState.Inactive;
                return false;
            }

            // Our own HP-based stink score: always positive as diaper gets dirtier,
            // unlike ZI's (CurLevel - 0.4f) which goes negative for very dirty diapers.
            float score = ComputeStinkScore(p);
            if (score <= 0f)
            {
                __result = ThoughtState.Inactive;
                return false;
            }

            // Three severity tiers matching ZI's thresholds.
            int severityIdx = score > 2.5f ? 2 : score > 1.2f ? 1 : 0;
            int traitIdx    = ZIR_ThoughtUtility.GetTraitOffset(p);
            int rIdx        = (int)regression.CurStage; // PreTeen=1, YoungChild=2, Toddler=3
            __result = ThoughtState.ActiveAtStage(12 + severityIdx * 20 + traitIdx * 5 + rIdx);
            return false;
        }

        /// <summary>
        /// HP-based stink score: sum of (0.6 - hpFrac) for dirty diapers within 6 cells.
        /// Always produces positive values when diapers are soiled — fixes ZI's negative
        /// contribution bug for very dirty diapers.
        /// </summary>
        private static float ComputeStinkScore(Pawn observer)
        {
            if (observer?.Map == null) return 0f;
            const float StinkRadius = 6f;
            const float StinkCutoff = 0.6f;
            float score = 0f;
            foreach (Pawn other in observer.Map.mapPawns.AllPawnsSpawned)
            {
                if (other == observer) continue;
                Apparel diaper = DiaperUtils.GetWornDiaper(other);
                if (diaper == null) continue;
                float hpFrac = (float)diaper.HitPoints / diaper.MaxHitPoints;
                if (hpFrac >= StinkCutoff) continue;
                if (!other.Position.InHorDistOf(observer.Position, StinkRadius)) continue;
                score += StinkCutoff - hpFrac;
            }
            return score;
        }
    }
}
