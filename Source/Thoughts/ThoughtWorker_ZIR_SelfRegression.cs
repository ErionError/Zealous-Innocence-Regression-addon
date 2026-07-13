using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Situational self-thought reflecting the pawn's current regression stage.
    /// Returns one of 16 stages: 4 trait profiles × 4 regression stages.
    /// Profile offset: Default=0  BB=4  PR=8  DL=12
    /// Stage  offset:  PreTeen=0  YoungChild=1  Toddler=2  Infant=3
    /// </summary>
    public class ThoughtWorker_ZIR_SelfRegression : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p.needs?.mood == null) return ThoughtState.Inactive;

            var reg = p.needs.TryGetNeed<Need_Regression>();
            if (reg == null || reg.CurStage == RegressionStage.Adult)
                return ThoughtState.Inactive;

            int stageOffset   = (int)reg.CurStage - 1;          // PreTeen=0 … Infant=3
            int profileOffset = ZIR_ThoughtUtility.GetTraitOffset(p) * 4; // 0,4,8,12

            return ThoughtState.ActiveAtStage(profileOffset + stageOffset);
        }
    }
}
