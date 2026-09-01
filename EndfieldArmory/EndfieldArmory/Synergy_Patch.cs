using RimWorld;
using Verse;

namespace AKE.endfield
{
    public class CompProperties_EdgeOfLightnessAura : CompProperties
    {
        public CompProperties_EdgeOfLightnessAura()
        {
            compClass = typeof(CompEdgeOfLightnessAura);
        }
    }

    public class CompEdgeOfLightnessAura : ThingComp
    {
        private const float AuraRadius = 7f;
        private const int AuraTickInterval = 60;
        private const float HediffSeverity = 1.0f;

        private static readonly HediffDef TacticalLinkDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("RE_TacticalLink");

        private int tickCounter = 0;

        public override void CompTick()
        {
            base.CompTick();

            tickCounter++;
            if (tickCounter < AuraTickInterval)
                return;
            tickCounter = 0;

            Pawn wielder = GetWielder();
            if (wielder == null || wielder.Dead || wielder.Map == null)
                return;

            ApplyAura(wielder);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref tickCounter, "tickCounter", 0);
        }

        private Pawn GetWielder()
        {
            if (parent.ParentHolder is Pawn_EquipmentTracker tracker)
                return tracker.pawn;
            return null;
        }

        private void ApplyAura(Pawn wielder)
        {
            Map map = wielder.Map;

            foreach (Pawn candidate in map.mapPawns.AllPawnsSpawned)
            {
                if (candidate.Dead || !candidate.Spawned)
                    continue;

                if (!IsFriendly(wielder, candidate))
                    continue;

                if (candidate.Position.DistanceTo(wielder.Position) > AuraRadius)
                    continue;

                RefreshTacticalLink(candidate);
            }
        }

        private static bool IsFriendly(Pawn wielder, Pawn candidate)
        {
            if (wielder.Faction == null)
                return false;

            return candidate.Faction == wielder.Faction;
        }

        private static void RefreshTacticalLink(Pawn pawn)
        {
            if (TacticalLinkDef == null) return;

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(TacticalLinkDef);

            if (existing == null)
            {
                Hediff fresh = HediffMaker.MakeHediff(TacticalLinkDef, pawn);
                fresh.Severity = HediffSeverity;
                pawn.health.AddHediff(fresh);
            }
            else
            {
                existing.Severity = HediffSeverity;

                var compDisappears = existing.TryGetComp<HediffComp_Disappears>();
                if (compDisappears != null)
                {
                    compDisappears.ticksToDisappear = 200;
                }
            }
        }
    }
}
