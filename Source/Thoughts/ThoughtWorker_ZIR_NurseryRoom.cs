using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Active while a regressed pawn is inside a qualifying nursery room.
    /// 20 stages: 4 trait profiles × 5 regression stages.
    /// Formula: traitIdx * 5 + regressionIdx
    /// </summary>
    public class ThoughtWorker_ZIR_NurseryRoom : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p.needs?.mood == null) return ThoughtState.Inactive;

            var reg = p.needs.TryGetNeed<Need_Regression>();
            if (reg == null) return ThoughtState.Inactive;

            Room room = p.GetRoom();
            if (!ZIR_RoomUtility.IsNurseryRoom(room))
                return ThoughtState.Inactive;

            int traitIdx = ZIR_ThoughtUtility.GetTraitOffset(p);
            int rIdx     = (int)reg.CurStage;

            return ThoughtState.ActiveAtStage(traitIdx * 5 + rIdx);
        }
    }
}
