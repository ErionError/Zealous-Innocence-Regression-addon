using DubsBadHygiene;
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Offers the ZIR_PottyTraining job to Childcare workers.
    /// Targets any colonist with Need_Regression below Adult whose caretaker
    /// cooldown (6 h) has expired and a valid fixture is reachable.
    /// </summary>
    public class WorkGiver_PottyTraining : WorkGiver_Scanner
    {
        // ── Fixture search ────────────────────────────────────────────────────
        private const float SearchRadius        = 40f;
        private const int   CooldownTicks       = ZIR_PottyUtility.CaretakerCooldownTicks;

        // Cached reflection into DBH's ClosestSanitation.FindBestToilet
        private static MethodInfo _findBestToilet;
        private static bool       _findBestToiletInitTried;

        private static bool TryInitToiletReflection()
        {
            if (_findBestToiletInitTried) return _findBestToilet != null;
            _findBestToiletInitTried = true;
            var type = AccessTools.TypeByName("DubsBadHygiene.ClosestSanitation");
            _findBestToilet = type?.GetMethod("FindBestToilet",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return _findBestToilet != null;
        }

        // Exposed for use by Patch_PawnGetGizmos schedule button.
        public static LocalTargetInfo FindToiletPublic(Pawn caretaker)
            => FindToilet(caretaker, SearchRadius);

        private static LocalTargetInfo FindToilet(Pawn caretaker, float radius)
        {
            // Priority 1: ZIR_PottyChair in range
            Thing chair = GenClosest.ClosestThingReachable(
                caretaker.Position, caretaker.Map,
                ThingRequest.ForDef(ZIR_DefOf.ZIR_PottyChair),
                PathEndMode.OnCell,
                TraverseParms.For(caretaker),
                radius,
                x => !x.IsForbidden(caretaker) && caretaker.CanReserve(x));

            if (chair != null) return chair;

            // Priority 2: any DBH toilet via reflection
            if (!TryInitToiletReflection()) return LocalTargetInfo.Invalid;
            try
            {
                var result = _findBestToilet.Invoke(null, new object[] { caretaker, false, radius });
                return result is LocalTargetInfo lti ? lti : LocalTargetInfo.Invalid;
            }
            catch { return LocalTargetInfo.Invalid; }
        }

        // ── WorkGiver overrides ───────────────────────────────────────────────
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn worker)
        {
            List<Pawn> pawns = new List<Pawn>();
            pawns.AddRange(worker.Map.mapPawns.FreeColonistsSpawned);
            pawns.AddRange(worker.Map.mapPawns.SlavesAndPrisonersOfColonySpawned);
            
            foreach (Pawn p in pawns)
            {
                if (p == worker) continue;
                var reg = p.needs?.TryGetNeed<Need_Regression>();
                if (reg == null || reg.CurStage == RegressionStage.Adult) continue;
                yield return p;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t is not Pawn patient) return false;
            if (!IsValidPatient(patient, pawn)) return false;
            return FindToilet(pawn, SearchRadius).IsValid;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t is not Pawn patient) return null;
            if (!IsValidPatient(patient, pawn)) return null;

            LocalTargetInfo fixture = FindToilet(pawn, SearchRadius);
            if (!fixture.IsValid) return null;

            return JobMaker.MakeJob(ZIR_DefOf.ZIR_PottyTraining, fixture, patient);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static bool IsValidPatient(Pawn patient, Pawn caretaker)
        {
            if (patient == caretaker) return false;
            if (!patient.Spawned || patient.Dead || patient.Downed) return false;

            var reg = patient.needs?.TryGetNeed<Need_Regression>();
            if (reg == null || reg.CurStage == RegressionStage.Adult) return false;

            // 6-hour cooldown since last session
            if (reg.lastPottySessionTick >= 0 &&
                Find.TickManager.TicksGame - reg.lastPottySessionTick < CooldownTicks)
                return false;

            // Caretaker must be capable
            if (!caretaker.health.capacities.CanBeAwake) return false;
            if (caretaker.Downed || caretaker.InMentalState) return false;

            return true;
        }
    }
}
