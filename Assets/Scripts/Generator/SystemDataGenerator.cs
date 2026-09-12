using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct WeightedBedrock { public BedrockTemplate template; public float weight; }

[Serializable]
public struct WeightedGas { public GasTemplate template; public float weight; }

[Serializable]
public struct WeightedLiquid { public LiquidTemplate template; public float weight; }

[Serializable]
public struct WeightedSoil { public SoilTemplate template; public float weight; }

public class SystemDataGenerator : MonoBehaviour
{
    public static SystemDataGenerator Instance { get; private set; }

    [Header("Generation Settings")]
    public StarClassTemplate starClass;
    public string systemName = "Sol";
    public float rockBudgetEarths = 15f;
    public float gasBudgetEarths = 500f;

    [Header("Accretion Pools: Bedrock")]
    public List<WeightedBedrock> innerBedrocks;
    public List<WeightedBedrock> habitableBedrocks;
    public List<WeightedBedrock> outerBedrocks;

    [Header("Accretion Pools: Soils")]
    public List<WeightedSoil> innerSoils;
    public List<WeightedSoil> habitableSoils;
    public List<WeightedSoil> outerSoils;

    [Header("Accretion Pools: Gases & Oceans")]
    public List<WeightedGas> primordialGases;
    public List<WeightedLiquid> primordialLiquids;

    [Header("Subdivision Math")]
    public float targetCellRadiusKm = 50f;
    public int maxDataSubdivisions = 7;
    public int maxGiantSubdivisions = 5;

    public List<CelestialBody> allBodies { get; private set; } = new List<CelestialBody>();
    public List<CelestialBody> mainPlanets { get; private set; } = new List<CelestialBody>();
    public CelestialBody star { get; set; }

    public double systemEdgeKm { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Update()
    {
        if (TimeManager.Instance == null) return;
        double time = TimeManager.Instance.totalGameSeconds;
        foreach (var body in allBodies) body.UpdateRotation(time);
    }

    public void GenerateData()
    {
        AccretionEngine.BuildSystem(this);
    }
}