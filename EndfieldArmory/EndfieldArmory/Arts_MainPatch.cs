using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AKE.endfield
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), "PreApplyDamage")]
    public static class Patch_PreApplyDamage
    {
        private static readonly HediffDef CryoDef = DefDatabase<HediffDef>.GetNamedSilentFail("Arts_Cryo");
        private static readonly HediffDef PyroDef = DefDatabase<HediffDef>.GetNamedSilentFail("Arts_Pyro");
        private static readonly HediffDef NatureDef = DefDatabase<HediffDef>.GetNamedSilentFail("Arts_Nature");
        private static readonly HediffDef ElectricDef = DefDatabase<HediffDef>.GetNamedSilentFail("Arts_Electric");

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Normal)]
        public static void MainPrefix(Pawn ___pawn, ref DamageInfo dinfo)
        {
            Pawn victim = ___pawn;
            if (!(dinfo.Instigator is Pawn attacker) || attacker.equipment?.Primary == null) return;

            var comp = attacker.equipment.Primary.TryGetComp<CompArtsWeapon>();
            if (comp == null) return;
            var props = comp.Props;

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

                        if (props.appliesElement == "Cryo" && oldSeverity < 0.76f && existing.Severity >= 0.76f)
                        {
                            CryoLogic.TriggerFreezeStun(victim, attacker);
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

            var hediffs = victim.health.hediffSet.hediffs;
            int totalStages = 0;
            Hediff_ArtsElement cryoToShatter = null;
            Hediff_ArtsElement pyroToDetonate = null;
            Hediff_ArtsElement natureToCorrect = null;
            Hediff_ArtsElement electricToArc = null;

            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_ArtsElement arts)
                {
                    totalStages += (arts.CurStageIndex + 1);

                    if (arts.def == CryoDef && arts.CurStageIndex == 3)
                        cryoToShatter = arts;
                    else if (arts.def == PyroDef && arts.CurStageIndex == 3)
                        pyroToDetonate = arts;
                    else if (arts.def == NatureDef && arts.CurStageIndex == 3)
                        natureToCorrect = arts;
                    else if (arts.def == ElectricDef && arts.CurStageIndex == 3)
                        electricToArc = arts;
                }
            }

            if (cryoToShatter != null)
            {
                CryoLogic.TriggerShatter(victim, ref dinfo, cryoToShatter);
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
            else if (electricToArc != null)
            {
                ElectricLogic.TriggerElectricDischarge(victim, attacker);
            }
            else if (totalStages > 0 && props.bonusPerStage > 0)
            {
                dinfo.SetAmount(dinfo.Amount * (1f + (totalStages * props.bonusPerStage)));
            }

            if (props.internalDamageOnFire && victim.HasAttachment(ThingDefOf.Fire))
            {
                NatureLogic.ApplyInternalDamage(victim, attacker, dinfo.Amount * 0.3f);
            }
        }
    }
}
