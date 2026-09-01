using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AKE.endfield
{
    [StaticConstructorOnStartup]
    public static class GloriousMemoryModInitializer
    {
        static GloriousMemoryModInitializer()
        {
            var harmony = new Harmony("com.re.endfieldarmory.gloriousmemory");
            harmony.PatchAll();
            Log.Message("[RE_Endfield Armory] Меч Glorious Memory (Славна Пам'ять) ініціалізовано.");
        }
    }

    public class Hediff_GloriousMemory : HediffWithComps
    {
        public List<int> expireTicks = new List<int>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref expireTicks, "expireTicks", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && expireTicks == null)
            {
                expireTicks = new List<int>();
            }
        }

        public override void Tick()
        {
            base.Tick();
            bool changed = false;
            int currentTick = Find.TickManager.TicksGame;

            for (int i = expireTicks.Count - 1; i >= 0; i--)
            {
                if (currentTick >= expireTicks[i])
                {
                    expireTicks.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                this.Severity = expireTicks.Count;
            }
        }

        public void AddStack(int durationTicks)
        {
            if (expireTicks.Count >= 3)
            {
                expireTicks.RemoveAt(0);
            }

            expireTicks.Add(Find.TickManager.TicksGame + durationTicks);
            this.Severity = expireTicks.Count;
        }
    }

    [HarmonyPatch(typeof(DamageWorker_AddInjury), "Apply")]
    public static class Patch_GloriousMemory_Logic
    {
        private static readonly HediffDef GmDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("RE_GloriousMemory");
        private static readonly HediffDef PhysicDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("Arts_Physical");

        private static readonly Dictionary<int, int> lastTriggerTick = new Dictionary<int, int>();

        private const int StackCooldownTicks = 30;
        private const int StackDurationTicks = 1800;
        private const float BaseDamageMultiplier = 1.07f;
        private const float PerStackMultiplier = 0.12f;

        [HarmonyPrefix]
        public static void Prefix(ref DamageInfo dinfo, Thing thing)
        {
            if (!(dinfo.Instigator is Pawn attacker)) return;

            var primaryWeapon = attacker.equipment?.Primary;
            if (primaryWeapon?.def?.defName != "RE_GloriousMemory") return;

            float damageMultiplier = BaseDamageMultiplier;

            if (GmDef != null)
            {
                Hediff gmBuff = attacker.health.hediffSet.GetFirstHediffOfDef(GmDef);
                if (gmBuff != null && gmBuff.Severity > 0)
                {
                    damageMultiplier += (gmBuff.Severity * PerStackMultiplier);
                }
            }

            dinfo.SetAmount(dinfo.Amount * damageMultiplier);
        }

        [HarmonyPostfix]
        public static void Postfix(DamageInfo dinfo, Thing thing)
        {
            if (!(dinfo.Instigator is Pawn attacker) || !(thing is Pawn victim) || dinfo.Amount <= 0)
                return;

            var primaryWeapon = attacker.equipment?.Primary;
            if (primaryWeapon?.def?.defName != "RE_GloriousMemory") return;

            if (GmDef == null || PhysicDef == null) return;

            if (!victim.health.hediffSet.HasHediff(PhysicDef)) return;

            int currentTick = Find.TickManager.TicksGame;
            lastTriggerTick.TryGetValue(attacker.thingIDNumber, out int lastTick);

            if (currentTick - lastTick < StackCooldownTicks) return;

            Hediff_GloriousMemory gmBuff = attacker.health.hediffSet.GetFirstHediffOfDef(GmDef) as Hediff_GloriousMemory;

            if (gmBuff == null)
            {
                gmBuff = (Hediff_GloriousMemory)attacker.health.AddHediff(GmDef);
            }

            gmBuff.AddStack(StackDurationTicks);
            lastTriggerTick[attacker.thingIDNumber] = currentTick;
        }
    }
}
