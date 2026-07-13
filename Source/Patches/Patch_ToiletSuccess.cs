using DubsBadHygiene;
using HarmonyLib;
using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace ZealousInnocenceRegression.Patches
{
    /// <summary>
    /// Fires the potty-training rewards (pottyProgress, bar reversal, learning bump,
    /// mood thought) when a pawn successfully completes a DBH UseToilet job.
    ///
    /// We patch JobDriver.Cleanup — the only reliable hook that fires once per job
    /// end with a condition flag — and filter to DBH's JobDriver_UseToilet instances.
    /// We additionally verify the bladder refilled to ≥ 0.9 to confirm the job was
    /// genuinely productive rather than cancelled mid-way.
    /// </summary>
    public static class Patch_ToiletSuccess
    {
        private static Type _dbhDriverType;

        public static void Register(Harmony harmony)
        {
            // Cache DBH's internal JobDriver_UseToilet type at startup.
            _dbhDriverType = AccessTools.TypeByName("DubsBadHygiene.JobDriver_UseToilet");
            if (_dbhDriverType == null)
            {
                Log.Warning("[ZIR] DubsBadHygiene.JobDriver_UseToilet not found — toilet-success rewards skipped.");
                return;
            }

            harmony.Patch(
                AccessTools.Method(typeof(JobDriver), nameof(JobDriver.Cleanup)),
                postfix: new HarmonyMethod(typeof(Patch_ToiletSuccess), nameof(Postfix)));

            Log.Message("[ZIR] Patched JobDriver.Cleanup for toilet-success potty rewards.");
        }

        public static void Postfix(JobDriver __instance, JobCondition condition)
        {
            // Only successful completions count as training.
            if (condition != JobCondition.Succeeded) return;

            // Filter to DBH's UseToilet driver only.
            if (_dbhDriverType == null || !_dbhDriverType.IsInstanceOfType(__instance)) return;

            Pawn pawn = __instance.pawn;
            if (pawn?.needs == null) return;

            // Confirm the bladder was actually dumped (i.e. the job wasn't trivially
            // vacuous because the pawn didn't even need to go).
            var bladder = pawn.needs.TryGetNeed<Need_Bladder>();
            if (bladder != null && bladder.CurLevel < 0.9f) return;

            ZIR_PottyUtility.OnSuccessfulToiletUse(pawn, selfDirected: true);
        }
    }
}
