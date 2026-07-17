using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
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

        [DebugAction("ZIR", "Dump thought snapshot",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DumpThoughtSnapshot(Pawn pawn)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[ZIR] ══ Thought Snapshot: {pawn.LabelShort} ══");

            // ── Regression ────────────────────────────────────────────────────
            var reg = pawn.needs?.TryGetNeed<Need_Regression>();
            if (reg == null)
            {
                sb.AppendLine("  [no Need_Regression — not a ZIR pawn]");
                Log.Message(sb.ToString());
                return;
            }

            sb.AppendLine($"  REGRESSION");
            sb.AppendLine($"    Stage          : {reg.CurStage}");
            sb.AppendLine($"    Bar level      : {reg.CurLevelPercentage:P1}");
            sb.AppendLine($"    Growth pts     : {reg.growthPoints:F1} / {(reg.CurStage == RegressionStage.Adult ? "—" : reg.CurrentThreshold.ToString("F0"))}");
            sb.AppendLine($"    Growth rate    : {reg.GrowthPointsRatePerDay:F2} pts/day");

            // ── Potty ─────────────────────────────────────────────────────────
            float pottyCapForStage = reg.CurStage switch
            {
                RegressionStage.Infant     => 0.15f,
                RegressionStage.Toddler    => 0.30f,
                RegressionStage.YoungChild => 0.65f,
                _                          => 1.00f
            };
            float cooldownRemain = 0f;
            if (reg.lastPottySessionTick > 0)
            {
                int elapsed = Find.TickManager.TicksGame - reg.lastPottySessionTick;
                cooldownRemain = Mathf.Max(0f, ZIR_PottyUtility.CaretakerCooldownTicks - elapsed);
            }
            sb.AppendLine($"  POTTY TRAINING");
            sb.AppendLine($"    pottyProgress  : {reg.pottyProgress:P1}  (stage cap: {pottyCapForStage:P0})");
            sb.AppendLine($"    Accident hits  : {reg.accidentPenaltyToday} / 2 today");
            sb.AppendLine($"    Caretaker CD   : {(cooldownRemain > 0 ? $"{cooldownRemain / 2500f:F1}h remaining" : "ready")}");

            // ── Trait profile ─────────────────────────────────────────────────
            int traitOffset = ZIR_ThoughtUtility.GetTraitOffset(pawn);
            string traitLabel = traitOffset switch
            {
                1 => "Big Boy (Self-Conscious)",
                2 => "Potty Rebel",
                3 => "Diaper Lover",
                _ => "Default (no ZIR trait)"
            };
            float progressMult = ZIR_PottyUtility.GetProgressMultiplier(pawn);
            sb.AppendLine($"  TRAIT PROFILE");
            sb.AppendLine($"    Trait          : {traitLabel}  (offset={traitOffset})");
            sb.AppendLine($"    Progress mult  : {progressMult:F2}×");

            // ── Learning ──────────────────────────────────────────────────────
            var learning = pawn.needs?.TryGetNeed<Need_Learning>();
            sb.AppendLine($"  LEARNING NEED");
            if (learning == null)
                sb.AppendLine("    absent (Biotech inactive or no need)");
            else
                sb.AppendLine($"    {learning.CurLevelPercentage:P1}  (suspended={learning.Suspended})");

            // ── Apparel flags ─────────────────────────────────────────────────
            bool hasOnesie    = ZIR_ApparelUtility.HasOnesie(pawn);
            bool hasPacifier  = ZIR_ApparelUtility.HasPacifier(pawn);
            bool hasExposedPr = ZIR_ApparelUtility.HasExposedProtection(pawn);
            sb.AppendLine($"  APPAREL FLAGS  (passive regression per interval)");
            sb.AppendLine($"    Onesie         : {hasOnesie}   (−0.00025/interval if true)");
            sb.AppendLine($"    Pacifier       : {hasPacifier}   (−0.0001875/interval if true)");
            sb.AppendLine($"    ExposedProtect : {hasExposedPr}  (−0.000375/interval if true)");
            if (pawn.apparel != null)
            {
                sb.AppendLine("    Worn apparel:");
                foreach (Apparel a in pawn.apparel.WornApparel)
                    sb.AppendLine($"      • {a.def.defName}  [{string.Join(", ", a.def.apparel?.tags ?? new System.Collections.Generic.List<string>())}]");
            }

            // ── Active ZIR memory thoughts (own + ZI accident memories remapped by ZIR) ───
            var memories = pawn.needs?.mood?.thoughts?.memories;
            sb.AppendLine($"  ZIR MEMORY THOUGHTS");
            if (memories == null)
            {
                sb.AppendLine("    (no memory access)");
            }
            else
            {
                bool any = false;
                foreach (Thought_Memory m in memories.Memories)
                {
                    if (m.def == null) continue;
                    bool isZIR      = m.def.defName.StartsWith("ZIR_");
                    bool isRemapped = IsZIAccidentThought(m.def.defName);
                    if (!isZIR && !isRemapped) continue;
                    float ageDays = (Find.TickManager.TicksGame - m.age) / 60000f;
                    string otherPawnLabel = (m is Thought_MemorySocial social && social.otherPawn != null)
                        ? $" (about {social.otherPawn.LabelShort})" : "";
                    string tag = isRemapped ? " [ZI-accident]" : "";
                    sb.AppendLine($"    • {m.def.defName}{tag}  stage={m.CurStageIndex}  mood={m.MoodOffset():+0.##;-0.##;0}  age={ageDays:F1}d{otherPawnLabel}");
                    any = true;
                }
                if (!any) sb.AppendLine("    (none)");
            }

            // ── Active ZIR situation thoughts (own + ZI apparel/stink thoughts remapped by ZIR) ──
            sb.AppendLine($"  ZIR SITUATION THOUGHTS");
            var allThoughts = new System.Collections.Generic.List<Thought>();
            pawn.needs?.mood?.thoughts?.GetAllMoodThoughts(allThoughts);
            bool anySit = false;
            foreach (Thought t in allThoughts)
            {
                if (t is Thought_Memory) continue;   // already covered above
                if (t.def == null) continue;
                bool isZIR      = t.def.defName.StartsWith("ZIR_");
                bool isRemapped = IsZIApparelThought(t.def.defName);
                if (!isZIR && !isRemapped) continue;
                string tag = isRemapped ? " [ZI-apparel]" : "";
                sb.AppendLine($"    • {t.def.defName}{tag}  stage={t.CurStageIndex}  mood={t.MoodOffset():+0.##;-0.##;0}");
                anySit = true;
            }
            if (!anySit) sb.AppendLine("    (none)");

            Log.Message(sb.ToString());
        }

        private static bool IsZIAccidentThought(string defName) => defName switch
        {
            "PantsPeed"    => true,
            "SoiledSelf"   => true,
            "WetBed"       => true,
            "DiaperPeed"   => true,
            "DiaperPeedBed"=> true,
            "PeedOnMe"     => true,
            _              => false
        };

        private static bool IsZIApparelThought(string defName) => defName switch
        {
            "Onesie_Dressed"    => true,
            "CribBed_Preferred" => true,
            "Stink"             => true,
            _                   => false
        };
    }
}
