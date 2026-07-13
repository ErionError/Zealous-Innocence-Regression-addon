using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Shared helpers for thought firing and trait-profile detection.
    /// </summary>
    public static class ZIR_ThoughtUtility
    {
        // ─── Trait profile ────────────────────────────────────────────────────
        // Maps to stage-index offset used in multi-stage ThoughtDefs.
        // Default=0  BB=1  PR=2  DL=3
        public static int GetTraitOffset(Pawn pawn)
        {
            if (pawn.story?.traits == null) return 0;
            if (ZIR_TraitDefOf.Big_Boy    != null && pawn.story.traits.HasTrait(ZIR_TraitDefOf.Big_Boy))    return 1;
            if (ZIR_TraitDefOf.Potty_Rebel != null && pawn.story.traits.HasTrait(ZIR_TraitDefOf.Potty_Rebel)) return 2;
            if (ZIR_TraitDefOf.Diaper_Lover != null && pawn.story.traits.HasTrait(ZIR_TraitDefOf.Diaper_Lover)) return 3;
            return 0;
        }

        // ─── Memory firing ────────────────────────────────────────────────────

        /// <summary>Fire stage-crossed-down memory on the regressed pawn.</summary>
        public static void FireStageCrossedDown(Pawn pawn)
        {
            if (!CanGainMemory(pawn)) return;
            int stage = GetTraitOffset(pawn); // 0=Default, 1=BB, 2=PR, 3=DL
            GainMemory(pawn, ZIR_ThoughtDefOf.ZIR_StageCrossedDown, stage);
        }

        /// <summary>Fire recovery-moment memory on the pawn when they resolve a letter.</summary>
        public static void FireRecoveryMoment(Pawn pawn)
        {
            if (!CanGainMemory(pawn)) return;
            int profile = GetTraitOffset(pawn);
            // PR(2) and DL(3) share stage index 2 in ZIR_RecoveryMoment
            int stage = profile == 1 ? 1 : (profile >= 2 ? 2 : 0);
            GainMemory(pawn, ZIR_ThoughtDefOf.ZIR_RecoveryMoment, stage);
        }

        /// <summary>
        /// Fire loved-one social memory on every close relation of <paramref name="regrPawn"/>
        /// when they cross to a new (worse) regression stage.
        /// Stage = observerTraitIdx * 5 + targetRegressionIdx.
        /// </summary>
        public static void FireLovedOneRegressed(Pawn regrPawn, RegressionStage newStage)
        {
            if (regrPawn.relations == null || regrPawn.Map == null) return;
            int targetRegressionIdx = (int)newStage; // Adult=0 … Infant=4
            if (targetRegressionIdx <= 0) return;     // don't fire at Adult level

            foreach (DirectPawnRelation rel in regrPawn.relations.DirectRelations)
            {
                if (!IsCloseRelation(rel.def)) continue;
                Pawn observer = rel.otherPawn;
                if (observer == null || observer == regrPawn || observer.Dead || !observer.Spawned) continue;
                if (!CanGainMemory(observer)) continue;

                int observerTraitIdx = GetTraitOffset(observer);
                int stageIdx = observerTraitIdx * 5 + targetRegressionIdx;

                var thought = (Thought_MemorySocial)ThoughtMaker.MakeThought(
                    ZIR_ThoughtDefOf.ZIR_LovedOneRegressed, stageIdx);
                observer.needs.mood.thoughts.memories.TryGainMemory(thought, regrPawn);
            }
        }

        // ─── Observer proximity (memory, refreshed daily) ──────────────────────

        /// <summary>
        /// Refresh or create the proximity observer memory on <paramref name="observer"/>.
        /// Uses TryGainMemoryFast so an existing memory is renewed with the updated stage
        /// rather than creating a duplicate.
        /// </summary>
        /// <summary>
        /// Refresh the proximity observer memory on <paramref name="observer"/>.
        /// Stage = observerTraitIdx * 5 + targetRegressionIdx.
        /// </summary>
        public static void FireObserverMemory(Pawn observer, RegressionStage targetStage)
        {
            if (!CanGainMemory(observer)) return;
            int targetRegressionIdx  = (int)targetStage;
            int observerTraitIdx     = GetTraitOffset(observer);
            int stageIdx             = observerTraitIdx * 5 + targetRegressionIdx;
            observer.needs.mood.thoughts.memories
                .TryGainMemoryFast(ZIR_ThoughtDefOf.ZIR_ObserverRegression, stageIdx);
        }

        // ─── Loved-one cleanup on recovery ────────────────────────────────────

        /// <summary>
        /// When a pawn recovers to Adult, remove ZIR_LovedOneRegressed memories
        /// from all close relations so they don't linger past recovery.
        /// </summary>
        public static void ClearLovedOneMemories(Pawn recoveredPawn)
        {
            if (recoveredPawn.relations == null) return;

            foreach (DirectPawnRelation rel in recoveredPawn.relations.DirectRelations)
            {
                if (!IsCloseRelation(rel.def)) continue;
                var memories = rel.otherPawn?.needs?.mood?.thoughts?.memories;
                if (memories == null) continue;

                // Iterate backwards to safely remove while enumerating
                for (int i = memories.Memories.Count - 1; i >= 0; i--)
                {
                    var m = memories.Memories[i];
                    if (m.def == ZIR_ThoughtDefOf.ZIR_LovedOneRegressed
                        && m is Thought_MemorySocial social
                        && social.otherPawn == recoveredPawn)
                    {
                        memories.RemoveMemory(m);
                    }
                }
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static bool CanGainMemory(Pawn pawn)
            => pawn?.needs?.mood?.thoughts?.memories != null;

        private static void GainMemory(Pawn pawn, ThoughtDef def, int stageIndex)
        {
            var thought = (Thought_Memory)ThoughtMaker.MakeThought(def, stageIndex);
            pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
        }

        public static bool IsCloseRelation(PawnRelationDef def)
        {
            return def == PawnRelationDefOf.Lover
                || def == PawnRelationDefOf.Spouse
                || def == PawnRelationDefOf.ExSpouse
                || def == PawnRelationDefOf.ExLover
                || def == PawnRelationDefOf.Parent
                || def == PawnRelationDefOf.Sibling
                || def == PawnRelationDefOf.Child;
        }
    }
}
