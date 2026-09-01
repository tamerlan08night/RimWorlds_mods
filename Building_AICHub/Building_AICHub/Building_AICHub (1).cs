using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace AKE.endfield
{
    [StaticConstructorOnStartup]
    public class Building_AICHub : Building
    {
        private string hubName;
        public string HubName => string.IsNullOrEmpty(hubName) ? def.label : hubName;
        public override string LabelNoCount => HubName;
        
        private const int QuickEjectAmount = 100;
        private const float BaseWindPower = 3500f;
        private const float MinWindPower = 1500f;
        
        // Ліміт: 3000 штук для кожного окремого типу предмета
        private const int MaxPerItemCapacity = 6000;

        private Dictionary<ThingDef, int> inventory = new Dictionary<ThingDef, int>();
        private HashSet<ThingDef> absorptionEnabled = new HashSet<ThingDef>();

        private CompPowerPlant powerComp;
        private readonly object inventoryLock = new object();

        private static ThingCategoryDef _catManufactured;
        private static ThingCategoryDef _catAmmo;

        // Таймер для запобігання спаму повідомленнями про брак місця
        private int lastRejectMessageTick = -300;

        static Building_AICHub()
        {
            _catManufactured = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Manufactured");
            _catAmmo = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Ammo");
        }

        public object InventoryLock => inventoryLock;
        public HashSet<ThingDef> AbsorptionEnabled => absorptionEnabled;

        public void SetCustomName(string newName)
        {
            this.hubName = newName;
        }

        public int GetStoredCount(ThingDef def)
        {
            if (def == null) return 0;
            lock (inventoryLock)
            {
                return inventory.TryGetValue(def, out int count) ? count : 0;
            }
        }

        public void GetInventorySummary(out int types, out int units)
        {
            types = 0;
            units = 0;
            lock (inventoryLock)
            {
                foreach (KeyValuePair<ThingDef, int> kv in inventory)
                {
                    if (kv.Value <= 0) continue;
                    types++;
                    units += kv.Value;
                }
            }
        }

        public int GetTotalStoredUnits()
        {
            GetInventorySummary(out _, out int units);
            return units;
        }

        public bool IsAcceptableResource(ThingDef def)
        {
            if (def == null) return false;

            // ПОВНЕ БЛОКУВАННЯ ТОКСИЧНИХ ВІДХОДІВ (Biotech / Vanilla / Mods)
            if (def.defName.IndexOf("Waste", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                def.defName.IndexOf("Toxic", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                def.defName.IndexOf("Pollution", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            if (def.IsWeapon || def.IsApparel || def.IsMedicine || def.IsDrug || def.IsIngestible || def.IsCorpse || def.projectile != null) return false;
            if (def.defName.IndexOf("Ammo", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (_catAmmo != null && def.IsWithinCategory(_catAmmo)) return false;
            if (def.CountAsResource) return true;
            if (ThingCategoryDefOf.ResourcesRaw != null && def.IsWithinCategory(ThingCategoryDefOf.ResourcesRaw)) return true;
            if (_catManufactured != null && def.IsWithinCategory(_catManufactured)) return true;
            if (ThingCategoryDefOf.Chunks != null && def.IsWithinCategory(ThingCategoryDefOf.Chunks)) return true;
            return false;
        }

        public bool HasResources(ThingDef def, int count)
        {
            if (!IsAcceptableResource(def) || count <= 0) return false;
            lock (inventoryLock)
            {
                return inventory.TryGetValue(def, out int current) && current >= count;
            }
        }

        public bool TryConsumeResources(ThingDef def, int count)
        {
            if (!IsAcceptableResource(def) || count <= 0) return false;
            lock (inventoryLock)
            {
                if (!inventory.TryGetValue(def, out int current) || current < count) return false;
                inventory[def] = current - count;
                return true;
            }
        }

        /// <summary>
        /// Перевіряє, чи є місце для додавання конкретного ресурсу (не більше 3000 для цього типу).
        /// </summary>
        public bool CanAcceptResource(ThingDef def, int count = 1)
        {
            if (!IsAcceptableResource(def) || count <= 0) return false;
            lock (inventoryLock)
            {
                int existingDefCount = inventory.TryGetValue(def, out int ex) ? ex : 0;
                return existingDefCount < MaxPerItemCapacity;
            }
        }

        /// <summary>
        /// Додає ресурс в інвентар Хабу (до 3000 од. на тип) та повертає кількість реально прийнятих одиниць.
        /// </summary>
        public int InjectResource(ThingDef def, int count)
        {
            if (!IsAcceptableResource(def) || count <= 0) return 0;

            lock (inventoryLock)
            {
                int existingDefCount = inventory.TryGetValue(def, out int ex) ? ex : 0;
                int spaceLeftForDef = MaxPerItemCapacity - existingDefCount;

                if (spaceLeftForDef <= 0)
                {
                    NotifyNotEnoughSpace();
                    return 0;
                }

                int actualToAdd = Mathf.Min(count, spaceLeftForDef);
                inventory[def] = existingDefCount + actualToAdd;

                // Якщо прийняли менше ніж запитували — для цього предмета сховище заповнилося
                if (actualToAdd < count)
                {
                    NotifyNotEnoughSpace();
                }

                return actualToAdd;
            }
        }

        /// <summary>
        /// Безпечно поглинає об'єкт з карти або фабрики. 
        /// Зменшує стак і знищує об'єкт ТІЛЬКИ якщо всі одиниці помістилися.
        /// </summary>
        public bool TryInjectThing(Thing thing)
        {
            if (thing == null || !IsAcceptableResource(thing.def)) return false;

            int added = InjectResource(thing.def, thing.stackCount);
            if (added <= 0) return false;

            if (added >= thing.stackCount)
            {
                thing.Destroy(DestroyMode.Vanish);
            }
            else
            {
                thing.stackCount -= added;
            }
            return true;
        }

        private void NotifyNotEnoughSpace()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            
            int currentTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            // Виводимо повідомлення не частіше ніж раз на 250 тіків (~4 секунди)
            if (currentTick - lastRejectMessageTick > 250)
            {
                lastRejectMessageTick = currentTick;
                Messages.Message("AIC Hub: Storage full for this resource type (6000 max).", this, MessageTypeDefOf.RejectInput, historical: false);
            }
        }

        public void DispenseResource(ThingDef def, int count)
        {
            if (def == null || count <= 0 || Map == null) return;
            int actualCount;
            lock (inventoryLock)
            {
                if (!inventory.TryGetValue(def, out int stored) || stored <= 0) return;
                actualCount = Mathf.Min(count, stored);
                inventory[def] = stored - actualCount;
            }
            Thing thing = ThingMaker.MakeThing(def);
            thing.stackCount = actualCount;
            GenSpawn.Spawn(thing, FindDispenseCell(), Map, WipeMode.VanishOrMoveAside);
        }

        private IntVec3 FindDispenseCell()
        {
            IntVec3 ic = InteractionCell;
            if (ic.IsValid && ic.InBounds(Map) && ic.Walkable(Map)) return ic;
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(this))
                if (cell.InBounds(Map) && cell.Walkable(Map)) return cell;
            return Position;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerPlant>();
        }

        public override void TickRare()
        {
            base.TickRare();

            if (powerComp == null)
            {
                powerComp = GetComp<CompPowerPlant>();
            }

            if (powerComp != null && Map != null)
            {
                float windSpeed = Map.windManager.WindSpeed;
                float finalPower = MinWindPower + (BaseWindPower * windSpeed);
                powerComp.PowerOutput = finalPower;
            }
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            GenDraw.DrawRadiusRing(Position, 18f);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;

            yield return new Command_Action
            {
                defaultLabel = "Open Storage Interface",
                defaultDesc = "Opens the AIC Logistics Control Terminal.",
                icon = ContentFinder<Texture2D>.Get("Things/UI/Commands/AICstorage", true),
                action = () => Find.WindowStack.Add(new Dialog_AICHubStorage(this))
            };

            yield return new Command_Action
            {
                defaultLabel = "Rename HUB",
                defaultDesc = "Change the unique name of this AIC Hub.",
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/Rename", true),
                action = () => Find.WindowStack.Add(new Dialog_RenameHub(this))
            };

            yield return new Command_Action
            {
                defaultLabel = "Quick Eject Raw Materials",
                defaultDesc = $"[DEBUG] Ejects up to {QuickEjectAmount} units.",
                icon = ContentFinder<Texture2D>.Get("Things/UI/Commands/DropCarriedThing", true),
                action = QuickEjectFirstAvailableResource
            };
        }

        private void QuickEjectFirstAvailableResource()
        {
            ThingDef target = null;
            int stock = 0;
            lock (inventoryLock)
            {
                foreach (KeyValuePair<ThingDef, int> kv in inventory)
                {
                    if (kv.Value > 0) { target = kv.Key; stock = kv.Value; break; }
                }
            }
            if (target == null)
            {
                Messages.Message("AIC Hub: No resources available to eject.", this, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            int toEject = Mathf.Min(QuickEjectAmount, stock);
            DispenseResource(target, toEject);
            Messages.Message($"AIC Hub: Ejected {toEject}× {target.LabelCap} onto the map.", this, MessageTypeDefOf.PositiveEvent, historical: false);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Values.Look(ref hubName, "hubName"); 

            Scribe_Collections.Look(ref inventory, "aicHub_inventory", LookMode.Def, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && inventory == null) inventory = new Dictionary<ThingDef, int>();

            Scribe_Collections.Look(ref absorptionEnabled, "aicHub_absorptionEnabled", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && absorptionEnabled == null) absorptionEnabled = new HashSet<ThingDef>();
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(base.GetInspectString());
            lock (inventoryLock)
            {
                sb.AppendLine();
                GetInventorySummary(out int types, out int units);
                sb.Append($"AIC Hub Storage: {units} units stored ({types} types, max 6000 per type)");
            }
            return sb.ToString().TrimEnd();
        }
    }

    public class Dialog_RenameHub : Window
    {
        private Building_AICHub hub;
        private string curName;

        public override Vector2 InitialSize => new Vector2(280f, 175f);

        public Dialog_RenameHub(Building_AICHub hub)
        {
            this.hub = hub;
            this.curName = hub.HubName;
            this.doCloseX = true;
            this.forcePause = true;
            this.closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "Enter new HUB name:");
            
            curName = Widgets.TextField(new Rect(0f, 35f, inRect.width, 35f), curName);

            if (Widgets.ButtonText(new Rect(0f, 85f, inRect.width, 35f), "OK"))
            {
                if (!string.IsNullOrWhiteSpace(curName))
                {
                    hub.SetCustomName(curName.Trim());
                }
                Close();
            }
        }
    }
}