using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;

namespace RE_EndfieldArmory
{
    [StaticConstructorOnStartup]
    public static class LupineScarletModInitializer
    {
        static LupineScarletModInitializer()
        {
            var harmony = new Harmony("com.re.endfieldarmory.lupinescarlet");
            // Тепер патчаться ТІЛЬКИ безпечні методи нанесення ран, жодних геттерів вербів!
            harmony.PatchAll();
            Log.Message("[RE_Endfield Armory] Модуль Lupine Scarlet (Scarlet Instinct) успішно ініціалізовано!");
        }
    }

    // Кастомний клас для бафу піку. Очищує стаки інстинкту після завершення таймера.
    public class Hediff_LupineApex : HediffWithComps
    {
        public override void PostRemoved()
        {
            base.PostRemoved();

            // Коли 20 секунд (Lupine Apex) завершуються, повністю видаляємо стаки Scarlet Instinct
            Hediff scarletInstinct = this.pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("RE_ScarletInstinct"));
            if (scarletInstinct != null)
            {
                this.pawn.health.RemoveHediff(scarletInstinct);
                Log.Message($"[Lupine Scarlet] Apex phase ended. Scarlet Instinct stacks for {this.pawn.LabelShort} reset to zero.");
            }
        }
    }

    [HarmonyPatch(typeof(DamageWorker_AddInjury), "Apply")]
    public static class Patch_LupineScarlet_Logic
    {
        private static Dictionary<int, int> lastScarletStackTick = new Dictionary<int, int>();

        // 1. Динамічний розрахунок та модифікація шкоди (Пасивний бонус ATK +16% та бонуси стаків)
        [HarmonyPrefix]
        public static void Prefix(ref DamageInfo dinfo, Thing thing)
        {
            if (dinfo.Instigator is Pawn attacker)
            {
                var primaryWeapon = attacker.equipment?.Primary;
                if (primaryWeapon?.def?.defName == "RE_LupineScarlet")
                {
                    float damageMultiplier = 1.0f;

                    // Пасивна специфікація меча: ATK +16%
                    damageMultiplier += 0.16f;

                    var hediffSet = attacker.health.hediffSet;

                    // Додаємо бонус від поточних стаків Scarlet Instinct (+1% за кожен стак, максимум +16%)
                    Hediff instinctBuff = hediffSet.GetFirstHediffOfDef(HediffDef.Named("RE_ScarletInstinct"));
                    if (instinctBuff != null)
                    {
                        damageMultiplier += (instinctBuff.Severity * 0.01f);
                    }

                    // Додаємо бонус від режиму Апексу (+24%), якщо активовано пік
                    if (hediffSet.HasHediff(HediffDef.Named("RE_LupineApex")))
                    {
                        damageMultiplier += 0.24f;
                    }

                    dinfo.SetAmount(dinfo.Amount * damageMultiplier);
                }
            }
        }

        // 2. Логіка фіксації підпалу та нарахування стаків інстинкту
        [HarmonyPostfix]
        public static void Postfix(DamageInfo dinfo, Thing thing)
        {
            if (dinfo.Instigator is Pawn attacker && thing is Pawn victim && dinfo.Amount > 0)
            {
                var primaryWeapon = attacker.equipment?.Primary;
                if (primaryWeapon?.def?.defName == "RE_LupineScarlet")
                {
                    if (victim.IsBurning())
                    {
                        var hediffSet = attacker.health.hediffSet;

                        if (hediffSet.HasHediff(HediffDef.Named("RE_LupineApex")))
                        {
                            return;
                        }

                        int currentTick = Find.TickManager.TicksGame;
                        lastScarletStackTick.TryGetValue(attacker.thingIDNumber, out int lastHitTick);

                        if (lastHitTick != currentTick)
                        {
                            HediffDef instinctDef = HediffDef.Named("RE_ScarletInstinct");
                            Hediff instinctBuff = hediffSet.GetFirstHediffOfDef(instinctDef);

                            if (instinctBuff == null)
                            {
                                instinctBuff = attacker.health.AddHediff(instinctDef);
                                instinctBuff.Severity = 1f;
                            }
                            else
                            {
                                instinctBuff.Severity = Mathf.Min(instinctBuff.Severity + 1f, 16f);
                            }

                            lastScarletStackTick[attacker.thingIDNumber] = currentTick;

                            if (instinctBuff.Severity >= 16f)
                            {
                                HediffDef apexDef = HediffDef.Named("RE_LupineApex");
                                Hediff apexBuff = attacker.health.AddHediff(apexDef);

                                var comp = apexBuff.TryGetComp<HediffComp_Disappears>();
                                if (comp != null)
                                {
                                    comp.ticksToDisappear = 1200; // 20 секунд
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}