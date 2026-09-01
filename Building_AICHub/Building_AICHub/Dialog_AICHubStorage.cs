using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AKEndfield
{
    public class Dialog_AICHubStorage : Window
    {
        private readonly Building_AICHub hub;
        private string searchString = "";
        private Vector2 scrollPosition = Vector2.zero;

        private static List<ThingDef> allDefsCache;
        private List<ThingDef> filteredDefs = new List<ThingDef>();
        private bool isDirty = true;

        private const float RowHeight = 38f;
        private const float Padding = 4f;

        public override Vector2 InitialSize => new Vector2(750f, 650f);

        public Dialog_AICHubStorage(Building_AICHub hub)
        {
            this.hub = hub;
            this.doCloseX = true;
            this.closeOnAccept = false;
            this.closeOnCancel = false;
            this.absorbInputAroundWindow = false;

            if (allDefsCache == null)
            {
                allDefsCache = new List<ThingDef>();
                // Виправлено: Синтаксис бази даних
                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
                {
                    if (hub.IsAcceptableResource(def))
                    {
                        allDefsCache.Add(def);
                    }
                }

                allDefsCache.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase));
            }

            isDirty = true;
        }

        private void RebuildFilteredList()
        {
            filteredDefs.Clear();
            string cleanSearch = searchString.Trim();

            foreach (ThingDef def in allDefsCache)
            {
                if (cleanSearch.Length == 0 ||
                    (def.label != null && def.label.IndexOf(cleanSearch, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    def.defName.IndexOf(cleanSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    filteredDefs.Add(def);
                }
            }

            isDirty = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (isDirty)
            {
                RebuildFilteredList();
            }

            hub.GetInventorySummary(out int activeTypes, out int totalUnits);

            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 32f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "AIC LOGISTICS CONTROL TERMINAL");

            Rect statsRect = new Rect(inRect.x, titleRect.yMax, inRect.width, 22f);
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(statsRect,
                $"Connected Assets: {activeTypes} item categories indexed. Total volume: {totalUnits:N0} units digitized.");
            ResetText();

            Rect searchRect = new Rect(inRect.x, statsRect.yMax + 8f, inRect.width - 80f, 30f);
            string newSearch = Widgets.TextField(searchRect, searchString);
            if (newSearch != searchString)
            {
                searchString = newSearch;
                isDirty = true;
            }

            Rect clearRect = new Rect(searchRect.xMax + 6f, searchRect.y, 74f, 30f);
            if (Widgets.ButtonText(clearRect, "Clear") && searchString.Length > 0)
            {
                searchString = "";
                isDirty = true;
            }

            Rect headersRect = new Rect(inRect.x, searchRect.yMax + 12f, inRect.width, 20f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);

            Widgets.Label(new Rect(headersRect.x + 40f, headersRect.y, 258f, headersRect.height), "RESOURCE TYPE");
            Widgets.Label(new Rect(headersRect.x + 298f, headersRect.y, 114f, headersRect.height), "IN NET-STOCK");
            Widgets.Label(new Rect(headersRect.x + 412f, headersRect.y, 72f, headersRect.height), "ACTION");
            Widgets.Label(new Rect(headersRect.x + 484f, headersRect.y, 200f, headersRect.height),
                "AUTO-ABSORPTION FILTER");
            ResetText();

            Rect scrollOutRect = new Rect(inRect.x, headersRect.yMax + 4f, inRect.width,
                inRect.height - headersRect.yMax - 4f - 56f);
            Rect scrollViewRect = new Rect(0f, 0f, scrollOutRect.width - 16f, filteredDefs.Count * RowHeight);

            Widgets.BeginScrollView(scrollOutRect, ref scrollPosition, scrollViewRect);
            float currentY = 0f;

            for (int i = 0; i < filteredDefs.Count; i++)
            {
                ThingDef def = filteredDefs[i];

                if (currentY + RowHeight >= scrollPosition.y && currentY <= scrollPosition.y + scrollOutRect.height)
                {
                    Rect rowRect = new Rect(0f, currentY, scrollViewRect.width, RowHeight);
                    if (i % 2 == 1)
                    {
                        Widgets.DrawLightHighlight(rowRect);
                    }

                    Widgets.DrawHighlightIfMouseover(rowRect);

                    Rect iconRect = new Rect(rowRect.x + Padding, rowRect.y + (RowHeight - 30f) / 2f, 30f, 30f);
                    Widgets.ThingIcon(iconRect, def);

                    Rect labelRect = new Rect(iconRect.xMax + 6f, rowRect.y, 258f, RowHeight);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    string displayName = def.label != null ? def.label.CapitalizeFirst() : def.defName;
                    Widgets.Label(labelRect, displayName);

                    int stockCount = hub.GetStoredCount(def);
                    Rect stockRect = new Rect(labelRect.xMax, rowRect.y, 114f, RowHeight);
                    if (stockCount > 0)
                    {
                        GUI.color = Color.cyan;
                        Widgets.Label(stockRect, stockCount.ToString("N0"));
                        GUI.color = Color.white;
                    }
                    else
                    {
                        GUI.color = Color.gray;
                        Widgets.Label(stockRect, "0");
                        GUI.color = Color.white;
                    }

                    Rect ejectBtnRect = new Rect(stockRect.xMax, rowRect.y + (RowHeight - 24f) / 2f, 50f, 24f);
                    if (stockCount > 0)
                    {
                        if (Widgets.ButtonText(ejectBtnRect, "Eject"))
                        {
                            OpenEjectDialog(def, stockCount);
                        }
                    }
                    else
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.3f);
                        Widgets.ButtonText(ejectBtnRect, "Eject", false, false, false);
                        GUI.color = Color.white;
                    }

                    Rect toggleRect = new Rect(ejectBtnRect.xMax + 20f, rowRect.y + (RowHeight - 24f) / 2f, 180f, 24f);
                    bool isEnabled = hub.AbsorptionEnabled.Contains(def);
                    string toggleLabel = isEnabled ? "🟩 ENABLED (Absorb)" : "🟥 DISABLED (Ignore)";

                    if (Widgets.ButtonText(toggleRect, toggleLabel))
                    {
                        if (isEnabled)
                        {
                            hub.AbsorptionEnabled.Remove(def);
                        }
                        else
                        {
                            hub.AbsorptionEnabled.Add(def);
                        }
                    }

                    Text.Anchor = TextAnchor.UpperLeft;
                }

                currentY += RowHeight;
            }

            Widgets.EndScrollView();

            Rect bottomBtnRect = new Rect(inRect.x, inRect.height - 48f, inRect.width, 44f);
            if (Widgets.ButtonText(bottomBtnRect, "⚡ RUN PROXIMITY INTEGRATION SYSTEM [+] ⚡"))
            {
                TriggerProximityAbsorption();
            }
        }

        private void TriggerProximityAbsorption()
        {
            Map map = hub.Map;
            if (map == null) return;

            int totalAbsorbed = 0;
            int categoriesCount = 0;
            var cellRadius = 18;

            lock (hub.InventoryLock)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(hub.Position, cellRadius, true))
                {
                    if (!cell.InBounds(map)) continue;

                    List<Thing> thingsInCell = map.thingGrid.ThingsListAt(cell);
                    for (int i = thingsInCell.Count - 1; i >= 0; i--)
                    {
                        Thing thing = thingsInCell[i];
                        if (thing.def.category == ThingCategory.Item && !thing.IsForbidden(Faction.OfPlayer))
                        {
                            if (hub.IsAcceptableResource(thing.def) && hub.AbsorptionEnabled.Contains(thing.def))
                            {
                                int initialCount = thing.stackCount;

                                // Отримуємо КІЛЬКІСТЬ РЕАЛЬНО ЗАБРАНИХ одиниць
                                int absorbed = hub.InjectResource(thing.def, initialCount);

                                if (absorbed > 0)
                                {
                                    totalAbsorbed += absorbed;
                                    categoriesCount++;

                                    if (absorbed >= initialCount)
                                    {
                                        thing.Destroy(DestroyMode.Vanish);
                                    }
                                    else
                                    {
                                        // Якщо забрано тільки частину — віднімаємо від стаку, залишаючи решту на землі
                                        thing.stackCount -= absorbed;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (totalAbsorbed > 0)
            {
                Messages.Message(
                    $"AIC Integration successful: Absorbed {totalAbsorbed:N0} units across multiple networks.",
                    MessageTypeDefOf.TaskCompletion, false);
            }
            else
            {
                Messages.Message(
                    "Integration standby: No active/allowed item stacks found within scanning radius or storage is full.",
                    MessageTypeDefOf.RejectInput, false);
            }
        }

        private void OpenEjectDialog(ThingDef def, int maxStock)
        {
            ThingDef capDef = def;
            int capMax = maxStock;

            Find.WindowStack.Add(new Dialog_Slider(
                $"Eject {capDef.label} — select quantity",
                1,
                capMax,
                delegate(int amount) { this.hub.DispenseResource(capDef, amount); },
                Mathf.Min(capMax, 50)
            ));
        }

        private static void ResetText()
        {
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}