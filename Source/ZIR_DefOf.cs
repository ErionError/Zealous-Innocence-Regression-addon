using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    [DefOf]
    public static class ZIR_DefOf
    {
        public static LetterDef ZIR_RegressionRecovery;

        [MayRequire("dubwise.dubsbadhygiene")]
        public static JobDef  ZIR_PottyTraining;

        [MayRequire("dubwise.dubsbadhygiene")]
        public static ThingDef ZIR_PottyChair;

        static ZIR_DefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(ZIR_DefOf)); }
    }
}
