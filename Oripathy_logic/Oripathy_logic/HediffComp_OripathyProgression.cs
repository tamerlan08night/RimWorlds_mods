using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AKEndfield
{
    public class HediffComp_OripathyProgression : HediffComp
    {
        // ── Стадія 3: Пошкодження органів ─────────────────────────────────────────────
        private bool organDamageApplied;

        // ── Етап 4: Видавання ресурсів ────────────────────────────────────────────
        private int resourceDropTicksRemaining;

        // ── Етап 5: Зворотний відлік до смерті ──────────────────────────────────────────
        private bool inStage5;
        private int stage5TicksRemaining;

        // Жорстко запрограмовані виключення органів
        private static readonly HashSet<string> AlwaysExcludedOrgans =
            new HashSet<string> { "Brain", "Heart" };

        public HediffCompProperties_OripathyProgression Props =>
            (HediffCompProperties_OripathyProgression)props;

        public override void CompPostMake()
        {
            base.CompPostMake();
            resourceDropTicksRemaining = Props.resourceDropIntervalTicks;
        }

        // Перевірка заморозки через наявність баффу на пішаку
        private bool IsFrozen
        {
            get
            {
                HediffDef stabilizedDef = HediffDef.Named("OE_OripathyStabilized");
                return stabilizedDef != null && Pawn.health.hediffSet.HasHediff(stabilizedDef);
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            // Якщо хвороба заморожена баффом, повністю зупиняємо зміну серйозності (прогресію)
            if (IsFrozen)
            {
                severityAdjustment = 0f;
            }

            float severity = parent.Severity;

            // ── Стадія 3: Одноразова кристалізація органу ───────────────────────
            if (!organDamageApplied && severity >= Props.stage3MinSeverity)
            {
                TryApplyOrganDamage();
            }

            // ── Етап 4: Періодичне падіння ресурсів ───────────────────────────────
            if (severity >= Props.stage4MinSeverity)
            {
                resourceDropTicksRemaining--;
                if (resourceDropTicksRemaining <= 0)
                {
                    TryDropResources();
                    resourceDropTicksRemaining = Props.resourceDropIntervalTicks;
                }
            }

            // ── Етап 5: Фатальний зворотний відлік ──────────────────────────────────────
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

            // Таймер смерті призупиняється, поки пішак заморожений баффом
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

            // Зберігаємо лише стадії, бо заморозка тепер живе у вигляді окремого Hediff на пішаку
            Scribe_Values.Look(ref organDamageApplied,        "organDamageApplied",        false);
            Scribe_Values.Look(ref resourceDropTicksRemaining, "resourceDropTicksRemaining", 60000);
            Scribe_Values.Look(ref inStage5,                  "inStage5",                  false);
            Scribe_Values.Look(ref stage5TicksRemaining,      "stage5TicksRemaining",      -1);
        }
    }
}