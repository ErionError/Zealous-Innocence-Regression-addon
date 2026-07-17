using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ZealousInnocenceRegression.Patches
{
    /// <summary>
    /// Prefix on MemoryThoughtHandler.TryGainMemory.
    /// Remaps accident and bedwetting memory thoughts to regression-indexed stages.
    ///
    /// All thoughts use the same formula: traitIdx * 5 + regressionIdx
    ///   traitIdx:     Default=0  BigBoy=1  PottyRebel=2  DiaperLover=3
    ///   regressionIdx: Adult=0   PreTeen=1  YoungChild=2  Toddler=3  Infant=4
    ///
    /// PantsPeed:   fires when pants are worn (ZI).
    /// SoiledSelf:  fires when no pants (DBH). Suppressed if PantsPeed already
    ///              exists for this pawn (eliminates duplicates from same accident).
    /// PeedOnMe:    also applies a small areal regression hit.
    /// </summary>
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory),
        new[] { typeof(Thought_Memory), typeof(Pawn) })]
    public static class Patch_AccidentThoughts
    {
        public static bool Prefix(MemoryThoughtHandler __instance, Thought_Memory newThought)
        {
            Pawn pawn = __instance?.pawn;
            if (pawn == null) return true;
            if (pawn.DevelopmentalStage != DevelopmentalStage.Adult) return true;

            var regression = pawn.needs?.TryGetNeed<Need_Regression>();
            if (regression == null) return true;

            int traitIdx = ZIR_ThoughtUtility.GetTraitOffset(pawn);
            int rIdx     = (int)regression.CurStage;
            int stage    = traitIdx * 5 + rIdx;

            string defName = newThought.def?.defName;

            switch (defName)
            {
                case "PantsPeed":
                case "WetBed":
                case "DiaperPeed":
                case "DiaperPeedBed":
                case "PeedOnMe":
                    // Update any already-stored memories of this def to the current stage.
                    // Vanilla's TryMergeWithExistingMemory may call Renew() on the oldest
                    // existing entry instead of adding newThought, discarding our
                    // SetForcedStage call on newThought. Pre-patching existing entries
                    // ensures Renew() always lands on the correct stage index.
                    foreach (var mem in __instance.Memories)
                    {
                        if (mem.def?.defName == defName)
                            mem.SetForcedStage(stage);
                    }
                    newThought.SetForcedStage(stage);
                    if (defName == "PeedOnMe")
                        RegressionStageEffects.ApplyArealRegression(pawn, -0.05f);
                    break;

                case "SoiledSelf":
                    // Suppress if ZI's PantsPeed already fired for the same accident.
                    if (__instance.Memories.Any(t => t.def?.defName == "PantsPeed"))
                        return false;
                    foreach (var mem in __instance.Memories)
                    {
                        if (mem.def?.defName == "SoiledSelf")
                            mem.SetForcedStage(stage);
                    }
                    newThought.SetForcedStage(stage);
                    break;
            }

            return true;
        }
    }
}
