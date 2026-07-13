using HarmonyLib;
using Verse;

namespace ZealousInnocenceRegression
{
    public class ZIRegressionMod : Mod
    {
        public static Harmony HarmonyInst { get; private set; }

        public ZIRegressionMod(ModContentPack content) : base(content)
        {
            HarmonyInst = new Harmony("somerandomdude.ZealousInnocenceRegression");
            // Apply all [HarmonyPatch]-decorated patches in this assembly
            // (e.g. Patch_AccidentThoughts). Must run before manual registrations.
            HarmonyInst.PatchAll(typeof(ZIRegressionMod).Assembly);
            Patches.Patch_BladderAccident.Register(HarmonyInst);
            Patches.Patch_BladderControl.Register(HarmonyInst);
            Patches.Patch_LearningNeed.Register(HarmonyInst);
            Patches.Patch_ToiletSuccess.Register(HarmonyInst);
            Patches.Patch_PawnGetGizmos.Register(HarmonyInst);
            Patches.Patch_DiaperThoughts.Register(HarmonyInst);
            Patches.Patch_RegressedPlayTime.Register(HarmonyInst);
            Patches.Patch_ApparelThoughts.Register(HarmonyInst);

            Log.Message($"[{content.Name}] loaded.");
        }
    }
}
