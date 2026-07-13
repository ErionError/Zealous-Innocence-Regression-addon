using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ZealousInnocenceRegression.Patches
{
    /// <summary>
    /// Postfix on ZI's InteractionWorker_RegressedPlayTime.Interacted.
    /// When two regressed pawns engage in childish play together, both take
    /// a small areal regression hit — the interaction normalises regressed
    /// behaviour and erodes adult identity.
    /// Penalty: −0.02 × trait mult for both initiator and recipient.
    /// Registered manually to avoid baking ZI type tokens into attributes.
    /// </summary>
    public static class Patch_RegressedPlayTime
    {
        public static void Register(HarmonyLib.Harmony harmony)
        {
            var ziType = AccessTools.TypeByName("ZealousInnocence.InteractionWorker_RegressedPlayTime");
            if (ziType == null)
            {
                Log.Warning("[ZIR] InteractionWorker_RegressedPlayTime not found — play-time areal regression skipped.");
                return;
            }

            var method = AccessTools.Method(ziType, "Interacted");
            if (method == null)
            {
                Log.Warning("[ZIR] InteractionWorker_RegressedPlayTime.Interacted not found — play-time areal regression skipped.");
                return;
            }

            harmony.Patch(method,
                postfix: new HarmonyMethod(typeof(Patch_RegressedPlayTime), nameof(Postfix)));
            Log.Message("[ZIR] Patched InteractionWorker_RegressedPlayTime.Interacted");
        }

        // Parameter names match the original method signature exactly.
        public static void Postfix(Pawn initiator, Pawn recipient)
        {
            RegressionStageEffects.ApplyArealRegression(initiator, -0.02f);
            RegressionStageEffects.ApplyArealRegression(recipient, -0.02f);
        }
    }
}
