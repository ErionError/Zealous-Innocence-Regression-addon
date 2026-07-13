using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Situational observer thought: active when the observer is within 30 cells
    /// of at least one regressed colonist. Stage = worst regression stage visible.
    /// PreTeen=0  YoungChild=1  Toddler=2  Infant=3
    /// </summary>
    public class ThoughtWorker_ZIR_ObserverRegression : ThoughtWorker
    {
        private const float ProximityRadius = 30f;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p.needs?.mood == null || p.Map == null) return ThoughtState.Inactive;

            RegressionStage worst = RegressionStage.Adult;
            bool found = false;

            foreach (Pawn other in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (other == p) continue;

                var reg = other.needs?.TryGetNeed<Need_Regression>();
                if (reg == null || reg.CurStage == RegressionStage.Adult) continue;

                if (!other.Position.InHorDistOf(p.Position, ProximityRadius)) continue;

                if (!found || (int)reg.CurStage > (int)worst)
                {
                    worst = reg.CurStage;
                    found = true;
                }
            }

            if (!found) return ThoughtState.Inactive;

            int stageIdx = (int)worst - 1; // PreTeen=0 … Infant=3
            return ThoughtState.ActiveAtStage(stageIdx);
        }
    }
}
