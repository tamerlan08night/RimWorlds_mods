using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArknightsArts
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), "PreApplyDamage")]
    public static class Patch_PreApplyDamage
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Normal)]
        public static void MainPrefix(Pawn ___pawn, ref DamageInfo dinfo)
        {
            Pawn victim = ___pawn;
            if (!(dinfo.Instigator is Pawn attacker) || attacker.equipment?.Primary == null) return;

            var comp = attacker.equipment.Primary.TryGetComp<CompArtsWeapon>();
            if (comp == null) return;
            var props = comp.Props;

            // 1. АПЛІКАЦІЯ ЕЛЕМЕНТА
            if (props.appliesElement != "None" && Rand.Value <= props.applyChance)
            {
                HediffDef elementDef = DefDatabase<HediffDef>.GetNamedSilentFail("Arts_" + props.appliesElement);
                if (elementDef != null)
                {
                    var existing = victim.health.hediffSet.GetFirstHediffOfDef(elementDef) as Hediff_ArtsElement;
                    if (existing != null)
                    {
                        float oldSeverity = existing.Severity;
                        existing.Severity = Math.Min(existing.Severity + 0.25f, 1.0f);
                        existing.ResetTimer();

                        // Поріг 0.76 - це перехід на 4 стадію
                        if (props.appliesElement == "Cryo" && oldSeverity < 0.76f && existing.Severity >= 0.76f)
                        {
                            victim.stances?.stunner?.StunFor(300, attacker);
                            MoteMaker.ThrowText(victim.DrawPos, victim.Map, "FROZEN!", 3.5f);
                        }

                        if (props.appliesElement == "Electric" && oldSeverity < 0.76f && existing.Severity >= 0.76f)
                        {
                            ElectricLogic.TriggerElectricDischarge(victim, attacker);
                        }
                    }
                    else
                    {
                        HealthUtility.AdjustSeverity(victim, elementDef, 0.25f);
                    }
                }
            }

            // 2. ПЕРЕВІРКА 4-Ї СТАДІЇ
            var hediffs = victim.health.hediffSet.hediffs;
            int totalStages = 0;
            Hediff_ArtsElement cryoToShatter = null;
            Hediff_ArtsElement pyroToDetonate = null;
            Hediff_ArtsElement natureToCorrect = null;
            Hediff_ArtsElement electricToArc = null; // Додано для електрики

            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_ArtsElement arts)
                {
                    totalStages += (arts.CurStageIndex + 1);

                    if (arts.def.defName == "Arts_Cryo" && arts.CurStageIndex == 3)
                        cryoToShatter = arts;
                    else if (arts.def.defName == "Arts_Pyro" && arts.CurStageIndex == 3)
                        pyroToDetonate = arts;
                    else if (arts.def.defName == "Arts_Nature" && arts.CurStageIndex == 3)
                        natureToCorrect = arts;
                    else if (arts.def.defName == "Arts_Electric" && arts.CurStageIndex == 3)
                        electricToArc = arts;
                }
            }

            // 3. ВИКЛИК ЕФЕКТІВ
            if (cryoToShatter != null)
            {
                dinfo.SetAmount(dinfo.Amount * 2.5f);
                victim.health.RemoveHediff(cryoToShatter);
                MoteMaker.ThrowText(victim.DrawPos, victim.Map, "SHATTER!", 4f);
            }
            else if (pyroToDetonate != null)
            {
                victim.health.RemoveHediff(pyroToDetonate);
                PyroLogic.TriggerPyroExplosion(victim, attacker);
            }
            else if (natureToCorrect != null)
            {
                victim.health.RemoveHediff(natureToCorrect);
                NatureLogic.TriggerNatureCorrosion(victim, attacker);
            }
            else if (electricToArc != null) // Виклик розряду при ударі по зарядженій цілі
            {
                ElectricLogic.TriggerElectricDischarge(victim, attacker);
            }
            else if (totalStages > 0 && props.bonusPerStage > 0)
            {
                dinfo.SetAmount(dinfo.Amount * (1f + (totalStages * props.bonusPerStage)));
            }

            // 4. ASPIRANT ЗБРОЯ (додаткова шкода по вогню)
            if (props.internalDamageOnFire && victim.HasAttachment(ThingDefOf.Fire))
            {
                // Тут має бути посилання на твій NatureLogic або інший клас
                // NatureLogic.ApplyInternalDamage(victim, attacker, dinfo.Amount * 0.3f);
            }
        }
    }
}