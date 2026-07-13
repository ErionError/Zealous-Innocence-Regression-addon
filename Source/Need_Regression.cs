using System.Collections.Generic;
using System.Diagnostics;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZealousInnocenceRegression
{
    public enum RegressionStage
    {
        Adult,      // 0.75 – 1.00  (normal)
        PreTeen,    // 0.50 – 0.75  (slipping)
        YoungChild, // 0.30 – 0.50  (noticeable)
        Toddler,    // 0.10 – 0.30  (dependent)
        Infant      // 0.00 – 0.10  (fully broken)
    }

    public class Need_Regression : Need
    {
        // ── Bar thresholds (lower bound of each stage) ────────────────────────
        public const float ThresholdPreTeen    = 0.75f;
        public const float ThresholdYoungChild = 0.50f;
        public const float ThresholdToddler    = 0.30f;
        public const float ThresholdInfant     = 0.10f;

        // ── Recovery growth-point thresholds per stage ────────────────────────
        // Deeper stages need more points (steeper escape).
        // Infant/Toddler are purely event-driven (floor = 0).
        public const float RecovThreshold_PreTeenToAdult     = 40f;
        public const float RecovThreshold_YoungToPreTeen     = 70f;
        public const float RecovThreshold_ToddlerToYoung     = 110f;
        public const float RecovThreshold_InfantToToddler    = 160f;

        // Max passive growth rate (pts/day) when learning need is fully satisfied.
        public const float MaxGrowthRatePerDay = 4.0f;

        // ── Saved state ───────────────────────────────────────────────────────
        // Progress toward the next recovery moment. Range: 0 → CurrentThreshold.
        // Capped at threshold; resets to 0 on recovery moment resolution.
        public float growthPoints = 0f;

        // ── Potty training state ──────────────────────────────────────────────
        // 0–1. Starts at 1.0 (fully trained adult). Degrades via accidents,
        // stage crossings. Improves via toilet use and caretaker sessions.
        public float pottyProgress = 1f;

        // Game tick of the last successful caretaker session. Used to gate the
        // 6-hour cooldown (15 000 ticks) per patient.
        public int lastPottySessionTick = -1;

        // Tracks how many pottyProgress-reducing accident penalties have fired
        // today. Cap = 2 per day (regression damage still fires freely).
        public int accidentPenaltyToday = 0;

        // Last in-game day accidentPenaltyToday was reset — checked in NeedInterval.
        private int lastDayReset = -1;

        // ── Computed properties ───────────────────────────────────────────────
        public RegressionStage CurStage
        {
            get
            {
                float level = CurLevelPercentage;
                if (level >= ThresholdPreTeen)    return RegressionStage.Adult;
                if (level >= ThresholdYoungChild) return RegressionStage.PreTeen;
                if (level >= ThresholdToddler)    return RegressionStage.YoungChild;
                if (level >= ThresholdInfant)     return RegressionStage.Toddler;
                return RegressionStage.Infant;
            }
        }

        /// <summary>Points needed for a recovery moment at the current stage.</summary>
        public float CurrentThreshold => CurStage switch
        {
            RegressionStage.PreTeen    => RecovThreshold_PreTeenToAdult,
            RegressionStage.YoungChild => RecovThreshold_YoungToPreTeen,
            RegressionStage.Toddler    => RecovThreshold_ToddlerToYoung,
            RegressionStage.Infant     => RecovThreshold_InfantToToddler,
            _                          => float.MaxValue   // Adult: no recovery needed
        };

        /// <summary>
        /// Passive accumulation floor per day for this stage (pts/day at zero learning).
        /// Infant and Toddler are 0 — purely event-driven.
        /// </summary>
        private float GrowthFloorPerDay => CurStage switch
        {
            RegressionStage.Infant     => 0.0f,
            RegressionStage.Toddler    => 0.0f,
            RegressionStage.YoungChild => 0.2f,
            RegressionStage.PreTeen    => 1.0f,
            _                          => 0.0f
        };

        /// <summary>Live pts/day rate: lerp(floor, max, learningLevel).</summary>
        public float GrowthPointsRatePerDay
        {
            get
            {
                if (CurStage == RegressionStage.Adult) return 0f;
                Need_Learning learning = pawn.needs?.TryGetNeed<Need_Learning>();
                float learningLevel = (learning != null && !learning.Suspended)
                    ? learning.CurLevelPercentage : 0f;
                return Mathf.Lerp(GrowthFloorPerDay, MaxGrowthRatePerDay, learningLevel);
            }
        }

        // ── Constructor ───────────────────────────────────────────────────────
        public Need_Regression(Pawn pawn) : base(pawn)
        {
            threshPercents = new List<float>
            {
                ThresholdPreTeen,
                ThresholdYoungChild,
                ThresholdToddler,
                ThresholdInfant
            };
        }

        public override void SetInitialLevel()
        {
            CurLevelPercentage = 1f;    // all pawns start as Adult
            growthPoints = 0f;
        }

        // ── Save / load ───────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref growthPoints,          "growthPoints",          0f);
            Scribe_Values.Look(ref pottyProgress,         "pottyProgress",         1f);
            Scribe_Values.Look(ref lastPottySessionTick,  "lastPottySessionTick",  -1);
            Scribe_Values.Look(ref accidentPenaltyToday,  "accidentPenaltyToday",  0);
            Scribe_Values.Look(ref lastDayReset,          "lastDayReset",          -1);
        }

        // ── Core API ──────────────────────────────────────────────────────────

        /// <summary>
        /// The only way to move the regression bar.
        /// Positive = recover, negative = worsen.
        /// A downward stage crossing carries 50 % of earned growth points.
        /// </summary>
        public void AdjustRegression(float amount)
        {
            RegressionStage oldStage = CurStage;
            CurLevelPercentage = Mathf.Clamp01(CurLevelPercentage + amount);
            RegressionStage newStage = CurStage;

            if (newStage == oldStage || pawn == null) return;

            bool regressing = (int)newStage > (int)oldStage;
            if (regressing)
            {
                growthPoints *= 0.5f;   // 50 % carry on downward crossing

                // pottyProgress carry: reset to 0 at Infant; ×0.75 per stage boundary elsewhere.
                if (newStage == RegressionStage.Infant)
                {
                    pottyProgress = 0f;
                }
                else
                {
                    int steps = (int)newStage - (int)oldStage;
                    for (int i = 0; i < steps; i++)
                        pottyProgress *= 0.75f;
                    pottyProgress = Mathf.Clamp01(pottyProgress);
                }
            }
            // Stage up (recovery): pottyProgress is intentionally left unchanged.

            RegressionStageEffects.OnStageCrossed(pawn, oldStage, newStage);
        }

        /// <summary>
        /// Add growth points toward the next recovery moment.
        /// Capped at the current stage threshold.
        /// Called by external systems (potty training, caretaker interactions, etc.).
        /// </summary>
        public void AddGrowthPoints(float amount)
        {
            if (amount <= 0f) return;
            float threshold = CurrentThreshold;
            if (threshold >= float.MaxValue) return;    // Adult — ignore
            growthPoints = Mathf.Min(growthPoints + amount, threshold);
        }

        // One full stage width — used by the recovery letter to bump the bar.
        public const float StageSize = 0.25f;

        // ── Potty training API ─────────────────────────────────────────────────

        /// <summary>Increase pottyProgress by amount × traitMult, clamped to 1.</summary>
        public void ApplyPottyProgress(float amount, RegressionStage stage)
        {
            pottyProgress = stage switch
            {
                RegressionStage.Infant      => Mathf.Clamp(pottyProgress + amount, 0f, 0.15f),
                RegressionStage.Toddler     => Mathf.Clamp(pottyProgress + amount, 0f, 0.3f),
                RegressionStage.YoungChild  => Mathf.Clamp(pottyProgress + amount, 0f, 0.65f),
                RegressionStage.PreTeen     => Mathf.Clamp(pottyProgress + amount, 0f, 1f),
                _                           => Mathf.Clamp(pottyProgress + amount, 0f, 1f)
            };
        }

        /// <summary>
        /// Reduces pottyProgress by amount, floored at 0.05.
        /// Respects the daily cap of 2 hits — further accidents skip the potty penalty
        /// but regression damage still fires elsewhere.
        /// </summary>
        /// <returns>True if the penalty was applied; false if daily cap was reached.</returns>
        public bool ApplyAccidentPottyPenalty(float amount)
        {
            if (accidentPenaltyToday >= 2) return false;
            pottyProgress = Mathf.Max(0.05f, pottyProgress - amount);
            accidentPenaltyToday++;
            return true;
        }

        // ── Passive tick rates (per NeedInterval = 150 ticks, 400/day) ─────────
        // Positive = bar rises, negative = bar falls.
        private const float AdultDriftPerInterval       =  0.00005f;  
        private const float OnesieRegrPerInterval       = -0.00025f;  
        private const float PacifierRegrPerInterval     = -0.0001875f;
        private const float ExposedProtRegrPerInterval  = -0.000375f; 

        // ── NeedInterval ──────────────────────────────────────────────────────
        // No passive drain. Accumulates growth points using learning level as multiplier.
        public override void NeedInterval()
        {
            if (pawn?.needs == null) return;

            // ── Apparel-driven regression (all stages) ────────────────────────
            if (ZIR_ApparelUtility.HasOnesie(pawn))
                CurLevelPercentage = Mathf.Clamp01(CurLevelPercentage + OnesieRegrPerInterval);
            if (ZIR_ApparelUtility.HasPacifier(pawn))
                CurLevelPercentage = Mathf.Clamp01(CurLevelPercentage + PacifierRegrPerInterval);
            if (ZIR_ApparelUtility.HasExposedProtection(pawn))
                CurLevelPercentage = Mathf.Clamp01(CurLevelPercentage + ExposedProtRegrPerInterval);

            // ── Adult stage: natural drift toward 1.0, no growth points ──────
            if (CurStage == RegressionStage.Adult)
            {
                if (CurLevelPercentage < 1f)
                    CurLevelPercentage = Mathf.Min(1f, CurLevelPercentage + AdultDriftPerInterval);
                return;
            }

            // Reset daily accident-penalty cap at midnight.
            int today = GenDate.DaysPassed;
            if (lastDayReset != today)
            {
                lastDayReset        = today;
                accidentPenaltyToday = 0;
            }

            // ── Prisoner resistance / will drain ──────────────────────────────
            DrainPrisonerResistanceWill();

            // Accumulate: rate = lerp(floor, max, learningLevel) pts/day
            // NeedInterval fires every 150 ticks → 400 intervals/day
            float rate = GrowthPointsRatePerDay;
            if (rate > 0f)
                AddGrowthPoints(rate / 400f);

            // Fire recovery moment when threshold reached
            if (growthPoints >= CurrentThreshold)
                ChoiceLetter_RegressionRecovery.Send(pawn, this);
        }

        // ── Prisoner resistance / will drain ─────────────────────────────────
        // Drain rates per day (fractional, applied proportionally per NeedInterval):
        //   PreTeen    −20% resistance / −10% will
        //   YoungChild −40% resistance / −25% will
        //   Toddler    −70% resistance / −50% will
        //   Infant     clamped to 0 immediately
        // NeedInterval = 150 ticks; 400 intervals/day → per-interval factor = rate/400.
        private static readonly float[] ResistDrainPerDay = { 0f, 0.20f, 0.40f, 0.70f, 1f };
        private static readonly float[] WillDrainPerDay   = { 0f, 0.10f, 0.25f, 0.50f, 1f };

        private void DrainPrisonerResistanceWill()
        {
            if (pawn?.guest == null) return;
            if (!pawn.IsPrisoner && !pawn.IsSlave) return;

            int idx = (int)CurStage; // Adult=0, PreTeen=1, YoungChild=2, Toddler=3, Infant=4

            if (CurStage == RegressionStage.Infant)
            {
                if (pawn.guest.resistance > 0f) pawn.guest.resistance = 0f;
                if (pawn.guest.will       > 0f) pawn.guest.will       = 0f;
                return;
            }

            // 400 NeedIntervals per day
            if (pawn.guest.resistance > 0f)
                pawn.guest.resistance = Mathf.Max(0f, pawn.guest.resistance - pawn.guest.resistance * ResistDrainPerDay[idx] / 400f);

            if (pawn.guest.will > 0f)
                pawn.guest.will = Mathf.Max(0f, pawn.guest.will - pawn.guest.will * WillDrainPerDay[idx] / 400f);
        }

        // ── Dev-mode helpers ──────────────────────────────────────────────────
        protected override void OffsetDebugPercent(float offsetPercent)
        {
            AdjustRegression(offsetPercent > 0f ? 0.025f : -0.025f);
        }

        public override string GetTipString()
        {
            string stageName = CurStage switch
            {
                RegressionStage.Adult      => "Adult",
                RegressionStage.PreTeen    => "Pre-Teen",
                RegressionStage.YoungChild => "Young Child",
                RegressionStage.Toddler    => "Toddler",
                RegressionStage.Infant     => "Infant",
                _                          => "Unknown"
            };

            return (LabelCap + ": " + stageName).Colorize(ColoredText.TipSectionTitleColor)
                + "\n" + def.description
                + "\n\n" + "Current level: ".Colorize(ColoredText.SubtleGrayColor)
                + CurLevelPercentage.ToStringPercent();
        }
    }
}
