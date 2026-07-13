using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Fires when a regressed pawn accumulates enough growth points to recover a stage.
    /// Player picks one passion to regain and optionally one trait.
    /// On confirmation: bar goes +0.25, growthPoints reset.
    /// </summary>
    public class ChoiceLetter_RegressionRecovery : ChoiceLetter
    {
        public Pawn pawn;

        // Cached on creation so choices don’t change while the letter sits in the stack
        public List<SkillDef> passionOptions = new List<SkillDef>();
        public List<Trait>    traitOptions   = new List<Trait>();

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                yield return new DiaOption("ZIR_RecoveryDialog_Open".Translate())
                {
                    action = OpenLetter,
                    resolveTree = true
                };
                yield return Option_Postpone;
            }
        }

        // ─── Factory ────────────────────────────────────────────────────────

        public static void Send(Pawn pawn, Need_Regression regression)
        {
            // Guard: don't stack multiple recovery letters for the same pawn
            foreach (var existing in Find.LetterStack.LettersListForReading)
            {
                if (existing is ChoiceLetter_RegressionRecovery r && r.pawn == pawn)
                    return;
            }

            var letter = (ChoiceLetter_RegressionRecovery)LetterMaker.MakeLetter(
                ZIR_DefOf.ZIR_RegressionRecovery);

            letter.pawn   = pawn;
            letter.Label  = "ZIR_RecoveryMoment_Label".Translate(pawn.LabelShort);
            letter.Text   = "ZIR_RecoveryMoment_Text".Translate(
                pawn.LabelShort, regression.CurStage.ToString());

            // Build passion options: skills that lost a passion or were degraded
            letter.passionOptions = DefDatabase<SkillDef>.AllDefsListForReading
                .Where(s => !pawn.skills.GetSkill(s).TotallyDisabled
                         && pawn.skills.GetSkill(s).passion != Passion.Major)
                .InRandomOrder()
                .Take(3)
                .ToList();

            // Build trait options: generate 2 possible traits
            letter.traitOptions = PawnGenerator.GenerateTraitsFor(pawn, 2, null, growthMomentTrait: true)
                ?? new List<Trait>();

            // Pause the game so the player notices
            Find.TickManager.Pause();
            Find.LetterStack.ReceiveLetter(letter);
        }

        // ─── Choices ────────────────────────────────────────────────────────

        public override void OpenLetter()
        {
            Find.WindowStack.Add(new Dialog_RegressionRecovery(this));
        }

        /// <summary>Called by Dialog_RegressionRecovery when player confirms.</summary>
        public void Resolve(SkillDef chosenPassion, Trait chosenTrait)
        {
            // Restore passion
            if (chosenPassion != null)
            {
                var skill = pawn.skills.GetSkill(chosenPassion);
                skill.passion = skill.passion.IncrementPassion();
            }

            // Restore trait
            if (chosenTrait != null)
                pawn.story.traits.GainTrait(chosenTrait);

            // Reset our own growthPoints so the next recovery moment requires re-earning
            var regression = pawn.needs.TryGetNeed<Need_Regression>();
            if (regression != null)
                regression.growthPoints = 0f;

            // Fire recovery memory on the pawn
            ZIR_ThoughtUtility.FireRecoveryMoment(pawn);

            // Advance regression bar by one full stage
            float advencementAmount = regression.CurStage switch
            {
                RegressionStage.PreTeen => Need_Regression.ThresholdPreTeen,
                RegressionStage.YoungChild => Need_Regression.ThresholdYoungChild,
                RegressionStage.Toddler => Need_Regression.ThresholdToddler,
                RegressionStage.Infant => Need_Regression.ThresholdInfant,
                _ => 0.0f
            };
            regression?.AdjustRegression(advencementAmount);

            // Dismiss the letter
            Find.LetterStack.RemoveLetter(this);
        }
    }
}
