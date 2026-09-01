using Verse;

namespace AKEndfield
{
    /// Підказка
    /// Додай це розширення DefModExtension до будь-якого ThingDef, щоб зареєструвати його як дійсний стабілізатор Oripathy.
    /// Це основний, незалежний спосіб визначення елементів стабілізатора, змінювати код не потрібно.
    ///
    /// Приклад XML (Folk Method - коротка тривалість):
    ///   <modExtensions>
    ///     <li Class="YourModNamespace.OripathyStabilizerExtension">
    ///       <stabilizeDurationTicks>60000</stabilizeDurationTicks>
    ///       <stabilizerLabel>Folk Remedy</stabilizerLabel>
    ///       <instantSeverityReduction>0.0</instantSeverityReduction>
    ///     </li>
    ///   </modExtensions>
    ///
    /// XML example (Factory Method - тривала тривалість):
    ///   <modExtensions>
    ///     <li Class="YourModNamespace.OripathyStabilizerExtension">
    ///       <stabilizeDurationTicks>300000</stabilizeDurationTicks>
    ///       <stabilizerLabel>Lungmen Oripathy Inhibitor</stabilizerLabel>
    ///       <instantSeverityReduction>0.05</instantSeverityReduction>
    ///     </li>
    ///   </modExtensions>
    public class OripathyStabilizerExtension : DefModExtension
    {
        // ── Тривалість ─────────────────────────────────────────────────────────
        // Як довго (у тиках) стабілізатор утримує прогрес Орипатії замороженим.
        // 60000 ticks = 1 ігровий день.
        // Folk method example   : 60000  (1 день)
        // Factory method example: 300000 (5 днів)
        public int stabilizeDurationTicks = 60000;

        // ── Миттєвий ефект────────────────────────────────────────────────────
        // Миттєво зменшує тяжкість орипатії на цю величину після застосування.
        // НЕ лікує - лише пом’якшує поточну стадію. 0 = миттєвого ефекту немає.
        public float instantSeverityReduction = 0f;
        public string stabilizerLabel = "Stabilizer";
    }
}
