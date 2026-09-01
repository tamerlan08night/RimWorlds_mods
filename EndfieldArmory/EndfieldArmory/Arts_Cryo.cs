using RimWorld;
using Verse;

namespace AKE.endfield
{
    public static class CryoLogic
    {
        public static void TriggerFreezeStun(Pawn victim, Pawn attacker)
        {
            victim.stances?.stunner?.StunFor(300, attacker);
            MoteMaker.ThrowText(victim.DrawPos, victim.Map, "FROZEN!", 3.5f);
        }

        public static void TriggerShatter(Pawn victim, ref DamageInfo dinfo, Hediff_ArtsElement cryoHediff)
        {
            dinfo.SetAmount(dinfo.Amount * 2.5f);
            victim.health.RemoveHediff(cryoHediff);
            MoteMaker.ThrowText(victim.DrawPos, victim.Map, "SHATTER!", 4f);
        }
    }
}
