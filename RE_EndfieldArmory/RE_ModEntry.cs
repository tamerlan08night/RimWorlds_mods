using HarmonyLib;
using Verse;

namespace AKE.endfield
{
    [StaticConstructorOnStartup]
    internal static class RE_WeaponSkillScalingMod
    {
        static RE_WeaponSkillScalingMod()
        {
            var harmony = new Harmony("RE_EndfieldArmory.WeaponSkillScaling");
            harmony.PatchAll();
            Log.Message("[RE_EndfieldArmory] Weapon Skill Scaling System initialised.");
        }
    }
}
