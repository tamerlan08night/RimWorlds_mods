using HarmonyLib;
using RimWorld;
using Verse;

namespace ArknightsArts
{
    // Патч для збільшення шкоди від вогню
    [HarmonyPatch(typeof(Pawn_HealthTracker), "PreApplyDamage")]
    public static class Patch_FireSensitivity
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Normal - 1)]
        public static void FireSensitivityPrefix(Pawn ___pawn, ref DamageInfo dinfo)
        {
            if (dinfo.Def != DamageDefOf.Flame && dinfo.Def != DamageDefOf.Burn) return;

            var pyroHediff = ___pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamedSilentFail("Arts_Pyro")) as Hediff_ArtsElement;
            if (pyroHediff == null) return;

            float multiplier = 1f + (pyroHediff.CurStageIndex + 1) * 0.20f;
            dinfo.SetAmount(dinfo.Amount * multiplier);
        }
    }

    // Тригер 4-ї стадії
    public static class PyroLogic
    {
        public static void TriggerPyroExplosion(Pawn victim, Pawn attacker)
        {
            if (victim.Map == null) return;

            DamageDef flameDef = DefDatabase<DamageDef>.GetNamedSilentFail("ArtsDmg_Pyro") ?? DamageDefOf.Flame;

            GenExplosion.DoExplosion(
                center: victim.Position,
                map: victim.Map,
                radius: 1.9f,
                damType: flameDef,
                instigator: attacker,
                damAmount: 25,
                armorPenetration: 0.3f,
                explosionSound: null,
                weapon: attacker?.equipment?.Primary?.def,
                projectile: null,
                intendedTarget: victim,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 1,
                applyDamageToExplosionCellsNeighbors: false,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 1,
                chanceToStartFire: 0.5f,
                damageFalloff: true
            );

            MoteMaker.ThrowText(victim.DrawPos, victim.Map, "IGNITE!", 4f);
        }
    }
}