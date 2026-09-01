using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RE_EndfieldArmory
{
    public class RE_SkillScalingEntry
    {
        public SkillDef skill;
        public int minSkill = 0;
        public HediffDef requiredHediff;       // Ефект на власникові
        public HediffDef requiredTargetHediff; // Ефект на цілі (ворогу)

        public float damageMultiplier = 1f;
        public float extraDamageBase = 0f;
        public float armorPenetrationMultiplier = 1f;
        public float meleeParryChanceMultiplier = 1f;
    }

    public class CompProperties_WeaponSkillScaling : CompProperties
    {
        public List<RE_SkillScalingEntry> scalings = new List<RE_SkillScalingEntry>();
        public CompProperties_WeaponSkillScaling() => compClass = typeof(CompWeaponSkillScaling);
    }

    public class CompWeaponSkillScaling : ThingComp
    {
        public CompProperties_WeaponSkillScaling Props => (CompProperties_WeaponSkillScaling)props;

        private bool IsActive(Pawn pawn, RE_SkillScalingEntry entry, Pawn target = null)
        {
            if (pawn == null) return false;

            if (entry.skill != null)
            {
                var skillRecord = pawn.skills.GetSkill(entry.skill);
                if (skillRecord == null || skillRecord.Level <= entry.minSkill) return false;
            }

            if (entry.requiredHediff != null && !pawn.health.hediffSet.HasHediff(entry.requiredHediff)) return false;

            if (entry.requiredTargetHediff != null)
            {
                if (target == null || !target.health.hediffSet.HasHediff(entry.requiredTargetHediff)) return false;
            }

            return true;
        }

        // 1. Метод для шкоди (з підтримкою Physic Arts та адитивною математикою)
        public float GetDamageMultiplier(Pawn p, Pawn target = null)
        {
            float totalMultiplier = 1f;
            foreach (var s in Props.scalings)
            {
                if (IsActive(p, s, target))
                {
                    // Додаємо бонус до загального множника
                    totalMultiplier += (s.damageMultiplier - 1f);
                }
            }
            return totalMultiplier;
        }

        public float GetArmorPenetrationMultiplier(Pawn p, Pawn target = null)
        {
            float totalMultiplier = 1f;
            foreach (var s in Props.scalings)
            {
                if (IsActive(p, s, target))
                {
                    totalMultiplier += (s.armorPenetrationMultiplier - 1f);
                }
            }
            return totalMultiplier;
        }

        // 3. ПОВЕРНУТИЙ МЕТОД: Шанс парирування (виправляє помилку CS1061)
        public float GetParryChanceMultiplier(Pawn p)
        {
            float bonus = 0f;
            foreach (var s in Props.scalings)
            {
                if (IsActive(p, s))
                    bonus += (s.meleeParryChanceMultiplier - 1f);
            }
            return 1f + bonus;
        }
    }
}