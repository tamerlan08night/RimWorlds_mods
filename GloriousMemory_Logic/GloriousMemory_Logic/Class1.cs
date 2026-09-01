using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RE_EndfieldArmory
{
    [StaticConstructorOnStartup]
    public static class GloriousMemoryModInitializer
    {
        static GloriousMemoryModInitializer()
        {
            var harmony = new Harmony("com.re.endfieldarmory.gloriousmemory");
            harmony.PatchAll();
            Log.Message("[RE_Endfield Armory] Меч Glorious Memory (Славна Пам'ять) підключено до матриці!");
        }
    }

    // Розумний хедифф, який самостійно керує таймерами кожного стаку
    public class Hediff_GloriousMemory : HediffWithComps
    {
        public List<int> expireTicks = new List<int>();

        // Обов'язковий метод для RimWorld, щоб таймери не ламалися при збереженні/завантаженні гри
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

            // Перевіряємо таймери з кінця списку, щоб безпечно видаляти елементи
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
            // Якщо вже є 3 стаки, видаляємо найстаріший, щоб дати місце новому
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
        private static Dictionary<int, int> lastTriggerTick = new Dictionary<int, int>();

        [HarmonyPrefix]
        public static void Prefix(ref DamageInfo dinfo, Thing thing)
        {
            if (dinfo.Instigator is Pawn attacker)
            {
                var primaryWeapon = attacker.equipment?.Primary;
                if (primaryWeapon?.def?.defName == "RE_GloriousMemory")
                {
                    // Пасивна специфікація меча: завжди ATK +7%
                    float damageMultiplier = 1.07f;

                    // Безпечно шукаємо деф та перевіряємо стаки
                    HediffDef gmDef = DefDatabase<HediffDef>.GetNamed("RE_GloriousMemory", false);
                    if (gmDef != null)
                    {
                        Hediff gmBuff = attacker.health.hediffSet.GetFirstHediffOfDef(gmDef);
                        if (gmBuff != null && gmBuff.Severity > 0)
                        {
                            // Кожен стак дає +12% урону (Максимум 3 стаки = +36%)
                            damageMultiplier += (gmBuff.Severity * 0.12f);
                        }
                    }

                    dinfo.SetAmount(dinfo.Amount * damageMultiplier);
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix(DamageInfo dinfo, Thing thing)
        {
            if (dinfo.Instigator is Pawn attacker && thing is Pawn victim && dinfo.Amount > 0)
            {
                var primaryWeapon = attacker.equipment?.Primary;
                if (primaryWeapon?.def?.defName == "RE_GloriousMemory")
                {
                    // Перевіряємо вразливість цілі (наявність Arts_Physical)
                    HediffDef physicDef = DefDatabase<HediffDef>.GetNamed("Arts_Physical", false);
                    if (physicDef != null && victim.health.hediffSet.HasHediff(physicDef))
                    {
                        int currentTick = Find.TickManager.TicksGame;
                        lastTriggerTick.TryGetValue(attacker.thingIDNumber, out int lastTick);

                        // Кулдаун тригера: мінімум 0.5с (30 тіків) між отриманням стаків
                        if (currentTick - lastTick >= 30)
                        {
                            HediffDef gmDef = DefDatabase<HediffDef>.GetNamed("RE_GloriousMemory", false);
                            if (gmDef == null) return;

                            Hediff_GloriousMemory gmBuff = attacker.health.hediffSet.GetFirstHediffOfDef(gmDef) as Hediff_GloriousMemory;

                            if (gmBuff == null)
                            {
                                gmBuff = (Hediff_GloriousMemory)attacker.health.AddHediff(gmDef);
                            }

                            // Додаємо 1 стак, який житиме 30 секунд (1800 тіків)
                            gmBuff.AddStack(1800);
                            lastTriggerTick[attacker.thingIDNumber] = currentTick;
                        }
                    }
                }
            }
        }
    }
}