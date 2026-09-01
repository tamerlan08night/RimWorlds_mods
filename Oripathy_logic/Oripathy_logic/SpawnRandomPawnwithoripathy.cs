using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AKE.endfield
{
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new Type[] { typeof(PawnGenerationRequest) })]
    internal static class Patch_PawnGenerator_OripathyRandomSpawn
    {
        private static readonly HediffDef OripathyDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("OE_Oripathy");

        [HarmonyPostfix]
        public static void Postfix(Pawn __result)
        {
            if (__result == null || __result.RaceProps == null || !__result.RaceProps.Humanlike) return;
            if (OripathyDef == null) return;

            if (Rand.Value < 0.05f)
            {
                if (!__result.health.hediffSet.HasHediff(OripathyDef))
                {
                    Hediff oripathy = HediffMaker.MakeHediff(OripathyDef, __result);
                    oripathy.Severity = Rand.Range(0.01f, 0.60f);
                    __result.health.AddHediff(oripathy);
                }
            }
        }
    }
}
