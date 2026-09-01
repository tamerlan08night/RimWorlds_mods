using System.Collections.Generic;
using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;
using Verse.Sound;

namespace RE_EndfieldArmory
{
    [StaticConstructorOnStartup]
    public static class NeverRestModInitializer
    {
        static NeverRestModInitializer()
        {
            var harmony = new Harmony("com.re.endfieldarmory.neverrest");
            harmony.PatchAll();
            Log.Message("[RE_Endfield Armory] Модуль Never Rest та візуальні ефекти успішно активовано!");
        }
    }

    // --- 1. ПАТЧ ДЛЯ ЛОГІКИ УДАРІВ ТА СТАКИ ВЛАСНИКА ---
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "Apply")]
    public static class Patch_NeverRest_Logic
    {
        private static Dictionary<int, int> lastStackTickPerPawn = new Dictionary<int, int>();

        [HarmonyPrefix]
        public static void Prefix(ref DamageInfo dinfo, Thing thing)
        {
            if (dinfo.Instigator is Pawn attacker)
            {
                var primaryWeapon = attacker.equipment?.Primary;
                if (primaryWeapon?.def?.defName == "RE_NeverRest")
                {
                    var hediffSet = attacker.health.hediffSet;
                    Hediff wielderBuff = hediffSet.GetFirstHediffOfDef(HediffDef.Named("RE_NeverRestWielderBuff"));
                    
                    if (wielderBuff != null && wielderBuff.Severity >= 3f)
                    {
                        if (attacker.skills != null && attacker.skills.GetSkill(SkillDefOf.Melee).Level > 13)
                        {
                            dinfo.SetAmount(dinfo.Amount * 1.05f);
                        }
                        return;
                    }
                }

                if (attacker.health.hediffSet.HasHediff(HediffDef.Named("RE_NeverRestAllianceBuff")))
                {
                    dinfo.SetAmount(dinfo.Amount * 1.025f);
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix(DamageInfo dinfo, Thing thing)
        {
            if (dinfo.Instigator is Pawn attacker && dinfo.Amount > 0)
            {
                var primaryWeapon = attacker.equipment?.Primary;
                if (primaryWeapon?.def?.defName == "RE_NeverRest")
                {
                    HediffDef wielderDef = HediffDef.Named("RE_NeverRestWielderBuff");
                    Hediff wielderBuff = attacker.health.hediffSet.GetFirstHediffOfDef(wielderDef);

                    int currentTick = Find.TickManager.TicksGame;
                    lastStackTickPerPawn.TryGetValue(attacker.thingIDNumber, out int lastHitTick);
                    bool alreadyGainedStackThisTick = (lastHitTick == currentTick);

                    if (!alreadyGainedStackThisTick)
                    {
                        float oldSeverity = wielderBuff != null ? wielderBuff.Severity : 0f;

                        if (wielderBuff == null)
                        {
                            wielderBuff = attacker.health.AddHediff(wielderDef);
                            wielderBuff.Severity = 1f;
                        }
                        else
                        {
                            wielderBuff.Severity = Mathf.Min(wielderBuff.Severity + 1f, 3f);
                        }

                        // ТРИГЕР: Перехід на 3-тю стадію (Peak)
                        if (oldSeverity < 3f && wielderBuff.Severity >= 3f && attacker.Map != null)
                        {
                            SoundDef sound = SoundDef.Named("RE_CommanderSound");
                            sound?.PlayOneShot(SoundInfo.InMap(attacker, MaintenanceType.None));

                            FleckDef fleck = DefDatabase<FleckDef>.GetNamedSilentFail("RE_NeverRestPeakPulse");
                            if (fleck != null)
                            {
                                FleckMaker.Static(attacker.Position, attacker.Map, fleck, 1f);
                            }
                        }

                        lastStackTickPerPawn[attacker.thingIDNumber] = currentTick;
                    }

                    // Скидання таймера бафа мечника
                    if (wielderBuff != null)
                    {
                        var comp = wielderBuff.TryGetComp<HediffComp_Disappears>();
                        if (comp != null) comp.ticksToDisappear = 1800; 
                    }
                }
            }
        }
    }

    // --- 2. ПОСТІЙНА АУРА ДЛЯ СОЮЗНИКІВ ---
    public class HediffCompProperties_NeverRestAura : HediffCompProperties
    {
        public float range = 8f;
        public HediffCompProperties_NeverRestAura()
        {
            this.compClass = typeof(HediffComp_NeverRestAura);
        }
    }

    public class HediffComp_NeverRestAura : HediffComp
    {
        public HediffCompProperties_NeverRestAura Props => (HediffCompProperties_NeverRestAura)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // Перевірка кожні 30 тіків (0.5 сек) на 3-й стадії
            if (Pawn.IsHashIntervalTick(30) && parent.Severity >= 3f && Pawn.Map != null && !Pawn.Dead)
            {
                HediffDef allyDef = HediffDef.Named("RE_NeverRestAllianceBuff");

                foreach (IntVec3 cell in GenRadial.RadialCellsAround(Pawn.Position, Props.range, true))
                {
                    if (!cell.InBounds(Pawn.Map)) continue;

                    List<Thing> things = cell.GetThingList(Pawn.Map);
                    for (int i = 0; i < things.Count; i++)
                    {
                        if (things[i] is Pawn ally && ally.Faction != null && ally.Faction == Pawn.Faction && ally != Pawn && !ally.Dead)
                        {
                            Hediff allyBuff = ally.health.hediffSet.GetFirstHediffOfDef(allyDef);
                            if (allyBuff == null)
                            {
                                allyBuff = ally.health.AddHediff(allyDef);
                            }

                            var allyComp = allyBuff.TryGetComp<HediffComp_Disappears>();
                            if (allyComp != null)
                            {
                                allyComp.ticksToDisappear = 1800; // 30 секунд
                            }
                        }
                    }
                }
            }
        }
    }

    // --- 3. ВІДМАЛЬОВУВАННЯ ІКОНОК (OVERLAY) ---
    public class HediffCompProperties_DrawOverlayIcon : HediffCompProperties
    {
        public string iconPath;
        public float iconSize = 0.7f;
        public float altitudeOffset = 1.3f;
        public bool onlyAtPeak = false;

        public HediffCompProperties_DrawOverlayIcon()
        {
            this.compClass = typeof(HediffComp_DrawOverlayIcon);
        }
    }

    [StaticConstructorOnStartup]
    public class HediffComp_DrawOverlayIcon : HediffComp
    {
        private Material iconMat;
        public HediffCompProperties_DrawOverlayIcon Props => (HediffCompProperties_DrawOverlayIcon)this.props;

        public void DrawOverlay(Vector3 drawPos)
        {
            if (Props.onlyAtPeak && this.parent.Severity < 3f) return;

            if (iconMat == null && !string.IsNullOrEmpty(Props.iconPath))
            {
                iconMat = MaterialPool.MatFrom(Props.iconPath, ShaderDatabase.MetaOverlay);
            }

            if (iconMat != null)
            {
                Vector3 pos = drawPos;
                pos.y = AltitudeLayer.MetaOverlays.AltitudeFor(); 
                pos.z += Props.altitudeOffset; 

                Matrix4x4 matrix = default(Matrix4x4);
                matrix.SetTRS(pos, Quaternion.identity, new Vector3(Props.iconSize, 1f, Props.iconSize));
                Graphics.DrawMesh(MeshPool.plane10, matrix, iconMat, 0);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "DrawAt")]
    public static class Patch_Pawn_DrawAt
    {
        public static void Postfix(Pawn __instance, Vector3 drawLoc, bool flip)
        {
            if (__instance.health?.hediffSet?.hediffs == null) return;

            foreach (var hediff in __instance.health.hediffSet.hediffs)
            {
                var comp = hediff.TryGetComp<HediffComp_DrawOverlayIcon>();
                if (comp != null)
                {
                    comp.DrawOverlay(drawLoc);
                }
            }
        }
    }
}