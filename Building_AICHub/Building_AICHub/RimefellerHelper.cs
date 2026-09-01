using Verse;
using RimWorld;
using Rimefeller; // Тепер підсвічуватиметься після додавання DLL

namespace AKEndfield
{
    public static class RimefellerHelper
    {
        // ModsConfig.IsActive — стандартна перевірка RimWorld за PackageID
        public static bool HasRimefeller => ModsConfig.IsActive("Dubwise.Rimefeller");

        public static bool HasEnoughOil(ThingWithComps building, float amount)
        {
            if (!HasRimefeller) return false;

            var compPipe = building.GetComp<CompPipe>();
            if (compPipe == null || compPipe.pipeNet == null) return false;

            return compPipe.pipeNet.TotalOil >= amount;
        }

        public static bool TryConsumeOil(ThingWithComps building, float amount)
        {
            if (!HasRimefeller) return false;

            var compPipe = building.GetComp<CompPipe>();
            if (compPipe == null || compPipe.pipeNet == null) return false;

            // PullOil автоматично перевіряє суму та списує нафту з мережі
            return compPipe.pipeNet.PullOil(amount);
        }
    }
}