using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Handles all side-effects of crossing a regression stage boundary.
    /// Called from Need_Regression.AdjustRegression whenever the stage changes.
    /// Growth-point thresholds and accumulation now live on Need_Regression itself.
    /// </summary>
    public static class RegressionStageEffects
    {
        // ─── Entry point ──────────────────────────────────────────────────────

        public static void OnStageCrossed(Pawn pawn, RegressionStage from, RegressionStage to)
        {
            if (pawn?.skills == null || pawn.story == null) return;

            bool regressing = (int)to > (int)from;

            if (regressing)
            {
                // Apply downward effects for each stage boundary crossed in one call.
                // Note: growthPoints 50% carry is applied BEFORE this call in AdjustRegression.
                for (int s = (int)from + 1; s <= (int)to; s++)
                    ApplyDownwardEffects(pawn, (RegressionStage)s);

                // Fire memories: self stage-crossing + loved-one social memory
                ZIR_ThoughtUtility.FireStageCrossedDown(pawn);
                ZIR_ThoughtUtility.FireLovedOneRegressed(pawn, to);
            }
            else if (to == RegressionStage.Adult)
            {
                // Pawn recovered to Adult: clear loved-one memories so relations
                // don't keep grieving someone who has come back.
                ZIR_ThoughtUtility.ClearLovedOneMemories(pawn);
            }

            // Sync Need_Learning presence and regression hediff after any crossing
            UpdateLearningState(pawn, to);
            ApplyRegressionHediff(pawn, to);
        }

        // ─── Downward effects (called once per stage boundary crossed) ────────

        private static void ApplyDownwardEffects(Pawn pawn, RegressionStage stage)
        {
            StripSkillXP(pawn, stage);
            DowngradePassion(pawn);
            RemoveRandomTrait(pawn);
        }

        private static void StripSkillXP(Pawn pawn, RegressionStage stage)
        {
            List<SkillRecord> skills = pawn.skills.skills
                .Where(s => !s.TotallyDisabled && s.Level > 0)
                .OrderByDescending(s => s.Level)
                .ToList();

            int count;
            float fraction;
            switch (stage)
            {
                case RegressionStage.PreTeen:    count = 3;             fraction = 0.25f; break;
                case RegressionStage.YoungChild: count = 5;             fraction = 0.50f; break;
                case RegressionStage.Toddler:    count = skills.Count;  fraction = 0.75f; break;
                case RegressionStage.Infant:     count = skills.Count;  fraction = 1.00f; break;
                default: return;
            }

            int done = 0;
            foreach (SkillRecord skill in skills)
            {
                if (done >= count) break;

                if (fraction >= 1f)
                {
                    skill.levelInt = 0;
                    skill.xpSinceLastLevel = 0f;
                }
                else
                {
                    float totalXp = skill.XpTotalEarned + skill.xpSinceLastLevel;
                    SetSkillFromTotalXp(skill, totalXp * (1f - fraction));
                }
                done++;
            }
        }

        /// <summary>Rebuild levelInt + xpSinceLastLevel from a total XP value.</summary>
        private static void SetSkillFromTotalXp(SkillRecord skill, float targetXp)
        {
            skill.levelInt = 0;
            skill.xpSinceLastLevel = 0f;

            float remaining = UnityEngine.Mathf.Max(0f, targetXp);
            for (int lvl = 0; lvl < 20; lvl++)
            {
                float cost = XpForLevelUp(lvl);
                if (remaining >= cost)
                {
                    remaining -= cost;
                    skill.levelInt = lvl + 1;
                }
                else
                {
                    skill.xpSinceLastLevel = remaining;
                    return;
                }
            }
        }

        /// <summary>
        /// Mirrors SkillRecord.XpForLevelUpCurve:
        /// (0,1000), (9,10000), (19,30000) — linear segments.
        /// </summary>
        private static float XpForLevelUp(int level)
        {
            if (level <= 0) return 1000f;
            if (level <= 9) return 1000f + level * 1000f;
            return 10000f + (level - 9) * 2000f;
        }

        private static void DowngradePassion(Pawn pawn)
        {
            SkillRecord best = pawn.skills.skills
                .Where(s => s.passion != Passion.None && !s.TotallyDisabled)
                .OrderByDescending(s => (int)s.passion)
                .ThenByDescending(s => s.Level)
                .FirstOrDefault();

            if (best == null) return;
            best.passion = best.passion == Passion.Major ? Passion.Minor : Passion.None;
        }

        private static void RemoveRandomTrait(Pawn pawn)
        {
            List<Trait> traits = pawn.story?.traits?.allTraits;
            if (traits.NullOrEmpty()) return;

            // ZIR identity traits must never be stripped — they define the pawn's
            // relationship to regression and are central to the thought profile system.
            List<Trait> eligible = traits.Where(t =>
                t.def != ZIR_TraitDefOf.Big_Boy &&
                t.def != ZIR_TraitDefOf.Potty_Rebel &&
                t.def != ZIR_TraitDefOf.Diaper_Lover).ToList();

            if (eligible.NullOrEmpty()) return;
            traits.Remove(eligible.RandomElement());
        }

        // ─── Areal regression ────────────────────────────────────────────────────

        /// <summary>
        /// Applies an environmental/social regression penalty to a pawn.
        /// No-ops at Infant (already at floor). DL/PR traits reduce impact by 75%.
        /// </summary>
        public static void ApplyArealRegression(Pawn pawn, float basePenalty)
        {
            if (pawn?.needs == null) return;
            var reg = pawn.needs.TryGetNeed<Need_Regression>();
            if (reg == null || reg.CurStage == RegressionStage.Infant) return;

            float mult = 1f;
            var traits = pawn.story?.traits;
            if (traits != null &&
                (traits.HasTrait(ZIR_TraitDefOf.Diaper_Lover) || traits.HasTrait(ZIR_TraitDefOf.Potty_Rebel)))
                mult = 0.25f;

            reg.AdjustRegression(basePenalty * mult);
        }

        // ─── Regression hediff ──────────────────────────────────────────────────

        private static HediffDef HediffForStage(RegressionStage stage) => stage switch
        {
            RegressionStage.PreTeen    => ZIR_HediffDefOf.ZIR_Stage_PreTeen,
            RegressionStage.YoungChild => ZIR_HediffDefOf.ZIR_Stage_YoungChild,
            RegressionStage.Toddler    => ZIR_HediffDefOf.ZIR_Stage_Toddler,
            RegressionStage.Infant     => ZIR_HediffDefOf.ZIR_Stage_Infant,
            _                          => null
        };

        private static readonly HediffDef[] AllStageHediffs = new HediffDef[4]; // filled on first call

        /// <summary>
        /// Swaps the active ZIR stage hediff to match the pawn's current regression
        /// stage. All stage hediffs not matching the current stage are removed first,
        /// then the correct one is added if missing. At Adult all are removed.
        /// The legacy ZIR_Regression hediff is also cleaned up here.
        /// </summary>
        public static void ApplyRegressionHediff(Pawn pawn, RegressionStage stage)
        {
            if (pawn?.health == null) return;

            // Lazy-fill the array once defs are initialised.
            if (AllStageHediffs[0] == null)
            {
                AllStageHediffs[0] = ZIR_HediffDefOf.ZIR_Stage_PreTeen;
                AllStageHediffs[1] = ZIR_HediffDefOf.ZIR_Stage_YoungChild;
                AllStageHediffs[2] = ZIR_HediffDefOf.ZIR_Stage_Toddler;
                AllStageHediffs[3] = ZIR_HediffDefOf.ZIR_Stage_Infant;
            }

            HediffDef target = HediffForStage(stage);

            // Remove all stage hediffs that don't belong.
            foreach (HediffDef def in AllStageHediffs)
            {
                if (def == null || def == target) continue;
                Hediff stale = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (stale != null) pawn.health.RemoveHediff(stale);
            }

            // Also clean up the old single-hediff approach.
            Hediff legacy = pawn.health.hediffSet.GetFirstHediffOfDef(ZIR_HediffDefOf.ZIR_Regression);
            if (legacy != null) pawn.health.RemoveHediff(legacy);

            if (target == null) return; // Adult — nothing to add

            if (pawn.health.hediffSet.GetFirstHediffOfDef(target) == null)
            {
                Hediff h = HediffMaker.MakeHediff(target, pawn);
                h.Severity = 1f; // must be > 0 or HediffWithComps.ShouldRemove fires immediately
                pawn.health.AddHediff(h);
            }
        }

        // ─── Learning need management ─────────────────────────────────────────

        /// <summary>
        /// Ensures Need_Learning is present when the pawn is regressed and absent when Adult.
        /// Our Patch_LearningNeed.ShouldHaveNeed postfix makes Need_Learning eligible;
        /// calling AddOrRemoveNeedsAsAppropriate() triggers the actual add/remove.
        /// </summary>
        public static void UpdateLearningState(Pawn pawn, RegressionStage stage)
        {
            if (!ModsConfig.BiotechActive) return;
            pawn.needs?.AddOrRemoveNeedsAsAppropriate();
        }
    }
}
