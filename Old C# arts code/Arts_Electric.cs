using RimWorld;
using Verse;
using Verse.Sound; // Додано для звуку
using System.Collections.Generic;
using System.Linq;

namespace ArknightsArts
{
    public static class ElectricLogic
    {
        public static void TriggerElectricDischarge(Pawn victim, Pawn attacker)
        {
            if (victim == null || victim.Map == null) return;

            // 1. Приголомшення основної цілі
            victim.stances?.stunner?.StunFor(300, attacker);
            MoteMaker.ThrowText(victim.DrawPos, victim.Map, "OVERLOAD!", 3.5f);

            // Звук розряду
            SoundDefOf.Thunder_OffMap.PlayOneShot(new TargetInfo(victim.Position, victim.Map));

            // Візуал на головній цілі
            FleckDef shockFleck = DefDatabase<FleckDef>.GetNamedSilentFail("Arts_ElectricShock") ?? FleckDefOf.MicroSparksFast;
            FleckMaker.Static(victim.Position, victim.Map, shockFleck, 1.5f);

            // 2. Пошук сусідніх цілей (Ланцюгова блискавка)
            float radius = 5.9f; // Трохи збільшив радіус для надійності
            var candidates = victim.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != victim
                       && p != attacker
                       && !p.Dead
                       && !p.Downed
                       && p.Position.InHorDistOf(victim.Position, radius)
                       && p.HostileTo(attacker.Faction)) // Б'є тільки ворогів
                .InRandomOrder()
                .Take(2)
                .ToList();

            foreach (Pawn arcTarget in candidates)
            {
                // ВИПРАВЛЕНО: тепер назва збігається з твоїм Damage_Electric.xml
                DamageDef electricDef = DefDatabase<DamageDef>.GetNamedSilentFail("ArtsDmg_Electric") ?? DamageDefOf.Stun;

                arcTarget.TakeDamage(new DamageInfo(
                    electricDef,
                    amount: 12f, // Шкода сусіднім цілям
                    armorPenetration: 0.5f,
                    instigator: attacker,
                    weapon: attacker?.equipment?.Primary?.def
                ));

                // Візуал для сусідів
                MoteMaker.ThrowText(arcTarget.DrawPos, arcTarget.Map, "ARC!", 2.5f);
                FleckMaker.Static(arcTarget.Position, arcTarget.Map, shockFleck, 1f);

                // Простий ефект блискавки з неба
                FleckMaker.Static(arcTarget.Position, arcTarget.Map, FleckDefOf.LightningGlow, 1.5f);
            }

            // 3. Видаляємо ефект електрики після розряду
            // Назва "Arts_Electric" збігається з твоїм Arts_Electric_Hediff.xml
            var electricHediff = victim.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamedSilentFail("Arts_Electric"));
            if (electricHediff != null)
            {
                victim.health.RemoveHediff(electricHediff);
            }
        }
    }
}