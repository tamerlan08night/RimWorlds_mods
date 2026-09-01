using HarmonyLib;
using RimWorld;
using Verse;

namespace AKEndfield
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
            // Перевіряємо, чи гра намагається видалити саме Оріпатію
            if (hediff is Hediff_Oripathy)
            {
                // Дозволяємо видалення, якщо увімкнено режим розробника (Dev Mode)
                if (Verse.Prefs.DevMode)
                {
                    return true;
                }
                
                // Якщо Dev Mode вимкнено — кажемо "ні", хвороба невиліковна
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
            // Guard: only fire for living colony members dying of Oripathy.
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

    
    // Зараження орипатією від видобутку корисних копалин (20% шанс за удар)
    [HarmonyPatch(typeof(Mineable), nameof(Mineable.Notify_TookMiningDamage))]
    internal static class Patch_Mineable_Notify_TookMiningDamage_Oripathy
    {
        [HarmonyPostfix]
        public static void Postfix(Mineable __instance, int amount, Pawn miner)
        {
            // 1. Перевіряємо, чи шахтар - це людина (а не тварина, механоїд чи вибух)
            if (miner == null || miner.RaceProps == null || !miner.RaceProps.Humanlike) return;

            // 2. Перевіряємо чи стіна яку копають - це оріджиніум.
            if (__instance.def.defName == "OE_OriginiumSurface")
            {
                // 3. Лотерея з шансом 20% (0.20f)
                if (Rand.Value < 0.20f)
                {
                    HediffDef oripathyDef = HediffDef.Named("OE_Oripathy");

                    if (oripathyDef != null)
                    {
                        // Перевіряємо, чи пішак уже заражений Оріпатією
                        Hediff existingOripathy = miner.health.hediffSet.GetFirstHediffOfDef(oripathyDef);

                        // Якщо ще ні — заражаємо його
                        if (existingOripathy == null)
                        {
                            Hediff newOripathy = HediffMaker.MakeHediff(oripathyDef, miner);
                            newOripathy.Severity = 0.01f; // Початкова стадія хвороби
                            miner.health.AddHediff(newOripathy);

                            // Виводимо сповіщення на екран
                            Messages.Message("Oripathy Infection Mining".Translate(miner.LabelShort, __instance.Label),
                                miner, MessageTypeDefOf.NegativeEvent);
                        }
                    }
                }
            }
        }
    }
}