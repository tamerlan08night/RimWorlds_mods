using HarmonyLib;
using RimWorld;
using Verse;

namespace AKE.endfield
{
    [StaticConstructorOnStartup]
    public static class GrandVisionModInitializer
    {
        static GrandVisionModInitializer()
        {
            var harmony = new Harmony("com.re.endfieldarmory.grandvision");
            harmony.PatchAll();
            Log.Message("[RE_Endfield Armory] Меч Grand Vision (Велич Ендміністратора) ініціалізовано.");
        }
    }

    public class Hediff_EndministratorMajesty : HediffWithComps
    {
    }

    [HarmonyPatch(typeof(DamageWorker_AddInjury), "Apply")]
    public static class Patch_GrandVision_Logic
    {
        private static readonly HediffDef MajestyDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("RE_EndministratorMajesty");
        private static readonly HediffDef CryoDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("Arts_Cryo");
        private static readonly HediffDef PhysicalDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("Arts_Physical");

        private const int MajestyDurationTicks = 1200;

        [HarmonyPrefix]
        public static void Prefix(ref DamageInfo dinfo, Thing thing)
        {
            if (!(dinfo.Instigator is Pawn attacker)) return;

            var primaryWeapon = attacker.equipment?.Primary;
            if (primaryWeapon?.def?.defName != "RE_GrandVision") return;

            var skillRecord = attacker.skills?.GetSkill(SkillDefOf.Intellectual);
            if (skillRecord == null || skillRecord.Level <= 10) return;

            float damageMultiplier = 1.0f;

            if (MajestyDef != null && attacker.health.hediffSet.HasHediff(MajestyDef))
            {
                damageMultiplier += 0.36f;
            }

            dinfo.SetAmount(dinfo.Amount * damageMultiplier);
        }

        [HarmonyPostfix]
        public static void Postfix(DamageInfo dinfo, Thing thing)
        {
            if (!(dinfo.Instigator is Pawn attacker) || !(thing is Pawn victim) || dinfo.Amount <= 0)
                return;

            var primaryWeapon = attacker.equipment?.Primary;
            if (primaryWeapon?.def?.defName != "RE_GrandVision") return;

            var skillRecord = attacker.skills?.GetSkill(SkillDefOf.Intellectual);
            if (skillRecord == null || skillRecord.Level <= 10) return;

            if (MajestyDef == null || CryoDef == null || PhysicalDef == null) return;

            var victimHediffs = victim.health.hediffSet;
            bool hasCryoMark = victimHediffs.HasHediff(CryoDef);
            bool hasPhysicMark = victimHediffs.HasHediff(PhysicalDef);

            if (hasCryoMark || hasPhysicMark)
            {
                Hediff majestyBuff = attacker.health.hediffSet.GetFirstHediffOfDef(MajestyDef);

                if (majestyBuff == null)
                {
                    majestyBuff = attacker.health.AddHediff(MajestyDef);
                }

                var comp = majestyBuff.TryGetComp<HediffComp_Disappears>();
                if (comp != null)
                {
                    comp.ticksToDisappear = MajestyDurationTicks;
                }
            }
        }
    }
}
