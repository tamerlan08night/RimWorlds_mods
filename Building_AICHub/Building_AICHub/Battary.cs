using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AKE.endfield
{
    [StaticConstructorOnStartup]
    public class Building_ThermiteBank : Building
    {
        private Building_AICHub linkedHub;
        private CompPowerTrader powerComp;
        private CompRefuelable refuelComp;

        private int hubCheckTicks = 0;
        private Sustainer soundSustainer;

        public ThingDef selectedBatteryDef;
        public ThingDef activeBatteryDef;

        private static ThingDef batteryLC;
        private static ThingDef batterySC;
        private static ThingDef batteryHC;
        private static SoundDef ambientSoundDef;

        static Building_ThermiteBank()
        {
            batteryLC = DefDatabase<ThingDef>.GetNamedSilentFail("IC_LCValleyBattery");
            batterySC = DefDatabase<ThingDef>.GetNamedSilentFail("IC_SCValleyBattery");
            batteryHC = DefDatabase<ThingDef>.GetNamedSilentFail("IC_HCValleyBattery");

            // Завантажуємо DefName вашого звуку
            ambientSoundDef = SoundDef.Named("EF_ThermiteBank_Ambience");
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
            refuelComp = GetComp<CompRefuelable>();

            if (refuelComp != null && refuelComp.props is CompProperties_Refuelable origProps)
            {
                var customProps = new CompProperties_Refuelable
                {
                    fuelConsumptionRate = origProps.fuelConsumptionRate,
                    fuelCapacity = origProps.fuelCapacity,
                    showFuelGizmo = origProps.showFuelGizmo,
                    autoRefuelPercent = origProps.autoRefuelPercent,
                    destroyOnNoFuel = origProps.destroyOnNoFuel,
                    fuelFilter = new ThingFilter()
                };
                refuelComp.props = customProps;
            }

            if (selectedBatteryDef == null)
            {
                selectedBatteryDef = batteryLC;
            }

            UpdateFuelFilter();
        }

        protected override void Tick()
        {
            base.Tick();

            if (refuelComp == null) return;

            if (refuelComp.HasFuel)
            {
                if (activeBatteryDef == null)
                {
                    activeBatteryDef = selectedBatteryDef;
                }

                // Вмикаємо/підтримуємо звук під час роботи
                MaintainWorkingSound();
            }
            else
            {
                activeBatteryDef = null;
                StopWorkingSound();

                hubCheckTicks--;
                if (hubCheckTicks <= 0)
                {
                    FindAndConsumeFromHub();
                    hubCheckTicks = 250;
                }
            }

            UpdatePowerOutput();
        }

        private void MaintainWorkingSound()
        {
            if (ambientSoundDef == null) return;

            if (soundSustainer == null || soundSustainer.Ended)
            {
                SoundInfo info = SoundInfo.InMap(this, MaintenanceType.PerTick);
                soundSustainer = ambientSoundDef.TrySpawnSustainer(info);
            }
            soundSustainer?.Maintain();
        }

        private void StopWorkingSound()
        {
            if (soundSustainer != null && !soundSustainer.Ended)
            {
                soundSustainer.End();
            }
            soundSustainer = null;
        }

        private void FindAndConsumeFromHub()
        {
            if (selectedBatteryDef == null) return;

            linkedHub = null;
            var hubs = Map.listerBuildings.AllBuildingsColonistOfClass<Building_AICHub>();
            foreach (var hub in hubs)
            {
                if (Position.DistanceTo(hub.Position) <= 18f)
                {
                    linkedHub = hub;
                    break;
                }
            }

            if (linkedHub != null && linkedHub.HasResources(selectedBatteryDef, 1))
            {
                if (linkedHub.TryConsumeResources(selectedBatteryDef, 1))
                {
                    refuelComp.Refuel(1f);
                    activeBatteryDef = selectedBatteryDef;
                }
            }
        }

        private void UpdatePowerOutput()
        {
            if (powerComp == null || refuelComp == null || !refuelComp.HasFuel || activeBatteryDef == null)
            {
                if (powerComp != null) powerComp.PowerOutput = 0f;
                StopWorkingSound();
                return;
            }

            if (activeBatteryDef == batteryHC)
                powerComp.PowerOutput = 3200f;
            else if (activeBatteryDef == batterySC)
                powerComp.PowerOutput = 1200f;
            else
                powerComp.PowerOutput = 600f;
        }

        private void UpdateFuelFilter()
        {
            if (refuelComp == null || selectedBatteryDef == null) return;

            var filter = refuelComp.Props.fuelFilter;
            if (filter != null)
            {
                filter.SetDisallowAll();
                filter.SetAllow(selectedBatteryDef, true);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;

            Texture2D buttonIcon = selectedBatteryDef?.uiIcon ?? BaseContent.BadTex;

            yield return new Command_Action
            {
                defaultLabel = $"Target: {selectedBatteryDef?.label ?? "None"}",
                defaultDesc = "Select battery type to request for this Thermite Bank.",
                icon = buttonIcon,
                action = () =>
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();

                    if (batteryLC != null)
                    {
                        options.Add(new FloatMenuOption("IC_LCValleyBattery (600 W)", () =>
                        {
                            selectedBatteryDef = batteryLC;
                            UpdateFuelFilter();
                        }));
                    }
                    if (batterySC != null)
                    {
                        options.Add(new FloatMenuOption("IC_SCValleyBattery (1200 W)", () =>
                        {
                            selectedBatteryDef = batterySC;
                            UpdateFuelFilter();
                        }));
                    }
                    if (batteryHC != null)
                    {
                        options.Add(new FloatMenuOption("IC_HCValleyBattery (3200 W)", () =>
                        {
                            selectedBatteryDef = batteryHC;
                            UpdateFuelFilter();
                        }));
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            StopWorkingSound();

            if ((mode == DestroyMode.Deconstruct || mode == DestroyMode.KillFinalize) &&
                refuelComp != null && refuelComp.Fuel >= 0.9f && activeBatteryDef != null)
            {
                Thing droppedBattery = ThingMaker.MakeThing(activeBatteryDef);
                droppedBattery.stackCount = 1;
                GenPlace.TryPlaceThing(droppedBattery, Position, Map, ThingPlaceMode.Near);
            }

            base.Destroy(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref selectedBatteryDef, "selectedBatteryDef");
            Scribe_Defs.Look(ref activeBatteryDef, "activeBatteryDef");
            Scribe_References.Look(ref linkedHub, "linkedHub");
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();

            if (activeBatteryDef != null && refuelComp != null && refuelComp.HasFuel)
            {
                text += $"\nBurning: {activeBatteryDef.LabelCap}";
            }

            if (selectedBatteryDef != null)
            {
                text += $"\nTarget Filter: {selectedBatteryDef.LabelCap}";
            }

            if (linkedHub != null && Position.DistanceTo(linkedHub.Position) <= 18f)
            {
                text += "\nAIC Hub: Connected";
            }
            else
            {
                text += "\nAIC Hub: Not in range (Manual fuel)";
            }
            return text;
        }
    }
}