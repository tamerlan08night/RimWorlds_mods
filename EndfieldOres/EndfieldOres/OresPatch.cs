using System.Collections.Generic;
using Verse;
using RimWorld;

namespace AKE.endfield
{
    [StaticConstructorOnStartup]
    public static class EndfieldOresPatch
    {
        private static readonly string[] OreDefNames =
        {
            "OE_OriginiumSurface",
            "OE_FeriumSurface",
            "OE_AmethystSurface"
        };

        static EndfieldOresPatch()
        {
            var preciousLump = GenStepDefOf.PreciousLump?.genStep as GenStep_PreciousLump;
            if (preciousLump == null)
            {
                Log.Warning("[AKEndfield] PreciousLump genStep is null. Cannot register ores.");
                return;
            }

            if (preciousLump.mineables == null)
            {
                preciousLump.mineables = new List<ThingDef>();
            }

            foreach (string defName in OreDefNames)
            {
                ThingDef oreDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (oreDef == null)
                {
                    Log.Warning($"[AKEndfield] ThingDef '{defName}' not found. Skipping.");
                    continue;
                }

                if (!preciousLump.mineables.Contains(oreDef))
                {
                    preciousLump.mineables.Add(oreDef);
                    Log.Message($"[AKEndfield] {defName} added to long-range mineral scanner.");
                }
            }
        }
    }
}
