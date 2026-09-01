using RimWorld;
using UnityEngine;
using Verse;

namespace AKEndfield
{
    public class IngestionOutcomeDoer_StabilizeOripathy : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (pawn == null || ingested == null) return;

            // 1. Отримуємо налаштування стабілізатора з XML через DefModExtension
            var ext = ingested.def.GetModExtension<OripathyStabilizerExtension>();
            if (ext == null) return;

            HediffDef oripathyDef = HediffDef.Named("OE_Oripathy");
            if (oripathyDef == null) return;

            // 2. Перевіряємо, чи є у пішака Оріпатія
            Hediff oripathy = pawn.health.hediffSet.GetFirstHediffOfDef(oripathyDef);
            if (oripathy == null) return; // Якщо не хворий, ліки просто з'їдаються без ефекту

            // 3. Миттєве зменшення серйозності (якщо вказано в XML)
            if (ext.instantSeverityReduction > 0f)
            {
                oripathy.Severity = Mathf.Max(0f, oripathy.Severity - ext.instantSeverityReduction);
            }

            // 4. Додаємо окремий бафф-стабілізатор на пішака
            HediffDef stabilizedDef = HediffDef.Named("OE_OripathyStabilized");
            if (stabilizedDef != null)
            {
                // Видаляємо старий бафф, якщо він вже є, щоб оновити тривалість
                Hediff existingBuff = pawn.health.hediffSet.GetFirstHediffOfDef(stabilizedDef);
                if (existingBuff != null)
                {
                    pawn.health.RemoveHediff(existingBuff);
                }

                // Створюємо новий бафф
                Hediff newBuff = HediffMaker.MakeHediff(stabilizedDef, pawn);
                
                // Налаштовуємо тривалість через компонент зникнення
                var disappearsComp = newBuff.TryGetComp<HediffComp_Disappears>();
                if (disappearsComp != null)
                {
                    disappearsComp.ticksToDisappear = ext.stabilizeDurationTicks;
                }

                pawn.health.AddHediff(newBuff);

                if (pawn.IsColonist)
                {
                    string label = ext.stabilizerLabel.NullOrEmpty() ? "Stabilizer" : ext.stabilizerLabel;
                    Messages.Message($"Оріпатію стабілізовано ({label}).", pawn, MessageTypeDefOf.PositiveEvent);
                }
            }
        }
    }
}