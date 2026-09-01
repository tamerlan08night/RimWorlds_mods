using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RE_EndfieldArmory
{
    // 1. Патч для інтерфейсу (відображення статів у меню)
    [HarmonyPatch(typeof(StatExtension), "GetStatValue")]
    internal static class RE_Patch_MeleeSkillScaling
    {
        [HarmonyPostfix]
        private static void Postfix(Thing thing, StatDef stat, ref float __result)
        {
            if (thing is Pawn pawn && stat != null)
            {
                Thing weapon = pawn.equipment?.Primary;
                var comp = weapon?.TryGetComp<CompWeaponSkillScaling>();
                if (comp == null) return;

                if (stat.defName == "MeleeWeapon_DamageMultiplier")
                    __result *= comp.GetDamageMultiplier(pawn);
                else if (stat.defName == "MeleeArmorPenetration")
                    __result *= comp.GetArmorPenetrationMultiplier(pawn);
                else if (stat.defName == "MeleeParryChance")
                    __result *= comp.GetParryChanceMultiplier(pawn);
            }
        }
    }

    // 2. Патч для реального нанесення шкоди (враховуючи ціль/жертву)
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "Apply")]
    internal static class RE_Patch_MeleeDamageTargetAware
    {
        [HarmonyPrefix]
        private static void Prefix(ref DamageInfo dinfo, Thing thing)
        {
            // Перевіряємо нападника та жертву
            if (dinfo.Instigator is Pawn wielder && thing is Pawn target)
            {
                Thing weapon = wielder.equipment?.Primary;
                var comp = weapon?.TryGetComp<CompWeaponSkillScaling>();

                if (comp != null)
                {
                    // Отримуємо множник, який враховує і навички власника, і ефекти на цілі
                    float multiplier = comp.GetDamageMultiplier(wielder, target);
                    if (multiplier != 1f)
                    {
                        dinfo.SetAmount(dinfo.Amount * multiplier);
                    }
                }
            }
        }
    }
}