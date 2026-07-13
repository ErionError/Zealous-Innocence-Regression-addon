using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ZealousInnocenceRegression.Patches
{
    /// <summary>
    /// Postfix on ZI's ThoughtWorker_Diaper_Dressed.CurrentStateInternal.
    /// For regressed adult pawns, overrides ZI's stage selection entirely and
    /// computes: 45 + fillState * 20 + traitIdx * 5 + regressionIdx
    ///   fillState:    NoDiaper=0  Clean=1  Used=2  Spent=3  Trashed=4
    ///   traitIdx:     Default=0   BigBoy=1  PottyRebel=2  DiaperLover=3
    ///   regressionIdx: Adult=0   PreTeen=1  YoungChild=2  Toddler=3  Infant=4
    ///
    /// Fill state is read directly from ZI's Need_Diaper and Helper_Diaper
    /// via cached reflection so we don't depend on ZI's stage mapping.
    /// </summary>
    public static class Patch_DiaperThoughts
    {
        private const int ZIRBase = 45;

        // Cached ZI reflection targets — populated once at Register time.
        private static MethodInfo   _getDiaper;
        private static MethodInfo   _needsDiaper;
        private static PropertyInfo _curCategory;

        public static void Register(HarmonyLib.Harmony harmony)
        {
            var ziWorkerType = AccessTools.TypeByName("ZealousInnocence.ThoughtWorker_Diaper_Dressed");
            if (ziWorkerType == null)
            {
                Log.Warning("[ZIR] ThoughtWorker_Diaper_Dressed not found — diaper thought scaling skipped.");
                return;
            }

            var method = AccessTools.Method(ziWorkerType, "CurrentStateInternal");
            if (method == null)
            {
                Log.Warning("[ZIR] ThoughtWorker_Diaper_Dressed.CurrentStateInternal not found.");
                return;
            }

            // Cache Helper_Diaper methods
            var helperType = AccessTools.TypeByName("ZealousInnocence.Helper_Diaper");
            if (helperType != null)
            {
                _getDiaper   = AccessTools.Method(helperType, "getDiaper");
                _needsDiaper = AccessTools.Method(helperType, "needsDiaper");
            }
            else
            {
                Log.Warning("[ZIR] ZealousInnocence.Helper_Diaper not found — diaper fill state fallback active.");
            }

            // Cache Need_Diaper.CurCategory property
            var needDiaperType = AccessTools.TypeByName("ZealousInnocence.Need_Diaper");
            if (needDiaperType != null)
                _curCategory = AccessTools.Property(needDiaperType, "CurCategory");

            harmony.Patch(method,
                postfix: new HarmonyMethod(typeof(Patch_DiaperThoughts), nameof(Postfix)));

            Log.Message("[ZIR] Patched ThoughtWorker_Diaper_Dressed.CurrentStateInternal");
        }

        public static void Postfix(Pawn p, ref ThoughtState __result)
        {
            if (p.DevelopmentalStage != DevelopmentalStage.Adult) return;

            var regression = p.needs?.TryGetNeed<Need_Regression>();
            if (regression == null) return;

            int fillState = ComputeFillState(p);
            if (fillState < 0)
            {
                // No diaper and doesn't need one — suppress ZI's underwear/nighttime thoughts.
                __result = ThoughtState.Inactive;
                return;
            }

            int traitIdx = ZIR_ThoughtUtility.GetTraitOffset(p);
            int rIdx     = (int)regression.CurStage;

            __result = ThoughtState.ActiveAtStage(ZIRBase + fillState * 20 + traitIdx * 5 + rIdx);
        }

        /// <summary>
        /// Returns the fill state index by reading ZI's Need_Diaper directly.
        ///   0 = no diaper, but pawn needs one
        ///  -1 = no diaper, not needed (suppress)
        ///   1 = Clean
        ///   2 = Used
        ///   3 = Spent
        ///   4 = Trashed
        /// Falls back to ZI's stage index if reflection is unavailable.
        /// </summary>
        private static int ComputeFillState(Pawn p)
        {
            // ── reflection path (primary) ──────────────────────────────────
            if (_getDiaper != null)
            {
                var diaper = _getDiaper.Invoke(null, new object[] { p }) as Apparel;

                if (diaper == null)
                {
                    if (_needsDiaper == null) return -1;
                    bool needs = (bool)(_needsDiaper.Invoke(null, new object[] { p }) ?? false);
                    return needs ? 0 : -1;
                }

                // Read CurCategory from Need_Diaper
                if (_curCategory != null)
                {
                    Need diaperNeed = FindNeedByTypeName(p, "Need_Diaper");
                    if (diaperNeed != null)
                    {
                        int cat = Convert.ToInt32(_curCategory.GetValue(diaperNeed));
                        // DiaperSituationCategory: Trashed=0, Spent=1, Used=2, Clean=3
                        // fillState: NoDiaper=0, Clean=1, Used=2, Spent=3, Trashed=4
                        return 4 - cat;
                    }
                }

                // Need_Diaper found but CurCategory unavailable — default to Clean
                return 1;
            }

            // ── fallback: never happens if ZI is loaded correctly ──────────
            return -1;
        }

        private static Need FindNeedByTypeName(Pawn p, string typeName)
        {
            if (p.needs?.AllNeeds == null) return null;
            foreach (Need n in p.needs.AllNeeds)
                if (n.GetType().Name == typeName) return n;
            return null;
        }
    }
}
