using UnityEngine;
using Verse;

namespace AKE.endfield
{
    public class Hediff_Oripathy : HediffWithComps
    {
        private static readonly HediffDef StabilizedDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("OE_OripathyStabilized");

        public bool IsFrozen
        {
            get
            {
                return StabilizedDef != null && pawn.health.hediffSet.HasHediff(StabilizedDef);
            }
        }

        public override string Label
        {
            get
            {
                string baseLabel = base.Label;
                int percent = Mathf.RoundToInt(Severity * 100f);
                return $"{baseLabel} ({percent}%)";
            }
        }

        public override bool ShouldRemove => false;
    }
}
