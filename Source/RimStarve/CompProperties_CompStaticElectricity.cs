using Verse;

namespace RimWorld
{
    public class CompProperties_CompStaticElectricity : CompProperties
    {
        public int maxChargeDays = 2;
        public int powerPerDay = 200;

        public CompProperties_CompStaticElectricity()
        {
            compClass = typeof(CompStaticElectricity);
        }
    }
}