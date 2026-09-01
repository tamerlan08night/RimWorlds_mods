using System.Collections.Generic;
using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;

namespace AKE.endfield
{
    [StaticConstructorOnStartup]
    public static class LupineScarletModInitializer
    {
        static LupineScarletModInitializer()
        {
            var harmony = new Harmony("com.re.endfieldarmory.lupinescarlet");
            harmony.PatchAll();
            Log.Message("[RE_Endfield Armory] Модуль Lupine Scarlet (Scarlet Instinct) ініціалізовано.");
        }
    }

    public class Hediff_LupineApex : HediffWithComps
    {
        private static readonly HediffDef InstinctDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("RE_ScarletInstinct");

        public override void PostRemoved()
        {
            base.PostRemoved();

            if (InstinctDef == null || pawn == null || pawn.Dead) return;

            Hediff scarletInstinct = pawn.health.hediffSet.GetFirstHediffOfDef(InstinctDef);
            if (scarletInstinct != null)
            {
                pawn.health.RemoveHediff(scarletInstinct);
            }
        }
    }

    [HarmonyPatch(typeof(DamageWorker_AddInjury), "Apply")]
    public static class Patch_LupineScarlet_Logic
    {
        private static readonly HediffDef InstinctDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("RE_ScarletInstinct");
        private static readonly HediffDef ApexDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("RE_LupineApex");

        private static readonly Dictionary<int, int> lastScarletStackTick = new Dictionary<int, int>();

        private const float PassiveDamageBonus = 0.16f;
        private const float PerStackBonus = 0.01f;
        private const float ApexDamageBonus = 0.24f;
        private const float MaxStacks = 16f;
        private const int ApexDurationTicks = 1200;

        [HarmonyPrefix]
        public static void Prefix(ref DamageInfo dinfo, Thing thing)
        {
            if (!(dinfo.Instigator is Pawn attacker)) return;

            var primaryWeapon = attacker.equipment?.Primary;
            if (primaryWeapon?.def?.defName != "RE_LupineScarlet") return;

            float damageMultiplier = 1.0f + PassiveDamageBonus;

            var hediffSet = attacker.health.hediffSet;

            if (InstinctDef != null)
            {
                Hediff instinctBuff = hediffSet.GetFirstHediffOfDef(InstinctDef);
                if (instinctBuff != null)
                {
                    damageMultiplier += (instinctBuff.Severity * PerStackBonus);
                }
            }

            if (ApexDef != null && hediffSet.HasHediff(ApexDef))
            {
                damageMultiplier += ApexDamageBonus;
            }

            dinfo.SetAmount(dinfo.Amount * damageMultiplier);
        }

        [HarmonyPostfix]
        public static void Postfix(DamageInfo dinfo, Thing thing)
        {
            if (!(dinfo.Instigator is Pawn attacker) || !(thing is Pawn victim) || dinfo.Amount <= 0)
                return;

            var primaryWeapon = attacker.equipment?.Primary;
            if (primaryWeapon?.def?.defName != "RE_LupineScarlet") return;
            if (InstinctDef == null || ApexDef == null) return;

            if (!victim.IsBurning()) return;

            var hediffSet = attacker.health.hediffSet;

            if (hediffSet.HasHediff(ApexDef)) return;

            int currentTick = Find.TickManager.TicksGame;
            lastScarletStackTick.TryGetValue(attacker.thingIDNumber, out int lastHitTick);

            if (lastHitTick == currentTick) return;

            Hediff instinctBuff = hediffSet.GetFirstHediffOfDef(InstinctDef);

            if (instinctBuff == null)
            {
                instinctBuff = attacker.health.AddHediff(InstinctDef);
                instinctBuff.Severity = 1f;
            }
            else
            {
                instinctBuff.Severity = Mathf.Min(instinctBuff.Severity + 1f, MaxStacks);
            }

            lastScarletStackTick[attacker.thingIDNumber] = currentTick;

            if (instinctBuff.Severity >= MaxStacks)
            {
                Hediff apexBuff = attacker.health.AddHediff(ApexDef);

                var comp = apexBuff.TryGetComp<HediffComp_Disappears>();
                if (comp != null)
                {
                    comp.ticksToDisappear = ApexDurationTicks;
                }
            }
        }
    }
}
