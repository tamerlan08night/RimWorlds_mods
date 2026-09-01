using RimWorld;
using Verse;

namespace ArknightsArts
{
    public static class CryoLogic
    {
        // Викликається, коли ворог переходить на 4-ту стадію (заморозка)
        public static void TriggerFreezeStun(Pawn victim, Pawn attacker)
        {
            victim.stances?.stunner?.StunFor(300, attacker); // [cite: 138]
            MoteMaker.ThrowText(victim.DrawPos, victim.Map, "FROZEN!", 3.5f); // [cite: 138]
        }

        // Викликається при наступному ударі по замороженому ворогу (розбиття)
        public static void TriggerShatter(Pawn victim, ref DamageInfo dinfo, Hediff_ArtsElement cryoHediff)
        {
            dinfo.SetAmount(dinfo.Amount * 2.5f); // [cite: 143]
            victim.health.RemoveHediff(cryoHediff); // [cite: 143]
            MoteMaker.ThrowText(victim.DrawPos, victim.Map, "SHATTER!", 4f); // [cite: 143]
        }
    }
}