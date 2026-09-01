using RimWorld;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace AKE.endfield
{
    public static class ElectricLogic
    {
        private static readonly HediffDef ElectricHediffDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("Arts_Electric");

        private static readonly DamageDef ElectricDamageDef =
            DefDatabase<DamageDef>.GetNamedSilentFail("ArtsDmg_Electric");

        private static readonly FleckDef ElectricShockFleck =
            DefDatabase<FleckDef>.GetNamedSilentFail("Arts_ElectricShock");

        public static void TriggerElectricDischarge(Pawn victim, Pawn attacker)
        {
            if (victim == null || victim.Map == null || attacker == null) return;

            victim.stances?.stunner?.StunFor(300, attacker);
            MoteMaker.ThrowText(victim.DrawPos, victim.Map, "OVERLOAD!", 3.5f);

            SoundDefOf.Thunder_OffMap.PlayOneShot(new TargetInfo(victim.Position, victim.Map));

            FleckDef shockFleck = ElectricShockFleck ?? FleckDefOf.MicroSparksFast;
            FleckMaker.Static(victim.Position, victim.Map, shockFleck, 1.5f);

            float radius = 5.9f;
            var candidates = victim.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != victim
                       && p != attacker
                       && !p.Dead
                       && !p.Downed
                       && p.Position.InHorDistOf(victim.Position, radius)
                       && attacker.Faction != null
                       && p.HostileTo(attacker.Faction))
                .InRandomOrder()
                .Take(2)
                .ToList();

            DamageDef dmgDef = ElectricDamageDef ?? DamageDefOf.Stun;

            foreach (Pawn arcTarget in candidates)
            {
                arcTarget.TakeDamage(new DamageInfo(
                    dmgDef,
                    amount: 12f,
                    armorPenetration: 0.5f,
                    instigator: attacker,
                    weapon: attacker.equipment?.Primary?.def
                ));

                MoteMaker.ThrowText(arcTarget.DrawPos, arcTarget.Map, "ARC!", 2.5f);
                FleckMaker.Static(arcTarget.Position, arcTarget.Map, shockFleck, 1f);
                FleckMaker.Static(arcTarget.Position, arcTarget.Map, FleckDefOf.LightningGlow, 1.5f);
            }

            if (ElectricHediffDef != null)
            {
                var electricHediff = victim.health.hediffSet.GetFirstHediffOfDef(ElectricHediffDef);
                if (electricHediff != null)
                {
                    victim.health.RemoveHediff(electricHediff);
                }
            }
        }
    }
}
