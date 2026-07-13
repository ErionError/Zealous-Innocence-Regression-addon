using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Concurrent;

namespace ZealousInnocenceRegression.Patches
{
    /// <summary>
    /// Appends Gizmo_RegressionRecovery to the bottom command bar when a pawn
    /// is below Adult regression stage. Also suppresses the vanilla Gizmo_GrowthTier
    /// when our gizmo is active, so both never appear simultaneously.
    /// </summary>
    public static class Patch_PawnGetGizmos
    {
        // Cache per-patient: (gameTick, caretaker, hasFixture)
        // Refreshed at most every 180 ticks (~3 s at normal speed).
        private struct ScheduleCache
        {
            public int   tick;
            public Pawn  caretaker;
            public bool  hasFixture;
        }
        private static readonly Dictionary<Pawn, ScheduleCache> _scheduleCache
            = new Dictionary<Pawn, ScheduleCache>();
        private const int CacheTtl = 180;

        public static void Register(Harmony harmony)
        {
            var method = AccessTools.Method(typeof(Pawn), nameof(Pawn.GetGizmos));
            if (method == null)
            {
                Log.Warning("[ZIR] Could not find Pawn.GetGizmos — recovery gizmo will not appear.");
                return;
            }
            harmony.Patch(method,
                postfix: new HarmonyMethod(typeof(Patch_PawnGetGizmos), nameof(Postfix)));
        }

        // IEnumerable postfix: Harmony chains __result through this iterator.
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            if (__instance.needs == null)
            {
                foreach (Gizmo g in __result) yield return g;
                yield break;
            }

            var regression = __instance.needs.TryGetNeed<Need_Regression>();
            bool showOurs = regression != null
                         && regression.CurStage != RegressionStage.Adult
                         && !__instance.DevelopmentalStage.Child();

            foreach (Gizmo g in __result)
            {
                // Suppress vanilla GrowthTier when our recovery gizmo is active.
                if (showOurs && g is Gizmo_GrowthTier) continue;
                yield return g;
            }

            if (showOurs)
            {
                yield return new Gizmo_RegressionRecovery(__instance, regression);
                yield return BuildScheduleButton(__instance, regression);
            }
        }

        private static Command_Action BuildScheduleButton(Pawn patient, Need_Regression reg)
        {
            bool onCooldown = reg.lastPottySessionTick >= 0 &&
                              Find.TickManager.TicksGame - reg.lastPottySessionTick
                              < ZIR_PottyUtility.CaretakerCooldownTicks;

            // Refresh caretaker + fixture lookup at most every CacheTtl ticks.
            int now = Find.TickManager.TicksGame;
            if (!_scheduleCache.TryGetValue(patient, out ScheduleCache cached)
                || now - cached.tick >= CacheTtl)
            {
                cached.tick       = now;
                cached.caretaker  = patient.Map?.mapPawns?.FreeColonistsSpawned
                    .FirstOrDefault(p => p != patient
                                     && !p.Downed
                                     && !p.InMentalState
                                     && p.workSettings?.WorkIsActive(WorkTypeDefOf.Childcare) == true);
                cached.hasFixture = HasReachableFixture(patient);
                _scheduleCache[patient] = cached;
            }

            Pawn caretaker   = cached.caretaker;
            bool noCaretaker = caretaker == null;
            bool noFixture   = !cached.hasFixture;
            bool canSchedule = !onCooldown && !noCaretaker && !noFixture;

            string tooltip;
            if (onCooldown)
            {
                int ticksLeft = ZIR_PottyUtility.CaretakerCooldownTicks
                                - (Find.TickManager.TicksGame - reg.lastPottySessionTick);
                tooltip = "ZIR_PottySchedule_Cooldown".Translate(
                    ticksLeft.ToStringTicksToPeriod());
            }
            else if (noCaretaker)
                tooltip = "ZIR_PottySchedule_NoCaretaker".Translate();
            else if (noFixture)
                tooltip = "ZIR_PottySchedule_NoFixture".Translate();
            else
                tooltip = "ZIR_PottySchedule_Tip".Translate(caretaker!.LabelShort);

            var cmd = new Command_Action
            {
                defaultLabel   = "ZIR_PottySchedule".Translate(),
                defaultDesc    = tooltip,
                icon           = TexCommand.ForbidOff,   // placeholder — replace if we add an icon
                Disabled       = !canSchedule,
                disabledReason = tooltip,
                action = () =>
                {
                    if (!canSchedule || caretaker == null) return;

                    // Find fixture again at action time (map state may have changed).
                    LocalTargetInfo fixture = FindFixture(caretaker);
                    if (!fixture.IsValid)
                    {
                        Messages.Message("ZIR_PottySchedule_NoFixture".Translate(),
                            patient, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    Job job = JobMaker.MakeJob(ZIR_DefOf.ZIR_PottyTraining, fixture, patient);
                    caretaker.jobs.TryTakeOrderedJob(job, JobTag.MiscWork);
                    Messages.Message(
                        "ZIR_PottyScheduled".Translate(caretaker.LabelShort, patient.LabelShort),
                        patient, MessageTypeDefOf.TaskCompletion, historical: false);
                }
            };
            return cmd;
        }

        private static bool HasReachableFixture(Pawn patient)
            => FindFixture(patient).IsValid;

        private static LocalTargetInfo FindFixture(Pawn searcher)
        {
            if (searcher?.Map == null) return LocalTargetInfo.Invalid;

            Thing chair = GenClosest.ClosestThingReachable(
                searcher.Position, searcher.Map,
                ThingRequest.ForDef(ZIR_DefOf.ZIR_PottyChair),
                PathEndMode.OnCell, TraverseParms.For(searcher), 40f,
                x => !x.IsForbidden(searcher) && searcher.CanReserve(x));
            if (chair != null) return chair;

            // DBH toilet fallback via reflection (same helper as WorkGiver)
            return WorkGiver_PottyTraining.FindToiletPublic(searcher);
        }
    }
}
