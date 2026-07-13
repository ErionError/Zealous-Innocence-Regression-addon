using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    [DefOf]
    public static class ZIR_HediffDefOf
    {
        public static HediffDef ZIR_Regression = null!;

        // Per-stage hediffs (one active at a time, replacing ZI's deprecated capMod system)
        public static HediffDef ZIR_Stage_PreTeen    = null!;
        public static HediffDef ZIR_Stage_YoungChild = null!;
        public static HediffDef ZIR_Stage_Toddler    = null!;
        public static HediffDef ZIR_Stage_Infant     = null!;

        static ZIR_HediffDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(ZIR_HediffDefOf)); }
    }
}
