using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EndfieldArmory
{
    /// <summary>
    /// CompProperties for the Edge of Lightness aura.
    /// Add to the weapon's ThingDef via XML:
    ///   <comps>
    ///     <li Class="EndfieldArmory.CompProperties_EdgeOfLightnessAura"/>
    ///   </comps>
    /// The ThingDef MUST also have <tickerType>Normal</tickerType>.
    /// </summary>
    public class CompProperties_EdgeOfLightnessAura : CompProperties
    {
        public CompProperties_EdgeOfLightnessAura()
        {
            compClass = typeof(CompEdgeOfLightnessAura);
        }
    }

    /// <summary>
    /// When the Edge of Lightness weapon is equipped by a pawn, this comp
    /// applies the RE_TacticalLink Hediff to all friendly pawns within
    /// AuraRadius tiles, refreshing it every AuraTickInterval ticks.
    ///
    /// TICK STRATEGY:
    /// RimWorld only calls CompTick() on Things whose ThingDef has
    /// tickerType = Normal. When the weapon sits in a Pawn_EquipmentTracker,
    /// the pawn's equipment tracker forwards ticks to each held ThingWithComps
    /// via ThingWithComps.Tick() → each comp's CompTick(). So as long as
    /// the ThingDef declares tickerType Normal, CompTick() fires every game
    /// tick even while the weapon is equipped — no custom hook needed.
    ///
    /// WIELDER DETECTION:
    /// parent.ParentHolder is cast to Pawn_EquipmentTracker; if successful,
    /// its .pawn property gives us the wielder. This is safer than any
    /// "equipped pawn" helper that may not exist in all 1.6 builds.
    /// </summary>
    public class CompEdgeOfLightnessAura : ThingComp
    {
        // ── Tunables ────────────────────────────────────────────────────────
        private const float AuraRadius = 7f;
        private const int AuraTickInterval = 60;   // ~1 second at normal speed
        private const float HediffSeverity = 1.0f;
        private static readonly HediffDef TacticalLinkDef =
            DefDatabase<HediffDef>.GetNamed("RE_TacticalLink");

        // ── State ────────────────────────────────────────────────────────────
        private int tickCounter = 0;

        // ── Core override ────────────────────────────────────────────────────
        public override void CompTick()
        {
            base.CompTick();

            // Only run once per interval.
            tickCounter++;
            if (tickCounter < AuraTickInterval)
                return;
            tickCounter = 0;

            // Resolve wielder from the parent's holder chain.
            Pawn wielder = GetWielder();
            if (wielder == null)
                return;

            // Safety: pawn must be alive and on a map.
            if (wielder.Dead || wielder.Map == null)
                return;

            ApplyAura(wielder);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the pawn currently holding this weapon, or null if the
        /// weapon is not equipped (e.g. lying on the ground or in storage).
        /// </summary>
        private Pawn GetWielder()
        {
            // parent.ParentHolder is the IThingHolder that owns this weapon.
            // When equipped it is a Pawn_EquipmentTracker.
            if (parent.ParentHolder is Pawn_EquipmentTracker tracker)
                return tracker.pawn;

            return null;
        }

        /// <summary>
        /// Iterates all spawned pawns on the map and applies/refreshes
        /// RE_TacticalLink on allies within range.
        /// Uses IReadOnlyList directly to avoid the CS0266 implicit-cast
        /// issue present in RimWorld 1.6.
        /// </summary>
        private void ApplyAura(Pawn wielder)
        {
            Map map = wielder.Map;

            // AllPawnsSpawned is IReadOnlyList<Pawn> in RimWorld 1.6 —
            // iterate with a plain foreach to avoid any List<T> cast.
            foreach (Pawn candidate in map.mapPawns.AllPawnsSpawned)
            {
                // Skip dead or non-spawned pawns.
                if (candidate.Dead || !candidate.Spawned)
                    continue;

                // Skip pawns that are not on the same team.
                if (!IsFriendly(wielder, candidate))
                    continue;

                // Distance check (squared comparison avoids a sqrt call).
                if (candidate.Position.DistanceTo(wielder.Position) > AuraRadius)
                    continue;

                // Apply or refresh the hediff.
                RefreshTacticalLink(candidate);
            }
        }

        /// <summary>
        /// Two pawns are "friendly" when they share the same faction,
        /// or when one is the wielder themselves.
        /// Handles null factions (e.g. wild animals) safely.
        /// </summary>
        private static bool IsFriendly(Pawn wielder, Pawn candidate)
        {
            if (wielder.Faction == null)
                return false;

            return candidate.Faction == wielder.Faction;
        }

        /// <summary>
        /// Adds RE_TacticalLink if the pawn doesn't have it yet;
        /// otherwise resets severity to 1.0 so it never expires while
        /// the wielder remains in range.
        /// </summary>
        private static void RefreshTacticalLink(Pawn pawn)
        {
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(TacticalLinkDef);

            if (existing == null)
            {
                Hediff fresh = HediffMaker.MakeHediff(TacticalLinkDef, pawn);
                fresh.Severity = HediffSeverity;
                pawn.health.AddHediff(fresh);
            }
            else
            {
                // Оновлюємо Severity
                existing.Severity = HediffSeverity;

                // ПРЯМЕ СКИНУТТЯ ТАЙМЕРА:
                // Шукаємо компонент Disappears і кажемо йому почати відлік заново
                var compDisappears = existing.TryGetComp<HediffComp_Disappears>();
                if (compDisappears != null)
                {
                    compDisappears.ticksToDisappear = 200; // Встановлюємо той самий час, що в XML
                }
            }
        }
    }
}
