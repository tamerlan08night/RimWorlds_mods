using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AKE.endfield
{
    public class Building_ICFactoryBase : Building_WorkTable
    {
        private Building_AICHub linkedHub;
        private CompPowerTrader powerComp;

        private int hubCheckTicks = 0;
        private float workLeft = 0f;

        private Bill_Production currentBill;
        private List<ThingDef> reservedDefs = new List<ThingDef>();
        private List<int> reservedCounts = new List<int>();

        // Прапор переповнення сховища для відображення у вікні інформації
        private bool isStorageFullWarning = false;

        // Одинарний Sustainer для відтворення звуку рецепту
        private Sustainer sustainer;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();

            if (!def.defName.StartsWith("IC"))
            {
                Log.Warning($"[AKEndfield] Attention: Machine {def.defName} does not have the required 'IC' prefix!");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref linkedHub, "linkedHub");
            Scribe_Values.Look(ref workLeft, "workLeft", 0f);
            Scribe_References.Look(ref currentBill, "currentBill");
            Scribe_Collections.Look(ref reservedDefs, "reservedDefs", LookMode.Def);
            Scribe_Collections.Look(ref reservedCounts, "reservedCounts", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (reservedDefs == null) reservedDefs = new List<ThingDef>();
                if (reservedCounts == null) reservedCounts = new List<int>();
            }
        }

        protected override void Tick()
        {
            base.Tick();

            // Пошук ХАБу
            if (linkedHub == null || linkedHub.Destroyed)
            {
                if (workLeft > 0 || reservedDefs.Count > 0)
                {
                    EjectReservedResources();
                }
                if (linkedHub != null)
                {
                    var hubPower = linkedHub.GetComp<CompPowerTrader>();
                    if (hubPower != null && !hubPower.PowerOn)
                    {
                        StopSound(); // Зупиняємо звук якщо ХАБ знеструмлено
                        return;
                    }
                }
                hubCheckTicks--;
                if (hubCheckTicks <= 0)
                {
                    FindHub();
                    hubCheckTicks = 300;
                }

                if (linkedHub == null)
                {
                    StopSound(); // Зупиняємо звук якщо немає ХАБу
                    return;
                }
            }

            // Перевірка енергії
            if (powerComp != null && !powerComp.PowerOn)
            {
                StopSound(); // Зупиняємо звук якщо станок знеструмлено
                return;
            }

            // Автоматичний крафт
            if (currentBill == null)
            {
                TryStartNextBill();
                if (currentBill == null)
                {
                    StopSound(); // Немає роботи то станок повністю мовчить
                }
            }
            else
            {
                // Перевіряємо, чи в ХАБі все ще є місце для готової продукції під час виготовлення
                if (!CanAcceptProducts(currentBill))
                {
                    isStorageFullWarning = true;
                    StopSound(); // Зупиняємо звук, оскільки крафт призупинено
                    return; // Чекаємо звільнення місця у ХАБі
                }

                isStorageFullWarning = false;
                workLeft -= 1f;

                // Граємо звук ЛИШЕ з рецепту і ЛИШЕ під час відліку workLeft
                MaintainRecipeSound();

                if (workLeft <= 0f)
                {
                    CompleteBill();
                }
            }
        }

        /// <summary>
        /// Перевіряє, чи може підключений ХАБ прийняти всі продукти рецепта.
        /// </summary>
        private bool CanAcceptProducts(Bill_Production bill)
        {
            if (linkedHub == null || bill?.recipe?.products == null) return false;

            foreach (ThingDefCountClass product in bill.recipe.products)
            {
                if (!linkedHub.CanAcceptResource(product.thingDef, product.count))
                {
                    return false;
                }
            }
            return true;
        }

        private void MaintainRecipeSound()
        {
            SoundDef recipeSound = currentBill?.recipe?.soundWorking;

            if (recipeSound == null)
            {
                StopSound();
                return;
            }

            if (sustainer == null || sustainer.Ended)
            {
                SoundInfo info = SoundInfo.InMap(this, MaintenanceType.PerTick);
                sustainer = recipeSound.TrySpawnSustainer(info);
            }

            sustainer?.Maintain();
        }

        private void StopSound()
        {
            if (sustainer != null && !sustainer.Ended)
            {
                sustainer.End();
            }
            sustainer = null;
        }

        private void FindHub()
        {
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
        }

        private void TryStartNextBill()
        {
            isStorageFullWarning = false;

            foreach (Bill bill in billStack)
            {
                if (!(bill is Bill_Production pBill)) continue;
                if (!pBill.ShouldDoNow()) continue;

                // 1. Перевіряємо, чи є місце в ХАБі під результат крафту
                if (!CanAcceptProducts(pBill))
                {
                    isStorageFullWarning = true;
                    continue; // Пропускаємо цей рецепт
                }

                // 2. Забираємо інгредієнти тільки якщо готову продукцію буде куди покласти
                if (TryReserveAndConsumeIngredients(pBill))
                {
                    currentBill = pBill;
                    workLeft = pBill.recipe.workAmount;
                    isStorageFullWarning = false;
                    break;
                }
            }
        }

        private bool TryReserveAndConsumeIngredients(Bill_Production bill)
        {
            // 1. Перевіряємо, чи вимагає рецепт нафту з Rimefeller
            var fluidExt = bill.recipe.GetModExtension<RecipeExtension_RimefellerFluid>();
            if (fluidExt != null && fluidExt.oilAmount > 0)
            {
                // Перевірка: чи є достатньо нафти у мережі труб
                if (!RimefellerHelper.HasEnoughOil(this, fluidExt.oilAmount))
                {
                    return false;
                }
            }

            // 2. Перевірка твердих інгредієнтів у ХАБі (ваш існуючий код)
            List<ThingDef> tempDefs = new List<ThingDef>();
            List<int> tempCounts = new List<int>();

            foreach (IngredientCount ingredient in bill.recipe.ingredients)
            {
                ThingDef bestDef = null;
                int requiredAmount = (int)ingredient.GetBaseCount();

                foreach (ThingDef allowedDef in ingredient.filter.AllowedThingDefs)
                {
                    if (linkedHub.HasResources(allowedDef, requiredAmount))
                    {
                        bestDef = allowedDef;
                        break;
                    }
                }

                if (bestDef == null)
                {
                    return false; // Не вистачає твердих предметів у ХАБі
                }

                tempDefs.Add(bestDef);
                tempCounts.Add(requiredAmount);
            }

            // 3. Якщо всі перевірки пройдено — СДОИМО/СПИСУЄМО ресурси

            // Списання нафти з труб
            if (fluidExt != null && fluidExt.oilAmount > 0)
            {
                if (!RimefellerHelper.TryConsumeOil(this, fluidExt.oilAmount))
                {
                    return false; // Пролаг або раптово зникла нафта
                }
            }

            // Списання предметів з ХАБу
            for (int i = 0; i < tempDefs.Count; i++)
            {
                linkedHub.TryConsumeResources(tempDefs[i], tempCounts[i]);
            }

            return true; // Рецепт успішно розпочато!
        }

        private void CompleteBill()
        {
            StopSound();

            foreach (ThingDefCountClass product in currentBill.recipe.products)
            {
                linkedHub.InjectResource(product.thingDef, product.count);
            }

            reservedDefs.Clear();
            reservedCounts.Clear();

            currentBill.Notify_IterationCompleted(null, new List<Thing>());
            currentBill = null;
            workLeft = 0f;
            isStorageFullWarning = false;
        }

        private void EjectReservedResources()
        {
            StopSound();

            for (int i = 0; i < reservedDefs.Count; i++)
            {
                Thing droppedThing = ThingMaker.MakeThing(reservedDefs[i]);
                droppedThing.stackCount = reservedCounts[i];
                GenPlace.TryPlaceThing(droppedThing, Position, Map, ThingPlaceMode.Near);
            }

            reservedDefs.Clear();
            reservedCounts.Clear();
            currentBill = null;
            workLeft = 0f;
            isStorageFullWarning = false;

            Messages.Message("Machine emergency stop: HUB lost, resources reset.", this, MessageTypeDefOf.NegativeEvent, false);
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (linkedHub != null)
            {
                text += "\nAIC Hub: Connected";

                if (isStorageFullWarning)
                {
                    text += "\nStatus: Paused (HUB storage full)";
                }

                if (currentBill != null && currentBill.recipe != null)
                {
                    float percent = (currentBill.recipe.workAmount - workLeft) / currentBill.recipe.workAmount * 100f;
                    text += $"\nCraft: {currentBill.recipe.label} ({percent:F0}%)";
                }
            }
            else
            {
                text += "\nAIC Hub: Not found";
            }
            return text;
        }
    }
}