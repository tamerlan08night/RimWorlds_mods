using System.Collections.Generic;
using Verse;

namespace AKEndfield
{
    /// Властивості, що налаштовуються за допомогою XML, для HediffComp_OripathyProgression.
    /// Розмістіть це в розділі <comps> вашого HediffDef.
    ///
    /// Усі числові пороги, тривалості та посилання на елементи можна налаштувати в XML
    /// без зміни коду C#.
    public class HediffCompProperties_OripathyProgression : HediffCompProperties
    {
        // ── Пороги тяжкості стадії (0,0 – 1,0) ───────────────────────────────
        // Кожне значення – це мінімальна тяжкість, за якої ця стадія стає активною.
        // Вони повинні відповідати значенням <minSeverity> у вашому HediffDef <stages>.
        // Відображення за замовчуванням: 15-денна хвороба, кожна одиниця = 1/15 ≈ 0,0667 на день.
        public float stage1MinSeverity = 0.000f;   // Day  1  
        public float stage2MinSeverity = 0.267f;   // Day  5
        public float stage3MinSeverity = 0.467f;   // Day  7
        public float stage4MinSeverity = 0.667f;   // Day 10
        public float stage5MinSeverity = 0.867f;   // Day 13

        // ── Етап 3: Кристалізація органів ────────────────────────────────────────
        // HediffDef, який застосовується до випадково вибраних органів під час переходу на Етап 3.
        // Визначити простий hediff у XML (наприклад, Oripathy_OrganCrystallisation), який
        // застосовує штраф за ефективність -10% до будь-якої частини, на яку він потрапляє.
        public HediffDef organDamageHediffDef = null;

        // Кількість органів для кристалізації. За замовчуванням: 2.
        public int organDamageCount = 2;

        // Назви частин тіла органів, які потрібно виключити за межі жорстко закодованого мозку / серця.
        // Приклад: <additionalExcludedOrgans><li>Хребет</li></additionalExcludedOrgans>
        public List<string> additionalExcludedOrgans = new List<string>();

        // ── Етап 4: Випадання ресурсів Originium 
        // ThingDef предмета, що випадає періодично (наприклад, OE_RawOriginium).
        public ThingDef resourceDropDef = null;
        
        public int resourceDropAmount = 2;

        // Тики між кожним падінням. 30000 = 12 години в грі (0.5 дні).
        public int resourceDropIntervalTicks = 30000;

        //  Етап 5: Фатальний зворотний відлік 
        // Скільки тиків після переходу на Етап 5 до смерті пішака.
        // За замовчуванням: 120000 тиків = 2 ігрові дні (охоплює дні 13–15).
        // Примітка: таймер смерті ПРИЗУПИНЕНИЙ, поки IsFrozen має значення true.
        public int stage5FatalDurationTicks = 120000;

        // Резервний список стабілізаторів 
        // Альтернатива OripathyStabilizerExtension.
        // Будь-який ThingDef, перелічений тут, вважається дійсним стабілізатором, навіть без
        // розширення. Використовує defaultFreezeDurationTicks як тривалість.
        // Надавати перевагу OripathyStabilizerExtension для нових елементів; використовуйте це для сумісності
        // з існуючими ThingDef, які ви не можете редагувати.
        public List<ThingDef> validStabilizers = new List<ThingDef>();

        // Тривалість заморожування (тики) застосовується до елементів у validStabilizers, які НЕ мають
        // OripathyStabilizerExtension. 30000 = 0.5 день.
        public int defaultFreezeDurationTicks = 30000;

        public HediffCompProperties_OripathyProgression()
        {
            compClass = typeof(HediffComp_OripathyProgression);
        }
    }
}
