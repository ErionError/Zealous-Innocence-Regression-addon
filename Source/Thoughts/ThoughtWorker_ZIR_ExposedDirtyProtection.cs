using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Situational thought: pawn is wearing a dirty (used/spent/trashed) diaper
    /// that is not hidden by any bottom-covering clothes.
    ///
    /// 20 stages: 4 trait profiles × 5 regression stages
    /// Formula: traitIdx * 5 + regressionIdx
    ///
    /// Only fires when the diaper is NOT clean. ZIR_ExposedProtection suppresses
    /// itself when the diaper is dirty so only one of the two fires at a time.
    /// </summary>
    public class ThoughtWorker_ZIR_ExposedDirtyProtection : ThoughtWorker
    {
        // Cached reflection — populated once on first use.
        private static bool           _cacheReady;
        private static MethodInfo     _getDiaper;
        private static PropertyInfo   _curCategory;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p.DevelopmentalStage != DevelopmentalStage.Adult) return ThoughtState.Inactive;
            if (!ZIR_ApparelUtility.HasExposedProtection(p))       return ThoughtState.Inactive;

            var need = p.needs?.TryGetNeed<Need_Regression>();
            if (need == null) return ThoughtState.Inactive;

            if (!IsDirty(p)) return ThoughtState.Inactive;

            int rIdx    = (int)need.CurStage;
            int traitIdx = ZIR_ThoughtUtility.GetTraitOffset(p);

            return ThoughtState.ActiveAtStage(traitIdx * 5 + rIdx);
        }

        /// <summary>
        /// Returns true when the worn diaper is Used, Spent, or Trashed
        /// (DiaperSituationCategory: Trashed=0, Spent=1, Used=2, Clean=3).
        /// </summary>
        private static bool IsDirty(Pawn p)
        {
            EnsureCache();
            if (_getDiaper == null || _curCategory == null) return false;

            var diaper = _getDiaper.Invoke(null, new object[] { p }) as Apparel;
            if (diaper == null) return false;

            Need diaperNeed = FindNeedByTypeName(p, "Need_Diaper");
            if (diaperNeed == null) return false;

            int cat = Convert.ToInt32(_curCategory.GetValue(diaperNeed));
            // Clean=3 → not dirty. Trashed=0, Spent=1, Used=2 → dirty.
            return cat < 3;
        }

        private static void EnsureCache()
        {
            if (_cacheReady) return;
            _cacheReady = true;

            var helperType = AccessTools.TypeByName("ZealousInnocence.Helper_Diaper");
            if (helperType != null)
                _getDiaper = AccessTools.Method(helperType, "getDiaper");

            var needDiaperType = AccessTools.TypeByName("ZealousInnocence.Need_Diaper");
            if (needDiaperType != null)
                _curCategory = AccessTools.Property(needDiaperType, "CurCategory");
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
