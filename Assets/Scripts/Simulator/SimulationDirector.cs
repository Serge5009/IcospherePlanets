using System;
using System.Collections.Generic;
using UnityEngine;

public class SimulationDirector : MonoBehaviour
{
    public static SimulationDirector Instance { get; private set; }

    [Header("Debug Toggles")]
    public bool simulateChemistry = true;
    public bool simulateVolatiles = true;
    public bool simulateBiosphere = true;
    public bool simulateEconomy = true;

    private List<ISimulationModule> activeModules = new List<ISimulationModule>();

    private ChemistryModule chemistryModule;
    private VolatileModule volatileModule;
    private BiosphereModule biosphereModule;
    private EconomyModule economyModule;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        InitializeModules();
    }

    private void InitializeModules()
    {
        chemistryModule = new ChemistryModule();
        volatileModule = new VolatileModule();
        biosphereModule = new BiosphereModule();
        economyModule = new EconomyModule();

        activeModules.Add(chemistryModule);
        activeModules.Add(volatileModule);
        activeModules.Add(biosphereModule);
        activeModules.Add(economyModule);

        foreach (var module in activeModules)
        {
            module.Initialize();
        }
    }

    private void Start()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnHourTick += HandleHourTick;
            TimeManager.Instance.OnDayTick += HandleDayTick;
            TimeManager.Instance.OnMonthTick += HandleMonthTick;
            TimeManager.Instance.OnYearTick += HandleYearTick;
        }
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnHourTick -= HandleHourTick;
            TimeManager.Instance.OnDayTick -= HandleDayTick;
            TimeManager.Instance.OnMonthTick -= HandleMonthTick;
            TimeManager.Instance.OnYearTick -= HandleYearTick;
        }
    }

    private void HandleHourTick() { }

    private void HandleDayTick()
    {
        if (SystemDataGenerator.Instance == null || SystemDataGenerator.Instance.allBodies.Count == 0) return;

        List<CelestialBody> bodiesNeedingEquilibrium = new List<CelestialBody>();

        foreach (var body in SystemDataGenerator.Instance.allBodies)
        {
            bool needsUpdate = false;

            if (simulateEconomy) needsUpdate |= economyModule.SimulateDay(body);

            if (needsUpdate)
            {
                bodiesNeedingEquilibrium.Add(body);
            }
        }

        if (bodiesNeedingEquilibrium.Count > 0)
        {
            ClimateResolver.Instance.TickClimateEquilibrium(bodiesNeedingEquilibrium);
        }
    }

    private void HandleMonthTick()
    {
        if (SystemDataGenerator.Instance == null || SystemDataGenerator.Instance.allBodies.Count == 0) return;

        List<CelestialBody> bodiesNeedingEquilibrium = new List<CelestialBody>();

        foreach (var body in SystemDataGenerator.Instance.allBodies)
        {
            bool needsUpdate = false;

            if (simulateChemistry) needsUpdate |= chemistryModule.SimulateMonth(body);
            if (simulateVolatiles) needsUpdate |= volatileModule.SimulateMonth(body);
            if (simulateBiosphere) needsUpdate |= biosphereModule.SimulateMonth(body);
            if (simulateEconomy) needsUpdate |= economyModule.SimulateMonth(body);

            if (needsUpdate)
            {
                bodiesNeedingEquilibrium.Add(body);
            }
        }

        if (bodiesNeedingEquilibrium.Count > 0)
        {
            ClimateResolver.Instance.TickClimateEquilibrium(bodiesNeedingEquilibrium);
        }
    }

    private void HandleYearTick()
    {
        if (SystemDataGenerator.Instance == null || SystemDataGenerator.Instance.allBodies.Count == 0) return;

        List<CelestialBody> bodiesNeedingEquilibrium = new List<CelestialBody>();

        foreach (var body in SystemDataGenerator.Instance.allBodies)
        {
            bool needsUpdate = false;

            if (simulateChemistry) needsUpdate |= chemistryModule.SimulateYear(body);
            if (simulateVolatiles) needsUpdate |= volatileModule.SimulateYear(body);
            if (simulateBiosphere) needsUpdate |= biosphereModule.SimulateYear(body);
            if (simulateEconomy) needsUpdate |= economyModule.SimulateYear(body);

            if (needsUpdate)
            {
                bodiesNeedingEquilibrium.Add(body);
            }
        }

        if (bodiesNeedingEquilibrium.Count > 0)
        {
            ClimateResolver.Instance.TickClimateEquilibrium(bodiesNeedingEquilibrium);
        }
    }

    public void RunFastForwardMonth(CelestialBody body)
    {
        if (simulateChemistry) chemistryModule.SimulateMonth(body);
        if (simulateVolatiles) volatileModule.SimulateMonth(body);
        if (simulateBiosphere) biosphereModule.SimulateMonth(body);
    }

    public void ForceInstantVolatileEquilibrium(CelestialBody body)
    {
        if (volatileModule != null)
        {
            volatileModule.ForceInstantVolatileEquilibrium(body);
        }
    }
}