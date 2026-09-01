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
    public static class GrandVisionModInitializer
    {
        static GrandVisionModInitializer()
        {
            var harmony = new Harmony("com.re.endfieldarmory.grandvision");
            harmony.PatchAll();
            Log.Message("[RE_Endfield Armory] Меч Grand Vision (Велич Ендміністратора) успішно синхронізовано з матрицею!");
        }
    }

    public class Hediff_EndministratorMajesty : HediffWithComps
    {
        // Кастомний клас бафу Величі
    }

    [HarmonyPatch(typeof(DamageWorker_AddInjury), "Apply")]
    public static class Patch_GrandVision_Logic
    {
        [HarmonyPrefix]
        public static void Prefix(ref DamageInfo dinfo, Thing thing)
        {
            if (dinfo.Instigator is Pawn attacker)
            {
                var primaryWeapon = attacker.equipment?.Primary;
                if (primaryWeapon?.def?.defName == "RE_GrandVision")
                {
                    // HARD режим: якщо Intellectual <= 10, ніяких системних бонусів немає
                    if (attacker.skills == null || attacker.skills.GetSkill(SkillDefOf.Intellectual).Level <= 10)
                    {
                        return;
                    }

                    float damageMultiplier = 1.0f;

                    // Якщо активна Велич — додаємо чисті +36% фізичного урону
                    if (attacker.health.hediffSet.HasHediff(HediffDef.Named("RE_EndministratorMajesty")))
                    {
                        damageMultiplier += 0.36f;
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
                if (primaryWeapon?.def?.defName == "RE_GrandVision")
                {
                    // Перевірка розуму для активації резонансу Орігініуму
                    if (attacker.skills == null || attacker.skills.GetSkill(SkillDefOf.Intellectual).Level <= 10)
                    {
                        return;
                    }

                    var victimHediffs = victim.health.hediffSet;

                    // Звіряємося за залізно підтвердженими дефнеймами з твого Arts-рушія
                    bool hasCryoMark = victimHediffs.HasHediff(HediffDef.Named("Arts_Cryo"));
                    bool hasPhysicMark = victimHediffs.HasHediff(HediffDef.Named("Arts_Physical"));

                    if (hasCryoMark || hasPhysicMark)
                    {
                        HediffDef majestyDef = HediffDef.Named("RE_EndministratorMajesty");
                        Hediff majestyBuff = attacker.health.hediffSet.GetFirstHediffOfDef(majestyDef);

                        if (majestyBuff == null)
                        {
                            majestyBuff = attacker.health.AddHediff(majestyDef);
                        }

                        var comp = majestyBuff.TryGetComp<HediffComp_Disappears>();
                        if (comp != null)
                        {
                            comp.ticksToDisappear = 1200; // 20 секунд дії
                        }
                    }
                }
            }
        }
    }
}