using UnityEngine;
using Verse;

namespace AKEndfield
{
    public class Hediff_Oripathy : HediffWithComps
    {
        // Перевірка наявності баффу OE_OripathyStabilized безпосередньо у HealthTracker
        public bool IsFrozen
        {
            get
            {
                HediffDef stabilizedDef = HediffDef.Named("OE_OripathyStabilized");
                return stabilizedDef != null && pawn.health.hediffSet.HasHediff(stabilizedDef);
            }
        }

        // Автоматично додає відсоток серйозності до назви у вкладці Health
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