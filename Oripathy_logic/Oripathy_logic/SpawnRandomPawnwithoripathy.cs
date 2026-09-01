using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AKEndfield
{
    /// <summary>
    /// Додає шанс спавну Оріпатії для будь-яких новозгенерованих людей у світі
    /// (рейдери, каравани, біженці, торговці тощо).
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new Type[] { typeof(PawnGenerationRequest) })]
    internal static class Patch_PawnGenerator_OripathyRandomSpawn
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __result)
        {
            // 1. Перевіряємо, чи пешка успішно створилася і чи це людина (а не тварина чи механоїд)
            if (__result == null || __result.RaceProps == null || !__result.RaceProps.Humanlike) 
                return;

            // 2. Шанс спавну з Оріпатією — зараз стоїть 5% (0.05f)
            if (Rand.Value < 0.05f)
            {
                HediffDef oripathyDef = HediffDef.Named("OE_Oripathy");

                // Перевіряємо, чи існує дефінішн і чи пешка ще не заражена
                if (oripathyDef != null && !__result.health.hediffSet.HasHediff(oripathyDef))
                {
                    Hediff oripathy = HediffMaker.MakeHediff(oripathyDef, __result);

                    // 3. Випадковий рівень важкості хвороби від 0.01 (1% - тільки початок) 
                    // до 0.60 (60% - важка стадія перед кристалізацією)
                    oripathy.Severity = Rand.Range(0.01f, 0.60f);

                    __result.health.AddHediff(oripathy);
                }
            }
        }
    }
}