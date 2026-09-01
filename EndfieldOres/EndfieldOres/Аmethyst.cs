using System.Collections.Generic;
using Verse;
using RimWorld;
using HarmonyLib;

namespace AKEndfield
{
    [StaticConstructorOnStartup]
    public static class AmethystPatch
    {
        static AmethystPatch()
        {
            var harmony = new Harmony("com.akendfield.amethystmpatch");

            // Ми патчимо не сам сканер, а саме те місце, де генерується список руд
            var original = AccessTools.Field(typeof(GenStep_PreciousLump), "mineables");

            // Додаємо свій деф у список, коли гра завантажується
            var originiumDef = DefDatabase<ThingDef>.GetNamed("OE_AmethystSurface", true);
            var mineables = ((GenStep_PreciousLump)GenStepDefOf.PreciousLump.genStep).mineables;

            if (!mineables.Contains(originiumDef))
            {
                mineables.Add(originiumDef);
            }
            // Безпечний пошук дефу (false захищає від вильоту, якщо дефу немає в XML)
            var currentOreDef = DefDatabase<ThingDef>.GetNamed("OE_AmethystSurface", false);
            var preciousLump = GenStepDefOf.PreciousLump?.genStep as GenStep_PreciousLump;

            if (preciousLump != null && currentOreDef != null)
            {
                // Якщо список у грі чомусь не ініціалізований — створюємо його
                if (preciousLump.mineables == null)
                {
                    preciousLump.mineables = new List<ThingDef>();
                }

                // Додаємо руду ТІЛЬКИ якщо її там ще немає
                if (!preciousLump.mineables.Contains(currentOreDef))
                {
                    preciousLump.mineables.Add(currentOreDef);
                    Log.Message($"[AKEndfield] {currentOreDef.defName} successfully added to long-range scanner.AMETHYST");
                }
            }
            else
            {
                Log.Warning("[AKEndfield] Failed to add ore to scanner: def or genStep is null.AMETHYST");
            }
        }
    }
}