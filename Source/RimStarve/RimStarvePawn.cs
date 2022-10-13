using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse.AI;

namespace Verse;

/// <summary>
///     Extends <see cref="Pawn" /> to provide additional graphic sets that are activated
///     based on a set of conditions.
/// </summary>
public class RimStarvePawn : Pawn
{
    /// <summary> Cache ModContentHolder for this mod </summary>
    private readonly ModContentHolder<Texture2D> contentHolder = LoadedModManager.RunningMods
        .FirstOrDefault(x => x.Name == "RimStarve")
        ?.GetContentHolder<Texture2D>();

    /// <summary> List of graphic replacements </summary>
    private readonly List<GraphicReplacement> replacements = new List<GraphicReplacement>();

    private CompHasGatherableBodyResource compElectricity;

    private CompHasGatherableBodyResource compShearable;

    private float lastTemperature = 20;

    /// <summary> Tracks the current life stage to see when it changed </summary>
    private PawnKindLifeStage lifeStageBefore;

    private bool IsSheared => compShearable?.Fullness < 0.1f;
    private bool IsEnergized => compElectricity?.Fullness > 0.9f;
    private bool IsFighting => CurJob?.def == JobDefOf.AttackMelee;
    private bool IsMoving => pather.MovingNow;
    private bool IsTamed => Faction == Faction.OfPlayer;
    private bool IsLyingDown => Downed || (jobs?.curDriver?.asleep ?? true) || !health.capacities.CanBeAwake;
    private bool IsCold => GetTemperature() <= 0;
    private bool IsHot => GetTemperature() >= 42;

    private float GetTemperature()
    {
        if (!Dead) // avoid null reference exception
        {
            GenTemperature.TryGetAirTemperatureAroundThing(this, out lastTemperature);
        }

        return lastTemperature;
    }

    private void SetupReplacements()
    {
        // cache the comps to avoid expensive calls every tick
        compShearable = compShearable ?? GetComp<CompShearable>();
        compElectricity = compElectricity ?? GetComp<CompStaticElectricity>();

        // load replacements for existing graphics based on the current life stage
        replacements.Clear();

        AddReplacement(
            LoadGraphic("summer_dead"),
            () => Dead && IsHot);

        AddReplacement(
            LoadGraphic("winter_dead"),
            () => Dead && IsCold);

        AddReplacement(
            LoadGraphic("dead"),
            () => Dead);

        AddReplacement(
            LoadGraphic("summer_sleep"),
            () => IsHot && IsLyingDown);

        AddReplacement(
            LoadGraphic("winter_sleep"),
            () => IsCold && IsLyingDown);

        AddReplacement(
            LoadGraphic("summer"),
            () => IsHot);

        AddReplacement(
            LoadGraphic("winter"),
            () => IsCold);

        AddReplacement(
            LoadGraphic("sheared_sleep"),
            () => IsTamed && IsSheared && IsLyingDown);

        AddReplacement(
            LoadGraphic("sheared"),
            () => IsTamed && IsSheared);

        AddReplacement(
            LoadGraphic("charged"),
            () => IsEnergized);

        AddReplacement(
            LoadGraphic("sleep"),
            () => IsLyingDown);

        AddReplacement( // default graphic if no previous match
            ageTracker.CurKindLifeStage.bodyGraphicData.Graphic,
            () => true);
    }

    /// <summary> Add a graphic replacement rule </summary>
    private void AddReplacement(Graphic graphic, Func<bool> condition)
    {
        if (graphic == null)
        {
            return; // skip if graphic could not be loaded
        }

        replacements.Add(new GraphicReplacement(graphic, condition));
    }

    /// <summary> Try to load the Graphic of appropriate type based on current life stage </summary>
    private Graphic LoadGraphic(string variant)
    {
        var path = ageTracker.CurKindLifeStage.bodyGraphicData.texPath + "_" + variant;
        if (Prefs.DevMode)
        {
            Log.Message($"path: {path}");
        }

        // determine graphic class type
        Type type = null;
        if (contentHolder.Get(path + "_south") != null)
        {
            type = typeof(Graphic_Multi);
        }
        else if (contentHolder.Get(path) != null)
        {
            type = typeof(Graphic_Single);
        }

        if (type == null)
        {
            return null; // texture not found, skip graphic
        }

        var data = new GraphicData();
        data.CopyFrom(ageTracker.CurKindLifeStage.bodyGraphicData);
        data.texPath = path;
        data.graphicClass = type;
        return data.Graphic;
    }

    /// <summary> Updates the active graphic based on a set of rules </summary>
    public void UpdateActiveGraphic()
    {
        // attempt to find a replacement graphic
        foreach (var replacement in replacements)
        {
            if (replacement.TryReplace(Drawer.renderer))
            {
                break; // break on first successful replacement
            }
        }
    }

    /// <summary> Executed every time the pawn is updated </summary>
    public override void TickRare()
    {
        base.TickRare();

        if (pather == null)
        {
            return; // this can occur if the pawn leaves the map area
        }

        if (ageTracker == null)
        {
            return;
        }

        if (Prefs.DevMode)
        {
            Log.Message("setup replacements");
        }

        // initialize the replacements (once per lifestage)
        if (lifeStageBefore != ageTracker.CurKindLifeStage)
        {
            SetupReplacements();
            lifeStageBefore = ageTracker.CurKindLifeStage;
        }

        if (Prefs.DevMode)
        {
            Log.Message("updating gfx");
        }

        UpdateActiveGraphic();
    }

    public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
    {
        base.PostApplyDamage(dinfo, totalDamageDealt);

        // try to run from attacker
        if (!Dead && !Downed && def.race.manhunterOnDamageChance == 0)
        {
            var wander_dest = RCellFinder.RandomWanderDestFor(this, Position, 12f, null, Danger.Deadly);
            if (wander_dest.IsValid)
            {
                jobs.StopAll();
                jobs.StartJob(new Job(JobDefOf.GotoWander, wander_dest)
                {
                    expiryInterval = 1500,
                    locomotionUrgency = LocomotionUrgency.Sprint
                });
            }
        }

        // update graphic if died
        if (Dead)
        {
            UpdateActiveGraphic();
        }
    }
}