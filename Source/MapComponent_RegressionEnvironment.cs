using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Hourly (2500 tick) checks:
    ///   1. Nursery room presence → regression penalty
    ///   2. Worn diaper state → threshold penalty × kind multiplier
    ///   3. Diaper destruction between checks → ruin spike
    /// </summary>
    public class MapComponent_RegressionEnvironment : MapComponent
    {
        private const int CheckInterval = 7500; // 1 in-game hour
        private const float NurseryPenalty = -0.005f;

        // Tracks each pawn's last-known diaper state for ruin detection
        // Key: pawn.thingIDNumber, Value: (defName, lastHpFraction)
        private readonly Dictionary<int, (string defName, float hpFrac)> _lastDiaper = new();

        public MapComponent_RegressionEnvironment(Map map) : base(map) { }

        /// <summary>
        /// Sync regression hediffs for all spawned pawns after the map finishes loading.
        /// Handles saves made before the hediff system existed.
        /// </summary>
        public override void FinalizeInit()
        {
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                var reg = pawn.needs?.TryGetNeed<Need_Regression>();
                if (reg != null)
                    RegressionStageEffects.ApplyRegressionHediff(pawn, reg.CurStage);
            }
        }

        // Observer thoughts are refreshed once per in-game day.
        private const int ObserverInterval = 60000;

        public override void MapComponentTick()
        {
            int tick = Find.TickManager.TicksGame;

            // Daily: refresh proximity observer memories on all colonists
            if (tick % ObserverInterval == 0)
                FireObserverThoughts();

            if (tick % CheckInterval != 0) return;

            var allPawns = map.mapPawns.AllPawnsSpawned;

            foreach (Pawn pawn in allPawns)
            {
                if (pawn.needs == null) continue;
                var regression = pawn.needs.TryGetNeed<Need_Regression>();
                if (regression == null) continue;

                // --- Nearby stink (mirrors ZI's Stink ThoughtWorker stages) ---
                float stink = StinkScore(pawn, allPawns);
                if (stink > 0f)
                {
                    // Three tiers matching ZI's Stink thought: light / serious / overwhelming
                    float stinkPenalty = stink > 2.5f ? -0.003f
                                      : stink > 1.2f ? -0.002f
                                                     : -0.001f;
                    RegressionStageEffects.ApplyArealRegression(pawn, stinkPenalty);
                }

                // --- Nursery room ---
                Room room = pawn.GetRoom();
                if (room != null && IsNurseryRoom(room))
                    regression.AdjustRegression(NurseryPenalty);

                // --- Diaper state ---
                int id = pawn.thingIDNumber;
                Apparel diaper = DiaperUtils.GetWornDiaper(pawn);

                if (diaper != null)
                {
                    // Hourly threshold penalty
                    regression.AdjustRegression(DiaperUtils.ThresholdPenalty(diaper));

                    // Update snapshot
                    _lastDiaper[id] = (diaper.def.defName,
                        (float)diaper.HitPoints / diaper.MaxHitPoints);
                }
                else
                {
                    // Was wearing a diaper last check but no longer → it was ruined
                    if (_lastDiaper.TryGetValue(id, out var snap))
                    {
                        float ruinPenalty = DiaperUtils.AccidentBase
                            * DiaperUtils.RuinMultiplier
                            * DiaperUtils.KindMultiplier(
                                DefDatabase<ThingDef>.GetNamedSilentFail(snap.defName));
                        regression.AdjustRegression(ruinPenalty);
                    }
                    _lastDiaper.Remove(id);
                }
            }
        }

        // ── Nearby stink ─────────────────────────────────────────────────────────
        /// <summary>
        /// Mirrors ZI's ThoughtWorker_Stink radius/scoring logic.
        /// Returns a stink score: sum of (0.6 - hpFrac) for each stinky diaper
        /// within 6 cells. Score > 0 means at least one stinky pawn is nearby.
        /// </summary>
        private static float StinkScore(Pawn observer, IEnumerable<Pawn> allPawns)
        {
            const float StinkRadius  = 6f;
            const float StinkCutoff  = 0.6f;

            float score = 0f;
            foreach (Pawn other in allPawns)
            {
                if (other == observer) continue;
                Apparel diaper = DiaperUtils.GetWornDiaper(other);
                if (diaper == null) continue;
                float hpFrac = (float)diaper.HitPoints / diaper.MaxHitPoints;
                if (hpFrac >= StinkCutoff) continue;
                if (!other.Position.InHorDistOf(observer.Position, StinkRadius)) continue;
                score += StinkCutoff - hpFrac;
            }
            return score;
        }

        // ── Room detection ─────────────────────────────────────────────────────────
        private static bool IsNurseryRoom(Room room) => ZIR_RoomUtility.IsNurseryRoom(room);

        // ── Observer thoughts ────────────────────────────────────────────────────
        private void FireObserverThoughts()
        {
            const float ProximityRadius = 30f;
            var colonists = map.mapPawns.FreeColonistsSpawned;

            foreach (Pawn observer in colonists)
            {
                if (observer.needs?.mood == null) continue;

                RegressionStage worst = RegressionStage.Adult;
                bool found = false;

                foreach (Pawn other in colonists)
                {
                    if (other == observer) continue;
                    var reg = other.needs?.TryGetNeed<Need_Regression>();
                    if (reg == null || reg.CurStage == RegressionStage.Adult) continue;
                    if (!other.Position.InHorDistOf(observer.Position, ProximityRadius)) continue;

                    if (!found || (int)reg.CurStage > (int)worst)
                    {
                        worst = reg.CurStage;
                        found = true;
                    }
                }

                if (found)
                    ZIR_ThoughtUtility.FireObserverMemory(observer, worst);
            }
        }
    }
}
