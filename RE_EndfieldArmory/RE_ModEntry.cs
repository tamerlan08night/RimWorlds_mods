using HarmonyLib;
using Verse;

namespace RE_EndfieldArmory
{
    // =========================================================================
    //  Mod entry point.
    //  RimWorld discovers this class via the [StaticConstructorOnStartup]
    //  attribute and calls the static constructor once on game launch.
    // =========================================================================
    [StaticConstructorOnStartup]
    internal static class RE_WeaponSkillScalingMod
    {
        static RE_WeaponSkillScalingMod()
        {
            var harmony = new Harmony("RE_EndfieldArmory.WeaponSkillScaling");
            harmony.PatchAll(); // picks up all [HarmonyPatch] classes in the assembly
            Log.Message("[RE_EndfieldArmory] Weapon Skill Scaling System initialised.");
        }
    }
}
