using DubsBadHygiene;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ZealousInnocenceRegression.Patches
{
    /// <summary>
    /// Primary hook: ZI's Need_Diaper.startAccident() — fires for every accident type
    /// (bedwetting, bladder fail, trait-based, panic) when ZI is active.
    ///
    /// Fallback hook: DBH's Need_Bladder.crapPants() — fires only when ZI is NOT active,
    /// or in the rare edge-case where a pawn has no diaper/underwear and ZI falls through.
    ///
    /// Both are patched via manual Harmony registration in ZIRegressionMod to avoid
    /// baking ZealousInnocence type tokens into attributes (optional-mod safety).
    /// </summary>
    public static class Patch_BladderAccident
    {
        // Called by ZIRegressionMod at startup — avoids [HarmonyPatch(typeof(ZI type))]
        // which would TypeLoadException if ZI were ever absent.
        public static void Register(HarmonyLib.Harmony harmony)
        {
            // --- ZI hook (primary) ---
            var ziType = AccessTools.TypeByName("ZealousInnocence.Need_Diaper");
            if (ziType != null)
            {
                var startAccident = AccessTools.Method(ziType, "startAccident");
                if (startAccident != null)
                {
                    harmony.Patch(startAccident,
                        postfix: new HarmonyMethod(typeof(Patch_BladderAccident),
                            nameof(Postfix_StartAccident)));
                    Log.Message("[ZIR] Patched ZealousInnocence.Need_Diaper.startAccident");
                }
                else
                    Log.Warning("[ZIR] Could not find Need_Diaper.startAccident — ZI accident hook skipped.");
            }
            else
                Log.Warning("[ZIR] ZealousInnocence.Need_Diaper not found — ZI accident hook skipped.");

            // --- DBH fallback hook ---
            var dbhCrapPants = AccessTools.Method(typeof(Need_Bladder), "crapPants");
            if (dbhCrapPants != null)
            {
                harmony.Patch(dbhCrapPants,
                    postfix: new HarmonyMethod(typeof(Patch_BladderAccident),
                        nameof(Postfix_CrapPants)));
                Log.Message("[ZIR] Patched DubsBadHygiene.Need_Bladder.crapPants");
            }
        }

        // Postfix for ZI's Need_Diaper.startAccident(bool pee)
        public static void Postfix_StartAccident(object __instance)
        {
            Pawn pawn = Traverse.Create(__instance).Field<Pawn>("pawn").Value;
            ApplyPenalty(pawn);
        }

        // Postfix for DBH's Need_Bladder.crapPants()
        public static void Postfix_CrapPants(Need_Bladder __instance)
        {
            Pawn pawn = Traverse.Create(__instance).Field<Pawn>("pawn").Value;
            ApplyPenalty(pawn);
        }

        private static void ApplyPenalty(Pawn pawn)
        {
            if (pawn?.needs == null) return;

            bool awake      = pawn.Awake();
            bool bedwetting = !awake && RestUtility.InBed(pawn) && !pawn.Downed;

            // Truly unconscious (downed, anaesthetised) — no regression.
            // Bedwetting while asleep in bed is a distinct, reduced-impact event.
            if (!awake && !bedwetting) return;

            var regression = pawn.needs.TryGetNeed<Need_Regression>();
            if (regression == null) return;

            Apparel diaper  = DiaperUtils.GetWornDiaper(pawn);
            float baseHit   = DiaperUtils.AccidentBase * DiaperUtils.KindMultiplier(diaper);

            // Bedwetting: half regression impact.
            if (bedwetting) baseHit *= 0.5f;

            // pottyProgress scales regression damage: well-trained pawn takes more per accident.
            float damageMult = ZIR_PottyUtility.GetAccidentDamageMult(
                regression.CurStage, regression.pottyProgress);
            regression.AdjustRegression(baseHit * damageMult);

            // pottyProgress penalty (daily cap of 2 hits enforced inside the method).
            float pottyPenalty = bedwetting ? 0.05f : 0.10f;
            regression.ApplyAccidentPottyPenalty(pottyPenalty);

            // DBH's crapPants() fires the adult "SoiledSelf" shame thought.
            // For Toddler/Infant stages, adult shame is inappropriate — remove it.
            // (The regression thought system already handles the psychological impact.)
            if (regression.CurStage == RegressionStage.Toddler ||
                regression.CurStage == RegressionStage.Infant)
            {
                var td = DefDatabase<ThoughtDef>.GetNamedSilentFail("SoiledSelf");
                if (td != null)
                    pawn.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(td);
            }
        }
    }
}
