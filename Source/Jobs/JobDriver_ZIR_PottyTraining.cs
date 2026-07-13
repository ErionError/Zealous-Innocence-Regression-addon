using DubsBadHygiene;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Caretaker-led potty training session.
    ///
    /// TargetA = fixture (searched by WorkGiver as availability gate, not visited)
    /// TargetB = patient pawn
    ///
    /// Simplified flow — session happens at the patient's location:
    ///   1. Caretaker walks to patient.
    ///   2. Caretaker guides patient for 750 ticks (at patient's position).
    ///   3. FinishAction: bladder reset, rewards, cooldown, optional mood thought.
    ///
    /// No path-forcing the patient and no fixture reservation. This prevents
    /// the caretaker and patient racing to different toilets and blocking other
    /// pawns from using fixtures.
    /// </summary>
    public class JobDriver_ZIR_PottyTraining : JobDriver
    {
        private Pawn Patient => (Pawn)job.targetB.Thing;

        private const int WaitTicks = 750;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // No reservation needed. The 6-hour cooldown on Need_Regression
            // prevents double-booking. Reserving the fixture blocked other pawns
            // from using toilets unnecessarily.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);

            // Toil 1: caretaker walks to patient
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);

            // Toil 2: guidance session at patient's location
            Toil session = ToilMaker.MakeToil("ZIR_PottyTraining_Session");
            session.defaultDuration     = WaitTicks;
            session.defaultCompleteMode = ToilCompleteMode.Delay;
            session.socialMode          = RandomSocialMode.SuperActive;
            session.handlingFacing      = true;

            session.AddFinishAction(delegate
            {
                OnSessionComplete(GetActor(), Patient);
            });

            yield return session;
        }

        private static void OnSessionComplete(Pawn caretaker, Pawn patient)
        {
            if (patient?.needs == null) return;

            var reg = patient.needs.TryGetNeed<Need_Regression>();
            if (reg == null || reg.CurStage == RegressionStage.Adult) return;

            int caretakerSocialSkill = caretaker.skills.GetSkill(SkillDefOf.Social).levelInt;
            float successRate = GetSuccessRate(caretakerSocialSkill, patient);

            // Roll for success
            bool trainingSucceeded = Rand.Value < successRate;
            
            if (trainingSucceeded)
            {
                // Fully reset bladder
                var bladder = patient.needs.TryGetNeed<Need_Bladder>();
                if (bladder != null) bladder.CurLevel += 0.5f;

                // Fire potty-training rewards
                ZIR_PottyUtility.OnSuccessfulToiletUse(patient, selfDirected: false);

                MoteMaker.ThrowText(patient.DrawPos, patient.Map, "Success!", Color.white);
                
                // Reset caretaker cooldown
                reg.lastPottySessionTick = Find.TickManager.TicksGame;
            }
            else
            {
                // Handle training failure here (optional)
                // Could add a small mood hit, partial progress, or nothing
                // Example: patient.needs?.mood?.thoughts?.memories?
                //     .TryGainMemory(ZIR_ThoughtDefOf.ZIR_FailedPottyTraining);
                MoteMaker.ThrowText(patient.DrawPos, patient.Map, "Failed", Color.white);
                reg.lastPottySessionTick = Find.TickManager.TicksGame;
            }

            // ZIR_ForcedPottyTraining: fires for all regressed patients.
            // Severity scales by trait — Potty_Rebel and Big_Boy get the harshest hits;
            // DL gets the mildest (they just wanted their diaper instead).
            var forcedMemories = patient.needs?.mood?.thoughts?.memories;
            if (forcedMemories != null)
            {
                int traitIdx = ZIR_ThoughtUtility.GetTraitOffset(patient);
                int rIdx     = reg != null ? (int)reg.CurStage : 0;
                var forcedThought = (Thought_Memory)ThoughtMaker.MakeThought(
                    ZIR_ThoughtDefOf.ZIR_ForcedPottyTraining, traitIdx * 5 + rIdx);
                forcedMemories.TryGainMemory(forcedThought);
            }
        }
        
        private static float GetSuccessRate(int socialSkill, Pawn patient)
        {
            // Clamp skill between 0 and 20
            socialSkill = Mathf.Clamp(socialSkill, 0, 20);

            // Success rate lookup table
            float[] successRates = new float[]
            {
                0.25f,   // 0
                0.30f,   // 1
                0.35f,   // 2
                0.40f,   // 3
                0.50f,   // 4
                0.55f,   // 5
                0.60f,   // 6
                0.65f,   // 7
                0.70f,   // 8
                0.75f,   // 9
                0.80f,   // 10
                0.85f,   // 11
                0.90f,   // 12
                0.95f,   // 13
                1.00f,   // 14
                1.05f,   // 15
                1.10f,   // 16
                1.15f,   // 17
                1.20f,   // 18
                1.25f,   // 19
                1.30f    // 20
            };

            float baseSuccessRate = successRates[socialSkill];

            // Apply regression stage modifier
            var regression = patient.needs.TryGetNeed<Need_Regression>();
            if (regression != null)
            {
                float regressionModifier = regression.CurStage switch
                {
                    RegressionStage.Adult => 2.0f,        
                    RegressionStage.PreTeen => 1.5f,     
                    RegressionStage.YoungChild => 1.2f,  
                    RegressionStage.Toddler => 1.0f,    
                    RegressionStage.Infant => 0.85f,     
                    _ => 1.0f
                };

                baseSuccessRate = Mathf.Clamp(baseSuccessRate * regressionModifier, 0.05f, 1.0f);
            }

            return Mathf.Clamp01(baseSuccessRate);
        }
    }
}
