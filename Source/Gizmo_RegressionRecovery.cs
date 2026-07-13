using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Bottom-bar gizmo showing recovery progress, learning (PreTeen/YoungChild),
    /// and potty-training progress for a regressed pawn.
    ///
    /// Two-row layout (Toddler / Infant):
    ///   Row 1 — Recovery bar (growthPoints / threshold)
    ///   Row 2 — Potty-training bar (amber)
    ///
    /// Three-row layout (PreTeen / YoungChild, Biotech active):
    ///   Row 1 — Recovery bar
    ///   Row 2 — Learning bar (cyan, feeds growthPoints via existing chain)
    ///   Row 3 — Potty-training bar (amber, feeds learning)
    /// </summary>
    [StaticConstructorOnStartup]
    public class Gizmo_RegressionRecovery : Gizmo
    {
        // Textures
        private static readonly Texture2D EmptyBarTex =
            SolidColorMaterials.NewSolidColorTexture(GenUI.FillableBar_Empty);

        private static readonly Texture2D RecoveryBarTex =
            SolidColorMaterials.NewSolidColorTexture(new Color(0.125f, 0.659f, 0.545f));

        private static readonly Texture2D PottyBarTex =
            SolidColorMaterials.NewSolidColorTexture(new Color(0.85f, 0.60f, 0.10f));

        // Layout
        private const float GizmoWidth      = 190f;
        private const float LabelFraction   = 0.55f;
        private const float BarMarginY      = 2f;
        private const float TwoRowHeight    = 75f;
        private const float ThreeRowHeight  = 105f;

        // Fields
        private readonly Pawn            pawn;
        private readonly Need_Regression regression;

        public Gizmo_RegressionRecovery(Pawn pawn, Need_Regression regression)
        {
            this.pawn       = pawn;
            this.regression = regression;
            Order = -99f;
        }

        public override bool Visible =>
            regression.CurStage != RegressionStage.Adult &&
            !pawn.DevelopmentalStage.Child() &&
            (pawn.IsColonistPlayerControlled || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony);

        public override float GetWidth(float maxWidth) => GizmoWidth;

        private bool IsThreeRowStage =>
            regression.CurStage == RegressionStage.PreTeen ||
            regression.CurStage == RegressionStage.YoungChild;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            bool threeRows = IsThreeRowStage && ModsConfig.BiotechActive;
            float height   = threeRows ? ThreeRowHeight : TwoRowHeight;

            Rect outer = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), height);
            Rect inner = outer.ContractedBy(8f);
            Widgets.DrawWindowBackground(outer);

            if (threeRows)
            {
                float rowH = inner.height / 3f;
                Rect row1 = new Rect(inner.x, inner.y,            inner.width, rowH);
                Rect row2 = new Rect(inner.x, inner.y + rowH,     inner.width, rowH);
                Rect row3 = new Rect(inner.x, inner.y + rowH * 2, inner.width, rowH);
                row1.yMax -= BarMarginY;
                row2.yMin += BarMarginY; row2.yMax -= BarMarginY;
                row3.yMin += BarMarginY;
                DrawRecoveryRow(row1);
                DrawLearningRow(row2);
                DrawPottyRow(row3);
            }
            else
            {
                float halfH = inner.height / 2f;
                Rect topRow = new Rect(inner.x, inner.y,         inner.width, halfH);
                Rect botRow = new Rect(inner.x, inner.y + halfH, inner.width, halfH);
                topRow.yMax -= BarMarginY;
                botRow.yMin += BarMarginY;
                DrawRecoveryRow(topRow);
                DrawPottyRow(botRow);
            }

            return new GizmoResult(GizmoState.Clear);
        }

        // Recovery row
        private void DrawRecoveryRow(Rect rect)
        {
            float threshold = regression.CurrentThreshold;
            float fill      = threshold > 0f ? Mathf.Clamp01(regression.growthPoints / threshold) : 0f;

            Rect labelRect = new Rect(rect.x, rect.y, rect.width * LabelFraction, rect.height);
            Text.Font   = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, "ZIR_Recovery".Translate() + ":");
            Text.Anchor = TextAnchor.UpperLeft;

            Rect barRect = new Rect(labelRect.xMax, rect.y + BarMarginY,
                                    rect.xMax - labelRect.xMax, rect.height - BarMarginY * 2f);
            Widgets.FillableBar(barRect, fill, RecoveryBarTex, EmptyBarTex, doBorder: true);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(barRect,
                $"{Mathf.FloorToInt(regression.growthPoints)} / {Mathf.FloorToInt(threshold)}");
            Text.Anchor = TextAnchor.UpperLeft;

            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                TooltipHandler.TipRegion(rect,
                    new TipSignal(BuildRecoveryTooltip(), pawn.thingIDNumber ^ 0x4A1B2C3D));
            }
        }

        private string BuildRecoveryTooltip()
        {
            var   sb        = new StringBuilder();
            float threshold = regression.CurrentThreshold;
            float rate      = regression.GrowthPointsRatePerDay;

            sb.Append(("ZIR_Recovery".Translate() + ": ").AsTipTitle());
            sb.Append($"{Mathf.FloorToInt(regression.growthPoints)} / {Mathf.FloorToInt(threshold)}");

            if (rate > 0f)
                sb.Append($"  (+{rate:F1} " + "ZIR_PtsPerDay".Translate() + ")");
            else
            {
                sb.AppendLine();
                sb.Append("ZIR_EventDriven".Translate().Colorize(ColoredText.SubtleGrayColor));
            }

            sb.AppendLine().AppendLine();
            sb.Append(("ZIR_CurrentStage".Translate() + ": ").AsTipTitle());
            sb.Append(regression.CurStage.ToLabel());
            return sb.ToString();
        }

        // Learning row (PreTeen / YoungChild only — called inside three-row path)
        private void DrawLearningRow(Rect rect)
        {
            if (!ModsConfig.BiotechActive) return;
            Need_Learning learning = pawn.needs?.TryGetNeed<Need_Learning>();
            if (learning == null) return;

            Rect labelRect = new Rect(rect.x, rect.y, rect.width * LabelFraction, rect.height);
            Text.Font   = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, learning.def.LabelCap);
            Text.Anchor = TextAnchor.UpperLeft;

            Rect barRect = new Rect(labelRect.xMax, rect.y + BarMarginY,
                                    rect.xMax - labelRect.xMax, rect.height - BarMarginY * 2f);
            Widgets.FillableBar(barRect, learning.CurLevelPercentage,
                                Widgets.BarFullTexHor, EmptyBarTex, doBorder: true);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(barRect, learning.CurLevelPercentage.ToStringPercent());
            Text.Anchor = TextAnchor.UpperLeft;

            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                TooltipHandler.TipRegion(rect, learning.GetTipString());
            }
        }

        // Potty-training row (all regressed stages)
        private void DrawPottyRow(Rect rect)
        {
            float fill = Mathf.Clamp01(regression.pottyProgress);

            Rect labelRect = new Rect(rect.x, rect.y, rect.width * LabelFraction, rect.height);
            Text.Font   = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, "ZIR_PottyTraining".Translate() + ":");
            Text.Anchor = TextAnchor.UpperLeft;

            Rect barRect = new Rect(labelRect.xMax, rect.y + BarMarginY,
                                    rect.xMax - labelRect.xMax, rect.height - BarMarginY * 2f);
            Widgets.FillableBar(barRect, fill, PottyBarTex, EmptyBarTex, doBorder: true);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(barRect, fill.ToStringPercent());
            Text.Anchor = TextAnchor.UpperLeft;

            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                TooltipHandler.TipRegion(rect,
                    new TipSignal(BuildPottyTooltip(), pawn.thingIDNumber ^ 0x5C3D2E1F));
            }
        }

        private string BuildPottyTooltip()
        {
            var sb = new StringBuilder();
            sb.Append(("ZIR_PottyTraining".Translate() + ": ").AsTipTitle());
            sb.Append(regression.pottyProgress.ToStringPercent());
            sb.AppendLine().AppendLine();

            if (regression.CurStage == RegressionStage.Toddler ||
                regression.CurStage == RegressionStage.Infant)
                sb.Append("ZIR_PottyTooltip_NeedsCaretaker".Translate()
                    .Colorize(ColoredText.SubtleGrayColor));
            else
                sb.Append("ZIR_PottyTooltip_SelfTrain".Translate()
                    .Colorize(ColoredText.SubtleGrayColor));

            return sb.ToString();
        }
    }

    public static class RegressionStageExtensions
    {
        public static string ToLabel(this RegressionStage stage) => stage switch
        {
            RegressionStage.Adult      => "ZIR_Stage_Adult".Translate(),
            RegressionStage.PreTeen    => "ZIR_Stage_PreTeen".Translate(),
            RegressionStage.YoungChild => "ZIR_Stage_YoungChild".Translate(),
            RegressionStage.Toddler    => "ZIR_Stage_Toddler".Translate(),
            RegressionStage.Infant     => "ZIR_Stage_Infant".Translate(),
            _                          => stage.ToString()
        };
    }
}
