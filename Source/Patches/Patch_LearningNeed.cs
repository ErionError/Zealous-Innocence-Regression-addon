using HarmonyLib;
using RimWorld;
using Verse;

namespace ZealousInnocenceRegression.Patches
{
    /// <summary>
    /// Forces Need_Learning to be active on adult pawns that are regressed below Adult stage.
    /// Registered manually (private method, string targeting).
    /// </summary>
    public static class Patch_LearningNeed
    {
        // Cached reflection accessor — much faster than Traverse on every call.
        private static readonly System.Reflection.FieldInfo _pawnField =
            AccessTools.Field(typeof(Pawn_NeedsTracker), "pawn");

        public static void Register(Harmony harmony)
        {
            if (!ModsConfig.BiotechActive) return;

            // ── ShouldHaveNeed: enable Need_Learning for our regressed adults ──
            var shouldHaveNeed = AccessTools.Method(typeof(Pawn_NeedsTracker), "ShouldHaveNeed");
            if (shouldHaveNeed == null)
            {
                Log.Warning("[ZIR] Could not find Pawn_NeedsTracker.ShouldHaveNeed — learning activation skipped.");
                return;
            }
            harmony.Patch(shouldHaveNeed,
                postfix: new HarmonyMethod(typeof(Patch_LearningNeed), nameof(Postfix_ShouldHaveNeed)));
            Log.Message("[ZIR] Patched Pawn_NeedsTracker.ShouldHaveNeed for learning activation.");

            // ── ZI ShouldHaveLearning: return true for our PreTeen/YoungChild pawns ──
            // ZI's Need_Learning_IsFrozen postfix freezes the need (sets CurLevel=0.5,
            // __result=true) whenever ShouldHaveLearning() returns false. Since ZI uses
            // its own hediff-based age system (HediffDefOf.RegressionState) which doesn't
            // know about our Need_Regression, ShouldHaveLearning() always returns false for
            // our regressed adults. We postfix ShouldHaveLearning at Priority.Low so our
            // result runs AFTER ZI's own logic, ensuring the freeze branch is never entered
            // for our PreTeen and YoungChild stages.
            var ziType = AccessTools.TypeByName("ZealousInnocence.Helper_Regression");
            if (ziType != null)
            {
                var shouldHaveLearn = AccessTools.Method(ziType, "ShouldHaveLearning");
                if (shouldHaveLearn != null)
                {
                    var postfixMethod = new HarmonyMethod(
                        typeof(Patch_LearningNeed), nameof(Postfix_ShouldHaveLearning_ZI));
                    postfixMethod.priority = Priority.Low;
                    harmony.Patch(shouldHaveLearn, postfix: postfixMethod);
                    Log.Message("[ZIR] Patched ZI Helper_Regression.ShouldHaveLearning for learning un-freeze.");
                }
                else
                    Log.Warning("[ZIR] Could not find ZI Helper_Regression.ShouldHaveLearning.");
            }
            else
                Log.Warning("[ZIR] ZealousInnocence.Helper_Regression not found — learning un-freeze skipped.");
        }

        // ── ShouldHaveNeed postfix ────────────────────────────────────────────
        public static void Postfix_ShouldHaveNeed(Pawn_NeedsTracker __instance, NeedDef nd, ref bool __result)
        {
            if (__result) return;
            if (nd.needClass != typeof(Need_Learning)) return;

            Pawn pawn = (Pawn)_pawnField?.GetValue(__instance);
            if (pawn?.needs == null) return;

            var regression = pawn.needs.TryGetNeed<Need_Regression>();
            if (regression == null) return;

            // Enable learning for all regressed stages; Toddler/Infant will have
            // it frozen by the stage-appropriate logic below, but we still need
            // the need object present so the gizmo can read it.
            if (regression.CurStage != RegressionStage.Adult)
                __result = true;
        }

        // ── ZI ShouldHaveLearning postfix (Priority.Low) ──────────────────────
        // Extension methods take the Pawn as first parameter ("this" pawn = __instance
        // in static extension method terms, but Harmony sees it as first arg).
        public static void Postfix_ShouldHaveLearning_ZI(Pawn p, ref bool __result)
        {
            if (__result) return;           // ZI already says yes — leave it
            if (p?.needs == null) return;

            var reg = p.needs.TryGetNeed<Need_Regression>();
            if (reg == null) return;

            // Un-freeze learning for our PreTeen and YoungChild stages so ZI's
            // Need_Learning_IsFrozen postfix skips the CurLevel-reset branch.
            if (reg.CurStage == RegressionStage.PreTeen ||
                reg.CurStage == RegressionStage.YoungChild)
                __result = true;
        }
    }
}
