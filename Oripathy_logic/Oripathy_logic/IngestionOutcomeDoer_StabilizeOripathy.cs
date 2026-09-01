using RimWorld;
using UnityEngine;
using Verse;

namespace AKE.endfield
{
    public class IngestionOutcomeDoer_StabilizeOripathy : IngestionOutcomeDoer
    {
        private static readonly HediffDef OripathyDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("OE_Oripathy");
        private static readonly HediffDef StabilizedDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("OE_OripathyStabilized");

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (pawn == null || ingested == null) return;

            var ext = ingested.def.GetModExtension<OripathyStabilizerExtension>();
            if (ext == null) return;

            if (OripathyDef == null) return;

            Hediff oripathy = pawn.health.hediffSet.GetFirstHediffOfDef(OripathyDef);
            if (oripathy == null) return;

            if (ext.instantSeverityReduction > 0f)
            {
                oripathy.Severity = Mathf.Max(0f, oripathy.Severity - ext.instantSeverityReduction);
            }

            if (StabilizedDef != null)
            {
                Hediff existingBuff = pawn.health.hediffSet.GetFirstHediffOfDef(StabilizedDef);
                if (existingBuff != null)
                {
                    pawn.health.RemoveHediff(existingBuff);
                }

                Hediff newBuff = HediffMaker.MakeHediff(StabilizedDef, pawn);

                var disappearsComp = newBuff.TryGetComp<HediffComp_Disappears>();
                if (disappearsComp != null)
                {
                    disappearsComp.ticksToDisappear = ext.stabilizeDurationTicks;
                }

                pawn.health.AddHediff(newBuff);

                if (pawn.IsColonist)
                {
                    string label = ext.stabilizerLabel.NullOrEmpty() ? "Stabilizer" : ext.stabilizerLabel;
                    Messages.Message($"Oripathy stabilized ({label}).", pawn, MessageTypeDefOf.PositiveEvent);
                }
            }
        }
    }
}
