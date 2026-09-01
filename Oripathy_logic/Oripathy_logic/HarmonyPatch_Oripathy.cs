using HarmonyLib;
using RimWorld;
using Verse;

namespace AKE.endfield
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatch_Oripathy
    {
        private static readonly Harmony HarmonyInstance =
            new Harmony("akendfield.armory.oripathy");

        static HarmonyPatch_Oripathy()
        {
            HarmonyInstance.PatchAll(typeof(HarmonyPatch_Oripathy).Assembly);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff))]
    internal static class Patch_PawnHealthTracker_RemoveHediff_PreventOripathyCure
    {
        [HarmonyPrefix]
        public static bool Prefix(Hediff hediff)
        {
            if (hediff is Hediff_Oripathy)
            {
                if (Verse.Prefs.DevMode)
                {
                    return true;
                }
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class Patch_Pawn_Kill_OripathyDeathLetter
    {
        [HarmonyPrefix]
        public static void Prefix(Pawn __instance, Hediff exactCulprit)
        {
            if (__instance == null || __instance.Dead) return;
            if (!(exactCulprit is Hediff_Oripathy)) return;
            if (!__instance.IsColonist && !__instance.IsSlaveOfColony) return;

            Find.LetterStack?.ReceiveLetter(
                "OripathyDeathLetterLabel".Translate(__instance.Named("PAWN")),
                "OripathyDeathLetterText".Translate(__instance.Named("PAWN")),
                LetterDefOf.Death,
                __instance);
        }
    }

    [HarmonyPatch(typeof(Mineable), nameof(Mineable.Notify_TookMiningDamage))]
    internal static class Patch_Mineable_Notify_TookMiningDamage_Oripathy
    {
        private static readonly HediffDef OripathyDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("OE_Oripathy");

        [HarmonyPostfix]
        public static void Postfix(Mineable __instance, int amount, Pawn miner)
        {
            if (miner == null || miner.RaceProps == null || !miner.RaceProps.Humanlike) return;
            if (OripathyDef == null) return;

            if (__instance.def.defName == "OE_OriginiumSurface")
            {
                if (Rand.Value < 0.20f)
                {
                    Hediff existingOripathy = miner.health.hediffSet.GetFirstHediffOfDef(OripathyDef);

                    if (existingOripathy == null)
                    {
                        Hediff newOripathy = HediffMaker.MakeHediff(OripathyDef, miner);
                        newOripathy.Severity = 0.01f;
                        miner.health.AddHediff(newOripathy);

                        Messages.Message("Oripathy Infection Mining".Translate(miner.LabelShort, __instance.Label),
                            miner, MessageTypeDefOf.NegativeEvent);
                    }
                }
            }
        }
    }
}
