using System.Linq;
using Verse;
using Verse.Sound;

namespace RimWorld;

public class CompStaticElectricity : CompHasGatherableBodyResource
{
    private const int frameskip = 150;
    private readonly int frameskip_offset = Rand.Range(0, frameskip);

    private readonly WeatherDef[] weathers =
    {
        WeatherDef.Named("Rain"), WeatherDef.Named("FoggyRain"),
        WeatherDef.Named("RainyThunderstorm"), WeatherDef.Named("DryThunderstorm")
    };

    protected override int GatherResourcesIntervalDays => Props.maxChargeDays;
    protected override int ResourceAmount => Props.powerPerDay;
    protected override ThingDef ResourceDef => null;
    protected override string SaveKey => "powerHarvestableGrowth";
    protected override bool Active => (parent as Pawn)?.Awake() ?? false;
    public CompProperties_CompStaticElectricity Props => props as CompProperties_CompStaticElectricity;

    public override void CompTick()
    {
        base.CompTick();

        if (parent.DestroyedOrNull() || !parent.Spawned || ((Pawn)parent).Dead)
        {
            return;
        }

        if ((GenTicks.TicksAbs + frameskip_offset) % frameskip != 0)
        {
            return;
        }

        if (fullness == 0)
        {
            return;
        }

        var battery = GenClosest.ClosestThing_Global(
            parent.Position,
            parent.Map.listerThings.ThingsMatching(ThingRequest.ForDef(ThingDefOf.Battery)),
            2f + (5f * fullness),
            t => !t.Destroyed);

        if (battery != null)
        {
            battery.TryGetComp<CompPowerBattery>()
                ?.AddEnergy(fullness * ResourceAmount * GatherResourcesIntervalDays);

            SoundDef.Named("PowerOnSmall").PlayOneShot(new TargetInfo(parent.Position, parent.Map));
            var loc = parent.Position.ToVector3Shifted();

            FleckMaker.ThrowLightningGlow(loc, parent.Map, 0.5f + fullness);
            if (fullness > 0.3)
            {
                FleckMaker.ThrowMicroSparks(loc, parent.Map);
            }

            fullness = 0;
            (parent as RimStarvePawn)?.UpdateActiveGraphic();
        }

        if (Fullness != 1f || !Rand.Chance(0.01f) || !weathers.Contains(parent.Map.weatherManager.curWeather))
        {
            return;
        }

        new WeatherEvent_LightningStrike(parent.Map, parent.Position).FireEvent();
        fullness = 0;
        (parent as RimStarvePawn)?.UpdateActiveGraphic();
    }

    private string ElectricityString()
    {
        if (!Active)
        {
            return null;
        }

        if (fullness > 0.9)
        {
            return "Full charge";
        }

        if (fullness > 0.75)
        {
            return "High charge";
        }

        return fullness > 0.3 ? "Medium charge" : "Low charge";
    }

    public override string CompInspectStringExtra()
    {
        if (!Active)
        {
            return null;
        }

        return "Static electricity: " + ElectricityString();
    }
}