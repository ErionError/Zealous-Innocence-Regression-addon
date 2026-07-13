using System.Linq;
using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Helpers for detecting ZI apparel that affect regression.
    /// All methods are safe to call with null pawn.
    /// </summary>
    public static class ZIR_ApparelUtility
    {
        // ── Tag/def name constants (mirrors ZI's XML) ─────────────────────────
        private const string TagOnesie    = "Onesies";
        private const string TagDiaper    = "Diaper";
        private const string TagUnderwear = "Underwear";
        private const string DefPacifier  = "ZI_Pacifier";

        // ── Apparel detection ─────────────────────────────────────────────────

        /// <summary>True if the pawn is wearing any onesie (tag=Onesies).</summary>
        public static bool HasOnesie(Pawn pawn)
        {
            if (pawn?.apparel == null) return false;
            foreach (Apparel a in pawn.apparel.WornApparel)
                if (HasTag(a, TagOnesie)) return true;
            return false;
        }

        /// <summary>True if the pawn is wearing a pacifier.</summary>
        public static bool HasPacifier(Pawn pawn)
        {
            if (pawn?.apparel == null) return false;
            foreach (Apparel a in pawn.apparel.WornApparel)
                if (a.def.defName == DefPacifier) return true;
            return false;
        }

        /// <summary>
        /// True if the pawn is wearing a protective garment (diaper or underwear)
        /// that is NOT hidden by any bottom-covering clothing (Legs body part group).
        /// Onesies cover Legs and therefore count as concealing.
        /// </summary>
        public static bool HasExposedProtection(Pawn pawn)
        {
            if (pawn?.apparel == null) return false;

            bool hasProtection = false;
            bool hasCovering   = false;

            foreach (Apparel a in pawn.apparel.WornApparel)
            {
                if (HasTag(a, TagDiaper) || HasTag(a, TagUnderwear))
                {
                    hasProtection = true;
                    continue;
                }

                // Any apparel covering Legs or Waist hides the underwear layer
                if (CoversBodyPart(a, "Legs") || CoversBodyPart(a, "Waist"))
                    hasCovering = true;
            }

            return hasProtection && !hasCovering;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static bool HasTag(Apparel a, string tag)
        {
            var tags = a.def?.apparel?.tags;
            if (tags == null) return false;
            foreach (string t in tags)
                if (t == tag) return true;
            return false;
        }

        private static bool CoversBodyPart(Apparel a, string bpgDefName)
        {
            var groups = a.def?.apparel?.bodyPartGroups;
            if (groups == null) return false;
            foreach (var g in groups)
                if (g.defName == bpgDefName) return true;
            return false;
        }
    }
}
