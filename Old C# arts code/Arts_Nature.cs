using System.Linq;
using RimWorld;
using Verse;

namespace ArknightsArts
{
    public class Hediff_NatureCorrosion : HediffWithComps
    {
        private int expiryTick = -1;
        private int nextDamageTick = -1;
        private Pawn instigatorCache;

        public const int DurationTicks = 300;
        public const int DamageIntervalTicks = 60;
        public const float DamagePerPulse = 4f;

        public void Initialise(Pawn instigator)
        {
            instigatorCache = instigator;
            expiryTick = Find.TickManager.TicksGame + DurationTicks;
            nextDamageTick = Find.TickManager.TicksGame + DamageIntervalTicks;
        }

        public void Refresh(Pawn instigator)
        {
            instigatorCache = instigator;
            expiryTick = Find.TickManager.TicksGame + DurationTicks;
        }

        public override void Tick()
        {
            base.Tick();
            if (pawn == null || pawn.Dead || pawn.Map == null) return;

            int now = Find.TickManager.TicksGame;
            if (now >= expiryTick) { pawn.health.RemoveHediff(this); return; }

            if (now >= nextDamageTick)
            {
                nextDamageTick = now + DamageIntervalTicks;
                ApplyCorrosionPulse();
            }
        }

        private void ApplyCorrosionPulse()
        {
            DamageDef damageDef = DefDatabase<DamageDef>.GetNamedSilentFail("ArtsDmg_Nature") ?? DamageDefOf.Deterioration;
            DamageInfo dinfo = new DamageInfo(damageDef, DamagePerPulse, 999f, -1f, instigatorCache ?? pawn);
            pawn.TakeDamage(dinfo);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref expiryTick, "expiryTick", -1);
            Scribe_Values.Look(ref nextDamageTick, "nextDamageTick", -1);
            Scribe_References.Look(ref instigatorCache, "instigatorCache");
        }
    }

    public static class NatureLogic
    {
        public static void TriggerNatureCorrosion(Pawn victim, Pawn attacker)
        {
            HediffDef corrosionDef = DefDatabase<HediffDef>.GetNamedSilentFail("Arts_NatureCorrosion");
            if (corrosionDef == null) return;

            var existing = victim.health.hediffSet.GetFirstHediffOfDef(corrosionDef) as Hediff_NatureCorrosion;

            if (existing != null)
            {
                // ПЕРЕПИСАНО: Замість existing.Refresh(attacker);
                return; // Якщо ворог вже під корозією, нічого не робимо.
            }
            else
            {
                var corrosion = (Hediff_NatureCorrosion)HediffMaker.MakeHediff(corrosionDef, victim);
                corrosion.Severity = 0.5f; // Початкова сила
                victim.health.AddHediff(corrosion);
                corrosion.Initialise(attacker);
            }
        }

        public static void ApplyInternalDamage(Pawn victim, Pawn attacker, float amount)
        {
            var part = victim.health.hediffSet.GetNotMissingParts()
                .Where(p => p.depth == BodyPartDepth.Inside).InRandomOrder().FirstOrDefault();

            if (part == null) return;

            DamageInfo internalDinfo = new DamageInfo(
                DamageDefOf.Stab, amount, armorPenetration: 0f, angle: -1f,
                instigator: attacker, hitPart: part, weapon: attacker?.equipment?.Primary?.def
            );
            victim.TakeDamage(internalDinfo);
        }
    }
}