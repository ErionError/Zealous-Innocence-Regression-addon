using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// References to Zealous Innocence trait defs.
    /// ZI is a hard dependency so these will always resolve, but
    /// MayRequire guards prevent startup warnings if somehow absent.
    /// </summary>
    [DefOf]
    public static class ZIR_TraitDefOf
    {
        [MayRequire("Proximo.ZealousInnocence")]
        public static TraitDef Big_Boy;       // Self-Conscious — hates diapers / dependency

        [MayRequire("Proximo.ZealousInnocence")]
        public static TraitDef Potty_Rebel;   // enjoys diapers / accidents

        [MayRequire("Proximo.ZealousInnocence")]
        public static TraitDef Diaper_Lover;  // likes wearing diapers

#pragma warning disable CS8618
        static ZIR_TraitDefOf() { }
#pragma warning restore CS8618
    }
}
