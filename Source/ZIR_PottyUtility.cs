using RimWorld;
using UnityEngine;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Central helper for the potty-training system.
    /// Trait multipliers, BladderControl bonus, accident damage scaling,
    /// and the toilet-success callback all live here.
    /// </summary>
    public static class ZIR_PottyUtility
    {
        // ── Caretaker cooldown ────────────────────────────────────────────────
        /// <summary>6 hours in ticks (15 000).</summary>
        public const int CaretakerCooldownTicks = 15_000;

        // ── Trait multipliers ─────────────────────────────────────────────────

        /// <summary>
        /// Returns the multiplier applied to all pottyProgress gains and bar-reversal
        /// amounts for this pawn.
        /// <para>
        /// Potty_Rebel: 0× for self-directed use; 0.5× for caretaker sessions.
        /// Diaper_Lover: 0.25× (not really trying).
        /// Big_Boy (Self-Conscious): 1.3× (motivated to recover).
        /// </para>
        /// </summary>
        public static float GetProgressMultiplier(Pawn pawn, bool caretakerSession = false)
        {
            var traits = pawn.story?.traits;
            if (traits == null) return 1f;

            if (traits.HasTrait(ZIR_TraitDefOf.Potty_Rebel))
                return caretakerSession ? 0.5f : 0f;

            if (traits.HasTrait(ZIR_TraitDefOf.Diaper_Lover))
                return 0.25f;

            if (traits.HasTrait(ZIR_TraitDefOf.Big_Boy))
                return 1.3f;

            return 1f;
        }

        // ── BladderControl bonus ──────────────────────────────────────────────

        /// <summary>
        /// Additive bonus to BladderControl capacity based on pottyProgress.
        /// Added AFTER ZI's existing pipeline (age factor × strength × sleep factor).
        /// Sleep factor is intentionally NOT applied to this bonus — potty training
        /// helps daytime control only; bedwetting is managed separately.
        /// </summary>
        public static float GetBladderControlBonus(RegressionStage stage, float pottyProgress)
        {
            float stageMax = stage switch
            {
                RegressionStage.PreTeen    => 0.38f,
                RegressionStage.YoungChild => 0.28f,
                RegressionStage.Toddler    => 0.18f,
                RegressionStage.Infant     => 0.08f,
                _                          => 0f
            };
            return pottyProgress * stageMax;
        }

        // ── Accident damage scaling ───────────────────────────────────────────

        /// <summary>
        /// Multiplier applied on top of kindMult to the regression bar penalty
        /// when an accident occurs. High pottyProgress = softer hit (the trained
        /// brain is more resilient); low pottyProgress = harder hit.
        /// Infant is capped 0.8–1.0 to prevent compounding with the potty reset.
        /// </summary>
        public static float GetAccidentDamageMult(RegressionStage stage, float pottyProgress)
        {
            return stage switch
            {
                RegressionStage.PreTeen    => Mathf.Lerp(0.6f, 1.4f, pottyProgress),
                RegressionStage.YoungChild => Mathf.Lerp(0.6f, 1.4f, pottyProgress),
                RegressionStage.Toddler    => Mathf.Lerp(0.7f, 1.2f, pottyProgress),
                RegressionStage.Infant     => Mathf.Lerp(0.8f, 1.0f, pottyProgress),
                _                          => 1f
            };
        }

        // ── Toilet success callback ───────────────────────────────────────────

        /// <summary>
        /// Call when a pawn successfully uses a toilet (self-directed) or completes a
        /// caretaker session. Applies pottyProgress gain, regression bar reversal,
        /// learning bump (PreTeen/YoungChild), direct growthPoints (Toddler/Infant),
        /// and the ZIR_PottySuccess mood thought.
        /// </summary>
        /// <param name="patient">The regressed pawn who used/was trained at the toilet.</param>
        /// <param name="selfDirected">True for DBH UseToilet; false for caretaker sessions.</param>
        public static void OnSuccessfulToiletUse(Pawn patient, bool selfDirected)
        {
            if (patient?.needs == null) return;
            var reg = patient.needs.TryGetNeed<Need_Regression>();
            if (reg == null || reg.CurStage == RegressionStage.Adult) return;

            float traitMult = GetProgressMultiplier(patient, caretakerSession: !selfDirected);

            // ── pottyProgress gain ────────────────────────────────────────────
            float progressGain = 0f;
            if (selfDirected)
            {
                progressGain = reg.CurStage switch
                {
                    RegressionStage.PreTeen    => 0.03f,
                    RegressionStage.YoungChild => 0.02f,
                    RegressionStage.Toddler    => 0.01f,
                    RegressionStage.Infant     => 0f,
                    _                          => 0f
                };
            }
            else // caretaker
            {
                progressGain = reg.CurStage switch
                {
                    RegressionStage.PreTeen    => 0.02f,
                    RegressionStage.YoungChild => 0.02f,
                    RegressionStage.Toddler    => 0.01f,
                    RegressionStage.Infant     => 0.01f,
                    _                          => 0f
                };
            }

            if (progressGain > 0f)
                reg.ApplyPottyProgress(progressGain * traitMult, reg.CurStage);

            // ── Regression bar reversal (self-directed and caretaker, trait-scaled) ──
            // Caretaker sessions give slightly less reversal than self-directed,
            // but DO push back regression at Toddler/Infant since those stages
            // have no self-directed toilet use at all.
            if (traitMult > 0f)
            {
                float reversal = selfDirected
                    ? reg.CurStage switch
                    {
                        RegressionStage.PreTeen    => 0.015f,
                        RegressionStage.YoungChild => 0.012f,
                        _                          => 0f
                    }
                    : reg.CurStage switch  // caretaker
                    {
                        RegressionStage.PreTeen    => 0.010f,
                        RegressionStage.YoungChild => 0.008f,
                        RegressionStage.Toddler    => 0.006f,
                        RegressionStage.Infant     => 0.003f,
                        _                          => 0f
                    };

                if (reversal > 0f)
                {
                    float ceiling = reg.CurStage switch
                    {
                        // -0.002f: cannot silently cross into next stage.
                        // Proper stage recovery must always go through the recovery letter.
                        RegressionStage.PreTeen    => Need_Regression.ThresholdPreTeen    - 0.002f,
                        RegressionStage.YoungChild => Need_Regression.ThresholdYoungChild - 0.002f,
                        RegressionStage.Toddler    => Need_Regression.ThresholdToddler    - 0.002f,
                        RegressionStage.Infant     => Need_Regression.ThresholdInfant     - 0.002f,
                        _                          => reg.CurLevelPercentage
                    };
                    reg.CurLevelPercentage = Mathf.Min(
                        reg.CurLevelPercentage + reversal * traitMult, ceiling);
                }
            }

            // ── Learning bump (PreTeen / YoungChild) ──────────────────────────
            if (ModsConfig.BiotechActive)
            {
                var learning = patient.needs?.TryGetNeed<Need_Learning>();
                if (learning != null && !learning.Suspended)
                {
                    float bump = selfDirected
                        ? reg.CurStage switch
                        {
                            RegressionStage.PreTeen    => 0.03f,
                            RegressionStage.YoungChild => 0.02f,
                            _                          => 0f
                        }
                        : reg.CurStage switch
                        {
                            RegressionStage.PreTeen    => 0.04f,
                            RegressionStage.YoungChild => 0.03f,
                            _                          => 0f
                        };

                    if (bump > 0f)
                        learning.CurLevel = Mathf.Clamp01(learning.CurLevel + bump);
                }
            }

            // ── Direct growthPoints for Toddler / Infant (caretaker only) ────
            // At these stages the learning need is frozen so we bypass it entirely.
            if (!selfDirected)
            {
                float gp = reg.CurStage switch
                {
                    RegressionStage.Toddler => 6f,
                    RegressionStage.Infant  => 3f,
                    _                       => 0f
                };
                if (gp > 0f) reg.AddGrowthPoints(gp);
            }

            // ── ZIR_PottySuccess mood thought (all regressed stages) ──────────
            var memories = patient.needs?.mood?.thoughts?.memories;
            if (memories != null)
            {
                int traitIdx = ZIR_ThoughtUtility.GetTraitOffset(patient);
                int rIdx     = (int)reg.CurStage;
                var successThought = (Thought_Memory)ThoughtMaker.MakeThought(
                    ZIR_ThoughtDefOf.ZIR_PottySuccess, traitIdx * 5 + rIdx);
                memories.TryGainMemory(successThought);
            }
        }
    }
}
