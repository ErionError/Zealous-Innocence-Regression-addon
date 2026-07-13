using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZealousInnocenceRegression.Patches
{
    /// <summary>
    /// Adds a pottyProgress-based additive bonus to ZI's BladderControl capacity
    /// AFTER ZI's full pipeline (age factor × strength × sleep factor) has run.
    /// Intentionally applied outside the sleep-factor chain so training helps
    /// daytime control only — bedwetting is managed by a separate path in ZI.
    /// </summary>
    public static class Patch_BladderControl
    {
        public static void Register(Harmony harmony)
        {
            var ziType = AccessTools.TypeByName("ZealousInnocence.PawnCapacityWorker_BladderControl");
            if (ziType == null)
            {
                Log.Warning("[ZIR] PawnCapacityWorker_BladderControl not found — BladderControl bonus skipped.");
                return;
            }

            var method = AccessTools.Method(ziType, "CalculateCapacityLevel");
            if (method == null)
            {
                Log.Warning("[ZIR] PawnCapacityWorker_BladderControl.CalculateCapacityLevel not found — BladderControl bonus skipped.");
                return;
            }

            harmony.Patch(method,
                postfix: new HarmonyMethod(typeof(Patch_BladderControl), nameof(Postfix)));
            Log.Message("[ZIR] Patched PawnCapacityWorker_BladderControl.CalculateCapacityLevel for potty bonus.");
        }

        // HediffSet is the first param of PawnCapacityWorker.CalculateCapacityLevel.
        public static void Postfix(HediffSet diffSet, ref float __result)
        {
            Pawn pawn = diffSet?.pawn;
            if (pawn?.needs == null) return;

            var reg = pawn.needs.TryGetNeed<Need_Regression>();
            if (reg == null || reg.CurStage == RegressionStage.Adult) return;

            // ZI's age pipeline uses its own RegressionState hediff (not ours), so it
            // computes adult-level BladderControl for our regressed pawns. Instead of
            // patching around that, we REPLACE the result entirely with a stage-envelope
            // driven by pottyProgress:
            //   floor  = control with zero training (stage-appropriate minimum)
            //   ceiling = control with full training (stage-appropriate maximum)
            // pottyProgress lerps between them, giving a principled range per stage.
            float floor = reg.CurStage switch
            {
                RegressionStage.Infant     => 0.02f,
                RegressionStage.Toddler    => 0.10f,
                RegressionStage.YoungChild => 0.30f,
                RegressionStage.PreTeen    => 0.55f,
                _                          => __result
            };
            float ceiling = reg.CurStage switch
            {
                RegressionStage.Infant     => 0.20f,
                RegressionStage.Toddler    => 0.45f,
                RegressionStage.YoungChild => 0.70f,
                RegressionStage.PreTeen    => 0.90f,
                _                          => __result
            };

            __result = Mathf.Lerp(floor, ceiling, reg.pottyProgress);
        }
    }
}
