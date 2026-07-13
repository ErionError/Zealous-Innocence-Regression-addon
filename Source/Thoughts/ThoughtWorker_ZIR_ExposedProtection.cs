using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Situational thought: pawn is wearing a diaper / protective underwear
    /// that is not hidden by any bottom-covering clothes.
    ///
    /// 20 stages:  4 trait profiles × 5 regression stages
    ///   Profile offset:  Default=0  BigBoy=5  PottyRebel=10  DiaperLover=15
    ///   Stage  offset:   Adult=0  PreTeen=1  YoungChild=2  Toddler=3  Infant=4
    /// </summary>
    public class ThoughtWorker_ZIR_ExposedProtection : ThoughtWorker
    {
        // Cached reflection shared with ExposedDirtyProtection
        private static bool         _cacheReady;
        private static MethodInfo   _getDiaper;
        private static PropertyInfo _curCategory;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p.DevelopmentalStage != DevelopmentalStage.Adult) return ThoughtState.Inactive;
            if (!ZIR_ApparelUtility.HasExposedProtection(p))       return ThoughtState.Inactive;

            var need = p.needs?.TryGetNeed<Need_Regression>();
            if (need == null) return ThoughtState.Inactive;

            // Suppress in favour of ZIR_ExposedDirtyProtection when diaper is dirty.
            if (IsDirty(p)) return ThoughtState.Inactive;

            int rIdx = (int)need.CurStage;        // 0=Adult … 4=Infant

            int profile = 0;
            if (p.story?.traits != null)
            {
                if (ZIR_TraitDefOf.Big_Boy    != null && p.story.traits.HasTrait(ZIR_TraitDefOf.Big_Boy))
                    profile = 5;
                else if (ZIR_TraitDefOf.Potty_Rebel != null && p.story.traits.HasTrait(ZIR_TraitDefOf.Potty_Rebel))
                    profile = 10;
                else if (ZIR_TraitDefOf.Diaper_Lover != null && p.story.traits.HasTrait(ZIR_TraitDefOf.Diaper_Lover))
                    profile = 15;
            }

            return ThoughtState.ActiveAtStage(profile + rIdx);
        }

        private static bool IsDirty(Pawn p)
        {
            EnsureCache();
            if (_getDiaper == null || _curCategory == null) return false;
            var diaper = _getDiaper.Invoke(null, new object[] { p }) as Apparel;
            if (diaper == null) return false;
            Need diaperNeed = FindNeedByTypeName(p, "Need_Diaper");
            if (diaperNeed == null) return false;
            int cat = Convert.ToInt32(_curCategory.GetValue(diaperNeed));
            return cat < 3; // Trashed=0, Spent=1, Used=2 → dirty; Clean=3 → clean
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
