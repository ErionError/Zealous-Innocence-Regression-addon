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
                    newThought.SetForcedStage(stage);
                    break;

                case "SoiledSelf":
                    // Suppress if ZI's PantsPeed already fired for the same accident.
                    if (__instance.Memories.Any(t => t.def?.defName == "PantsPeed"))
                        return false;
                    newThought.SetForcedStage(stage);
                    break;

                case "WetBed":
                    newThought.SetForcedStage(stage);
                    break;

                case "DiaperPeed":
                    newThought.SetForcedStage(stage);
                    break;

                case "DiaperPeedBed":
                    newThought.SetForcedStage(stage);
                    break;

                case "PeedOnMe":
                    newThought.SetForcedStage(stage);
                    // Bedwetting-on-bedmate is a mild areal regression trigger.
                    RegressionStageEffects.ApplyArealRegression(pawn, -0.05f);
                    break;
            }

            return true;
        }
    }
}
