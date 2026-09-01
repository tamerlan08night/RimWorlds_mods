using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AKE.endfield
{
    [StaticConstructorOnStartup]
    public static class ArtsSystem
    {
        static ArtsSystem()
        {
            new Harmony("com.endfield.arts.system").PatchAll();
            Log.Message("<color=#00ff00>[Endfield Armory]</color> Arts System Initialized.");
        }
    }

    public class CompProperties_ArtsWeapon : CompProperties
    {
        public string appliesElement = "None";
        public float applyChance = 1.0f;
        public float bonusPerStage = 0f;
        public bool internalDamageOnFire = false;

        public CompProperties_ArtsWeapon() => compClass = typeof(CompArtsWeapon);
    }

    public class CompArtsWeapon : ThingComp
    {
        public CompProperties_ArtsWeapon Props => (CompProperties_ArtsWeapon)props;
    }

    public class Hediff_ArtsElement : HediffWithComps
    {
        public int expiryTick = -1;
        public const int DurationTicks = 1800;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            ResetTimer();
        }

        public void ResetTimer()
        {
            expiryTick = Find.TickManager.TicksGame + DurationTicks;
        }

        public override void Tick()
        {
            base.Tick();
            if (pawn == null || pawn.Dead) return;
            if (Find.TickManager.TicksGame >= expiryTick)
                pawn.health.RemoveHediff(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref expiryTick, "expiryTick", -1);
        }
    }
}
