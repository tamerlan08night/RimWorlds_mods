using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AKE.endfield
{
    public class HediffComp_OripathyProgression : HediffComp
    {
        private bool organDamageApplied;
        private int resourceDropTicksRemaining;
        private bool inStage5;
        private int stage5TicksRemaining;

        private static readonly HashSet<string> AlwaysExcludedOrgans =
            new HashSet<string> { "Brain", "Heart" };

        private static readonly HediffDef StabilizedDef =
            DefDatabase<HediffDef>.GetNamedSilentFail("OE_OripathyStabilized");

        public HediffCompProperties_OripathyProgression Props =>
            (HediffCompProperties_OripathyProgression)props;

        public override void CompPostMake()
        {
            base.CompPostMake();
            resourceDropTicksRemaining = Props.resourceDropIntervalTicks;
        }

        private bool IsFrozen
        {
            get
            {
                return StabilizedDef != null && Pawn.health.hediffSet.HasHediff(StabilizedDef);
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn == null || Pawn.Dead) return;

            if (IsFrozen)
            {
                severityAdjustment = 0f;
            }

            float severity = parent.Severity;

            if (!organDamageApplied && severity >= Props.stage3MinSeverity)
            {
                TryApplyOrganDamage();
            }

            if (severity >= Props.stage4MinSeverity)
            {
                resourceDropTicksRemaining--;
                if (resourceDropTicksRemaining <= 0)
                {
                    TryDropResources();
                    resourceDropTicksRemaining = Props.resourceDropIntervalTicks;
                }
            }

            HandleStage5Tick(severity);
        }

        private void HandleStage5Tick(float severity)
        {
            if (severity < Props.stage5MinSeverity)
            {
                inStage5 = false;
                return;
            }

            if (!inStage5)
            {
                inStage5 = true;
                stage5TicksRemaining = Props.stage5FatalDurationTicks;

                Messages.Message(
                    "OripathyFatalStageMessage".Translate(Pawn.Named("PAWN")),
                    Pawn,
                    MessageTypeDefOf.ThreatBig);
            }

            if (!IsFrozen)
            {
                stage5TicksRemaining--;
            }

            if (stage5TicksRemaining <= 0 && !Pawn.Dead)
            {
                KillFromOripathy();
            }
        }

        private void TryApplyOrganDamage()
        {
            organDamageApplied = true;

            if (Props.organDamageHediffDef == null)
            {
                Log.Warning("[Oripathy] organDamageHediffDef is null.");
                return;
            }

            var eligible = GetEligibleOrganParts();
            eligible.Shuffle();

            int toApply = Math.Min(Props.organDamageCount, eligible.Count);
            for (int i = 0; i < toApply; i++)
            {
                var crystallisation = HediffMaker.MakeHediff(
                    Props.organDamageHediffDef, Pawn, eligible[i]);
                Pawn.health.AddHediff(crystallisation, eligible[i]);
            }

            if (toApply > 0)
            {
                Messages.Message(
                    "OripathyOrganDamageMessage".Translate(Pawn.Named("PAWN"), toApply),
                    Pawn,
                    MessageTypeDefOf.NegativeEvent);
            }
        }

        private List<BodyPartRecord> GetEligibleOrganParts()
        {
            var excluded = new HashSet<string>(AlwaysExcludedOrgans);
            foreach (string extra in Props.additionalExcludedOrgans)
                excluded.Add(extra);

            excluded.Add("Torso");
            excluded.Add("Pelvis");
            excluded.Add("Neck");

            var result = new List<BodyPartRecord>();
            foreach (var part in Pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.depth != BodyPartDepth.Inside) continue;
                if (part.def.tags.NullOrEmpty()) continue;
                if (excluded.Contains(part.def.defName)) continue;

                result.Add(part);
            }
            return result;
        }

        private void TryDropResources()
        {
            if (Props.resourceDropDef == null || !Pawn.Spawned) return;

            Thing drop = ThingMaker.MakeThing(Props.resourceDropDef);
            drop.stackCount = Mathf.Clamp(
                Props.resourceDropAmount,
                1,
                Props.resourceDropDef.stackLimit);

            GenPlace.TryPlaceThing(drop, Pawn.Position, Pawn.Map, ThingPlaceMode.Near);
        }

        private void KillFromOripathy()
        {
            Pawn.Kill(null, parent);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();

            Scribe_Values.Look(ref organDamageApplied, "organDamageApplied", false);
            Scribe_Values.Look(ref resourceDropTicksRemaining, "resourceDropTicksRemaining", 30000);
            Scribe_Values.Look(ref inStage5, "inStage5", false);
            Scribe_Values.Look(ref stage5TicksRemaining, "stage5TicksRemaining", -1);
        }
    }
}
