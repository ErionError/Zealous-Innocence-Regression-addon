using LudeonTK;
using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Dev-mode debug actions for ZIR testing.  Appear in Dev Tools → ZIR menu.
    /// </summary>
    public static class DebugActions_ZIR
    {
        [DebugAction("ZIR", "Regress selected pawn (−0.25)",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RegressPawn(Pawn pawn)
        {
            var reg = pawn.needs?.TryGetNeed<Need_Regression>();
            if (reg == null) { Log.Warning("[ZIR] Pawn has no Need_Regression."); return; }
            reg.AdjustRegression(-0.25f);
            Log.Message($"[ZIR] {pawn.LabelShort} regression → {reg.CurLevelPercentage:P0} ({reg.CurStage})");
        }

        [DebugAction("ZIR", "Recover selected pawn (+0.25)",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RecoverPawn(Pawn pawn)
        {
            var reg = pawn.needs?.TryGetNeed<Need_Regression>();
            if (reg == null) { Log.Warning("[ZIR] Pawn has no Need_Regression."); return; }
            reg.AdjustRegression(0.25f);
            Log.Message($"[ZIR] {pawn.LabelShort} regression → {reg.CurLevelPercentage:P0} ({reg.CurStage})");
        }

        [DebugAction("ZIR", "Add 30 growth points",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddGrowthPoints(Pawn pawn)
        {
            var reg = pawn.needs?.TryGetNeed<Need_Regression>();
            if (reg == null) { Log.Warning("[ZIR] Pawn has no Need_Regression."); return; }
            if (reg.CurStage == RegressionStage.Adult)
            {
                Log.Message($"[ZIR] {pawn.LabelShort} is Adult — no growth points to add.");
                return;
            }
            reg.AddGrowthPoints(30f);
            Log.Message($"[ZIR] {pawn.LabelShort} growthPoints → {reg.growthPoints:F1} / {reg.CurrentThreshold:F0}");
        }

        [DebugAction("ZIR", "Set growth points to recovery threshold",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SetGrowthPointsToThreshold(Pawn pawn)
        {
            var reg = pawn.needs?.TryGetNeed<Need_Regression>();
            if (reg == null) { Log.Warning("[ZIR] No Need_Regression."); return; }
            if (reg.CurStage == RegressionStage.Adult)
            {
                Log.Message("[ZIR] Pawn is already Adult — no threshold.");
                return;
            }
            float threshold = reg.CurrentThreshold;
            reg.growthPoints = threshold;
            Log.Message($"[ZIR] {pawn.LabelShort} growthPoints set to {threshold:F0} (threshold)");
        }

        [DebugAction("ZIR", "Log pawn regression state",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogRegressionState(Pawn pawn)
        {
            var reg      = pawn.needs?.TryGetNeed<Need_Regression>();
            var learning = pawn.needs?.TryGetNeed<Need_Learning>();

            Log.Message($"[ZIR] {pawn.LabelShort}:" +
                $"\n  Stage:           {reg?.CurStage} ({reg?.CurLevelPercentage:P0})" +
                $"\n  GrowthPoints:    {reg?.growthPoints:F1} / {reg?.CurrentThreshold:F0}" +
                $"\n  Rate (pts/day):  {reg?.GrowthPointsRatePerDay:F2}" +
                $"\n  Need_Learning:   {(learning != null ? $"{learning.CurLevelPercentage:P0}" : "absent")}");
        }
    }
}
