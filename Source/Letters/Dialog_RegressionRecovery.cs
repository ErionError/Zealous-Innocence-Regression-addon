using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using GUI = UnityEngine.GUI;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Simple dialog for the regression recovery growth moment.
    /// Shows passion choices + trait choices; player picks one of each.
    /// </summary>
    public class Dialog_RegressionRecovery : Window
    {
        private readonly ChoiceLetter_RegressionRecovery letter;
        private SkillDef selectedPassion;
        private Trait    selectedTrait;

        public override Vector2 InitialSize => new Vector2(540f, 480f);

        public Dialog_RegressionRecovery(ChoiceLetter_RegressionRecovery letter)
        {
            this.letter      = letter;
            forcePause       = true;
            absorbInputAroundWindow = true;
            closeOnAccept    = false;
            closeOnCancel    = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Pawn pawn = letter.pawn;

            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 40f),
                "ZIR_RecoveryDialog_Title".Translate(pawn.LabelShortCap));
            Text.Font = GameFont.Small;

            float y = 50f;

            // Flavour
            Widgets.Label(new Rect(0f, y, inRect.width, 60f),
                "ZIR_RecoveryDialog_Desc".Translate(pawn.LabelShort));
            y += 65f;

            // ─── Passion choices ─────────────────────────────────────────────
            if (!letter.passionOptions.NullOrEmpty())
            {
                Widgets.Label(new Rect(0f, y, inRect.width, 24f),
                    "ZIR_RecoveryDialog_PassionHeader".Translate());
                y += 28f;

                foreach (SkillDef skill in letter.passionOptions)
                {
                    bool chosen = selectedPassion == skill;
                    Rect row = new Rect(10f, y, inRect.width - 10f, 30f);

                    if (Widgets.RadioButtonLabeled(row, skill.LabelCap, chosen))
                        selectedPassion = skill;

                    y += 34f;
                }
                y += 8f;
            }

            // ─── Trait choices ───────────────────────────────────────────────
            if (!letter.traitOptions.NullOrEmpty())
            {
                Widgets.Label(new Rect(0f, y, inRect.width, 24f),
                    "ZIR_RecoveryDialog_TraitHeader".Translate());
                y += 28f;

                foreach (Trait trait in letter.traitOptions)
                {
                    bool chosen = selectedTrait == trait;
                    Rect row = new Rect(10f, y, inRect.width - 10f, 30f);

                    if (Widgets.RadioButtonLabeled(row, trait.LabelCap, chosen))
                        selectedTrait = trait;

                    y += 34f;
                }
                y += 8f;

                // "None" option
                Rect noneRow = new Rect(10f, y, inRect.width - 10f, 30f);
                if (Widgets.RadioButtonLabeled(noneRow, "ZIR_RecoveryDialog_NoTrait".Translate(),
                    selectedTrait == null))
                    selectedTrait = null;
                y += 34f;
            }

            // ─── Confirm button ──────────────────────────────────────────────
            Rect confirmRect = new Rect(inRect.width / 2f - 80f, inRect.height - 40f, 160f, 36f);
            bool canConfirm  = selectedPassion != null || letter.passionOptions.NullOrEmpty();

            if (canConfirm && Widgets.ButtonText(confirmRect, "Confirm".Translate()))
            {
                SoundDefOf.Quest_Succeded.PlayOneShotOnCamera();
                letter.Resolve(selectedPassion, selectedTrait);
                Close();
            }
            else if (!canConfirm)
            {
                // Greyed out hint
                GUI.color = Color.grey;
                Widgets.ButtonText(confirmRect, "Confirm".Translate());
                GUI.color = Color.white;
                TooltipHandler.TipRegionByKey(confirmRect, "ZIR_RecoveryDialog_PickPassion");
            }
        }
    }
}
