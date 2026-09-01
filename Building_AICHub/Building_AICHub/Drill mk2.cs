using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AKEndfield
{
    [StaticConstructorOnStartup]
    public static class Building_OriginiumDrill_MKII
    {
        static Building_OriginiumDrill_MKII()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def?.placeWorkers != null)
                {
                    def.placeWorkers.RemoveAll(t => t == null);
                }
            }

            ThingDef drillDef = DefDatabase<ThingDef>.GetNamed("OE_OriginiumDrill_MKII", false);
            if (drillDef != null)
            {
                if (drillDef.placeWorkers == null)
                {
                    drillDef.placeWorkers = new List<Type>();
                }

                if (!drillDef.placeWorkers.Contains(typeof(PlaceWorker_DrillAutonomousRadius_MK2)))
                {
                    drillDef.placeWorkers.Add(typeof(PlaceWorker_DrillAutonomousRadius_MK2));
                }
            }
        }
    }

    public class PlaceWorker_ShowRadius_MKII : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            GenDraw.DrawRadiusRing(center, 5f);
        }
    }

    public class PlaceWorker_DrillAutonomousRadius_MK2 : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            GenDraw.DrawRadiusRing(center, 5f); 
        }
    }

    public class Building_OriginiumDrillMKII : Building
    {
        private const int TransferInterval = 1200;
        private const int EmergencyEjectMax = 50;
        private const float MiningProgressPerTick = 1f / 1200f;

        // МАКСИМАЛЬНА МІСТКІСТЬ ВНУТРІШНЬОГО СХОВИЩА БУРА MKII
        private const int MaxInternalCapacity = 300;

        private Building_AICHub linkedHub;
        private Dictionary<ThingDef, int> internalCache = new Dictionary<ThingDef, int>();
        private CompPowerTrader powerComp;

        private List<ThingDef> cacheKeysWorkingList;
        private List<int> cacheValuesWorkingList;

        private readonly object cacheLock = new object();
        private int transferTick = 0;
        private float miningProgress = 0f;

        private Sustainer sustainer;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            this.powerComp = this.GetComp<CompPowerTrader>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref linkedHub, "linkedHub");
            Scribe_Values.Look(ref miningProgress, "miningProgress", 0f);
            Scribe_Collections.Look(ref internalCache, "internalCache", LookMode.Def, LookMode.Value, ref cacheKeysWorkingList, ref cacheValuesWorkingList);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && internalCache == null)
            {
                internalCache = new Dictionary<ThingDef, int>();
            }
        }

        protected override void Tick()
        {
            base.Tick();

            // 1. Перевірка наявності електроенергії
            if (powerComp != null && !powerComp.PowerOn)
            {
                StopDrillSound();
                return;
            }

            // 2. Перевірка заповненості сховища
            bool isBufferFull = GetTotalCacheUnits() >= MaxInternalCapacity;

            if (isBufferFull)
            {
                // При заповненому буфері зупиняємо звук і прогрес буріння
                StopDrillSound();
            }
            else
            {
                // Якщо є місце — продовжуємо буріння та звук
                MaintainDrillSound();

                miningProgress += MiningProgressPerTick;
                if (miningProgress >= 1f)
                {
                    miningProgress = 0f;
                    ExecuteAutonomousMining();
                }
            }

            // 3. Спроба передачі ресурсів у ХАБ (працює завжди, щоб звільнити місце при появі зв'язку/простору)
            transferTick++;
            if (transferTick >= TransferInterval)
            {
                transferTick = 0;
                TryStreamToHub();
            }
        }

        private void MaintainDrillSound()
        {
            if (sustainer == null || sustainer.Ended)
            {
                SoundDef soundDef = SoundDef.Named("EF_drill");
                if (soundDef != null)
                {
                    SoundInfo info = SoundInfo.InMap(this, MaintenanceType.PerTick);
                    sustainer = soundDef.TrySpawnSustainer(info);
                }
            }
            sustainer?.Maintain();
        }

        private void StopDrillSound()
        {
            if (sustainer != null && !sustainer.Ended)
            {
                sustainer.End();
            }
            sustainer = null;
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            StopDrillSound();
            EjectAllInternalCacheOnGround();
            base.DeSpawn(mode);
        }

        private void ExecuteAutonomousMining()
        {
            if (this.Map == null) return;

            ThingDef foundResource = null;
            IntVec3 targetCell = IntVec3.Invalid;

            foreach (IntVec3 cell in this.OccupiedRect())
            {
                ThingDef d = this.Map.deepResourceGrid.ThingDefAt(cell);
                int countAtCell = this.Map.deepResourceGrid.CountAt(cell);

                if (d != null && countAtCell > 0)
                {
                    foundResource = d;
                    targetCell = cell;
                    break;
                }
            }

            if (foundResource == null)
            {
                float maxScanRadius = 4f;
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(this.Position, maxScanRadius, true))
                {
                    if (!cell.InBounds(this.Map)) continue;

                    ThingDef d = this.Map.deepResourceGrid.ThingDefAt(cell);
                    int countAtCell = this.Map.deepResourceGrid.CountAt(cell);

                    if (d != null && countAtCell > 0)
                    {
                        foundResource = d;
                        targetCell = cell;
                        break;
                    }
                }
            }

            if (foundResource != null && targetCell.IsValid)
            {
                int yieldCount = foundResource.deepCountPerPortion;
                if (yieldCount <= 0) yieldCount = 15;

                int currentGridCount = this.Map.deepResourceGrid.CountAt(targetCell);

                if (currentGridCount < yieldCount)
                {
                    yieldCount = currentGridCount;
                }

                int newGridCount = currentGridCount - yieldCount;
                this.Map.deepResourceGrid.SetAt(targetCell, foundResource, newGridCount);

                int finalYield = Mathf.RoundToInt(yieldCount * 1.30f);
                if (finalYield < 1 && yieldCount > 0) finalYield = 1;

                ProcessMinedResource(foundResource, finalYield);
            }
            else
            {
                ThingDef backupRockChunk = DeepDrillUtility.GetBaseResource(this.Map, this.Position);
                if (backupRockChunk != null)
                {
                    ProcessMinedResource(backupRockChunk, 1);
                }
            }
        }

        private void ProcessMinedResource(ThingDef def, int count)
        {
            if (def == null || count <= 0) return;

            int remaining = count;

            // 1. Спроба відразу передати у зв'язаний ХАБ
            if (linkedHub != null && !linkedHub.Destroyed && linkedHub.Spawned)
            {
                int absorbed = linkedHub.InjectResource(def, remaining);
                remaining -= absorbed;
            }

            if (remaining <= 0) return;

            // 2. Додавання у внутрішнє сховище бура (надлишок не випадає на землю)
            AddToCache(def, remaining);
        }

        public int GetTotalCacheUnits()
        {
            lock (cacheLock)
            {
                int total = 0;
                foreach (var kv in internalCache)
                {
                    if (kv.Value > 0) total += kv.Value;
                }
                return total;
            }
        }

        public int AddToCache(ThingDef def, int count)
        {
            if (def == null || count <= 0) return 0;

            lock (cacheLock)
            {
                int currentTotal = GetTotalCacheUnits();
                int spaceLeft = MaxInternalCapacity - currentTotal;

                if (spaceLeft <= 0)
                {
                    return count;
                }

                int toAdd = Mathf.Min(count, spaceLeft);

                if (internalCache.TryGetValue(def, out int existing))
                    internalCache[def] = existing + toAdd;
                else
                    internalCache[def] = toAdd;

                return count - toAdd;
            }
        }

        private void TryStreamToHub()
        {
            if (linkedHub == null || linkedHub.Destroyed || !linkedHub.Spawned)
                return;

            lock (cacheLock)
            {
                if (internalCache.Count == 0) return;

                List<ThingDef> keys = new List<ThingDef>(internalCache.Keys);
                foreach (ThingDef def in keys)
                {
                    int currentAmount = internalCache[def];
                    if (currentAmount <= 0) continue;

                    int absorbed = linkedHub.InjectResource(def, currentAmount);
                    if (absorbed > 0)
                    {
                        int remaining = currentAmount - absorbed;
                        if (remaining <= 0)
                            internalCache.Remove(def);
                        else
                            internalCache[def] = remaining;
                    }
                }
            }
        }

        private void EjectAllInternalCacheOnGround()
        {
            if (Map == null) return;

            lock (cacheLock)
            {
                foreach (KeyValuePair<ThingDef, int> kv in internalCache)
                {
                    if (kv.Value <= 0) continue;
                    DropResourceOnGround(kv.Key, kv.Value);
                }
                internalCache.Clear();
            }
        }

        private void DropResourceOnGround(ThingDef def, int count)
        {
            if (def == null || count <= 0 || Map == null) return;

            int remaining = count;
            while (remaining > 0)
            {
                int stackToSpawn = Mathf.Min(remaining, def.stackLimit);
                Thing drop = ThingMaker.MakeThing(def);
                drop.stackCount = stackToSpawn;
                GenPlace.TryPlaceThing(drop, Position, Map, ThingPlaceMode.Near);
                remaining -= stackToSpawn;
            }
        }

        private void EmergencyEject()
        {
            ThingDef ejectable = null;
            int available = 0;

            lock (cacheLock)
            {
                foreach (KeyValuePair<ThingDef, int> kv in internalCache)
                {
                    if (kv.Value > 0)
                    {
                        ejectable = kv.Key;
                        available = kv.Value;
                        break;
                    }
                }
            }

            if (ejectable == null)
            {
                Messages.Message("OriginiumDrill: Internal cache is empty.", this, MessageTypeDefOf.RejectInput, false);
                return;
            }

            int toEject = Mathf.Min(EmergencyEjectMax, available);

            lock (cacheLock)
            {
                int remaining = internalCache[ejectable] - toEject;
                if (remaining <= 0)
                    internalCache.Remove(ejectable);
                else
                    internalCache[ejectable] = remaining;
            }

            DropResourceOnGround(ejectable, toEject);
            Messages.Message($"OriginiumDrill: Ejected {toEject}x {ejectable.LabelCap}.", this, MessageTypeDefOf.PositiveEvent, false);
        }

        private void BeginGlobalHubSelection()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            if (this.Map != null)
            {
                foreach (Building_AICHub foundHub in this.Map.listerBuildings.AllBuildingsColonistOfClass<Building_AICHub>())
                {
                    string label = foundHub.HubName; 

                    options.Add(new FloatMenuOption(label, () =>
                    {
                        this.linkedHub = foundHub;
                        Messages.Message($"Successfully linked to {label}!", this, MessageTypeDefOf.TaskCompletion, false);
                    }));
                }
            }

            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                Messages.Message("No AIC Hubs found on this map.", MessageTypeDefOf.RejectInput, false);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
                yield return g;

            yield return new Command_Action
            {
                defaultLabel = "Link to AIC Hub",
                defaultDesc = "Connect this automated drill to a local AIC Logistics Hub.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchReport", true),
                action = BeginGlobalHubSelection
            };

            yield return new Command_Action
            {
                defaultLabel = "Emergency Eject",
                defaultDesc = "Manually purge items from the internal buffer.",
                icon = ContentFinder<Texture2D>.Get("Things/UI/Commands/DeepDrillDrop", true),
                action = EmergencyEject
            };
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder(base.GetInspectString());
            bool connected = linkedHub != null && !linkedHub.Destroyed && linkedHub.Spawned;

            sb.AppendLine();
            sb.AppendLine(connected ? "AIC Hub: Connected" : "AIC Hub: Not Connected");

            lock (cacheLock)
            {
                int totalStored = GetTotalCacheUnits();
                
                if (totalStored >= MaxInternalCapacity)
                {
                    sb.AppendLine("Status: Stopped (Buffer Full)");
                }
                else
                {
                    sb.AppendLine($"Automated Drill Progress: {(miningProgress * 100f).ToString("F0")}%");
                }

                sb.AppendLine($"Internal Buffer: {totalStored} / {MaxInternalCapacity} units");

                bool hasAnything = false;
                foreach (var kv in internalCache)
                {
                    if (kv.Value <= 0) continue;
                    if (!hasAnything)
                    {
                        sb.AppendLine("Contents:");
                        hasAnything = true;
                    }
                    sb.AppendLine($"  {kv.Key.LabelCap}: {kv.Value}");
                }
            }
            return sb.ToString().TrimEnd();
        }
    }
}