using Verse;

namespace RimWorld;

public class CompProperties_CompStaticElectricity : CompProperties
{
    public readonly int maxChargeDays = 2;
    public readonly int powerPerDay = 200;

    public CompProperties_CompStaticElectricity()
    {
        compClass = typeof(CompStaticElectricity);
    }
}