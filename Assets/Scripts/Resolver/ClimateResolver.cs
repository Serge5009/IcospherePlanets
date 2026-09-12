using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

public class ClimateResolver : MonoBehaviour
{
    public static ClimateResolver Instance { get; private set; }

    private Queue<CelestialBody> pendingUpdates = new Queue<CelestialBody>();
    private bool isJobRunning = false;
    private JobHandle currentJobHandle;
    private PlanetSimulationData currentProcessingData;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void OnDestroy()
    {
        if (isJobRunning) currentJobHandle.Complete();
        ClimateQueue.DisposeAll();
    }

    private void Update()
    {
        if (isJobRunning)
        {
            if (currentJobHandle.IsCompleted)
            {
                currentJobHandle.Complete();
                FinalizePlanetUpdate(currentProcessingData);
                isJobRunning = false;
                currentProcessingData = null;
            }
        }
        else if (pendingUpdates.Count > 0)
        {
            CelestialBody nextBody = pendingUpdates.Dequeue();
            if (ClimateQueue.activeSimulations.TryGetValue(nextBody, out PlanetSimulationData simData))
            {
                currentProcessingData = simData;
                currentJobHandle = SchedulePlanetUpdate(simData);
                isJobRunning = true;
            }
        }
    }

    public async Task GenerateInitialClimateAsync(List<CelestialBody> bodies, int dryCycles, int wetCycles, int simCycles, Action<string> onProgress = null)
    {
        Debug.Log($"Generating Initial Climate Equilibrium...");

        if (isJobRunning) currentJobHandle.Complete();
        ClimateQueue.DisposeAll();
        pendingUpdates.Clear();
        isJobRunning = false;

        List<PlanetSimulationData> simDataList = new List<PlanetSimulationData>();

        for (int i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body.bodyType == BodyType.Star || body.bodyType == BodyType.GasGiant) continue;

            PlanetSimulationData simData = ClimateQueue.InitializePlanet(body);
            simDataList.Add(simData);
        }

        await RunGenerationCycles(simDataList, bodies, dryCycles, "Phase 1 (Dry)", onProgress);

        onProgress?.Invoke("Phase 2: Seeding Oceans...");
        for (int s = 0; s < simDataList.Count; s++)
        {
            var simData = simDataList[s];

            CalculateStatsJob statJob = new CalculateStatsJob { climates = simData.climates, topologies = simData.topologies, waterLevel = simData.body.waterLevel, result = simData.statsResult };
            statJob.Schedule().Complete();
            simData.body.globalMinTemperature = simData.statsResult[0].minTemp;
            simData.body.globalMaxTemperature = simData.statsResult[0].maxTemp;

            bool seeded = HydrologyBuilder.TrySeedOceans(simData.body, simData.maxAltitude);
            if (seeded)
            {
                Color iceCol = simData.body.oceanLiquid != null ? simData.body.oceanLiquid.iceColor : Color.white;
                for (int i = 0; i < simData.terrainVisuals.Length; i++)
                {
                    TerrainVisualData vis = simData.terrainVisuals[i];
                    vis.liquidColor = simData.body.oceanColor;
                    vis.iceColorR = iceCol.r;
                    vis.iceColorG = iceCol.g;
                    vis.iceColorB = iceCol.b;
                    simData.terrainVisuals[i] = vis;
                }
            }
        }
        await Task.Yield();

        await RunGenerationCycles(simDataList, bodies, wetCycles, "Phase 3 (Wet)", onProgress);

        onProgress?.Invoke("Phase 4: Forcing Volatile Equilibrium...");
        foreach (var simData in simDataList)
        {
            if (SimulationDirector.Instance != null)
            {
                SimulationDirector.Instance.ForceInstantVolatileEquilibrium(simData.body);
            }
        }
        await RunGenerationCycles(simDataList, bodies, 1, "Phase 4 (Snap)", onProgress);

        await RunVolatileSimulationCycles(simDataList, bodies, simCycles, "Phase 5 (Volatiles)", onProgress);

        foreach (var simData in simDataList)
        {
            CalculateStatsJob statJob = new CalculateStatsJob { climates = simData.climates, topologies = simData.topologies, waterLevel = simData.body.waterLevel, result = simData.statsResult };
            statJob.Schedule().Complete();
            ApplyStatsToBody(simData.body, simData.statsResult[0], simData.climates.Length);
        }

        Debug.Log("Initial Climate Equilibrium Resolved!");

        GeopoliticsGenerator.GenerateNations(bodies);

        if (MapModeManager.Instance != null)
        {
            MapModeManager.Instance.ApplyModeToAllPlanets();
        }
    }

    public void TickClimateEquilibrium(List<CelestialBody> bodies)
    {
        if (bodies == null || bodies.Count == 0) return;

        foreach (var body in bodies)
        {
            if (!pendingUpdates.Contains(body))
            {
                pendingUpdates.Enqueue(body);
            }
        }
    }

    private JobHandle SchedulePlanetUpdate(PlanetSimulationData simData)
    {
        CelestialBody body = simData.body;
        JobHandle currentDependency = default;

        if (body.HasCoastlineChanged(body.waterLevel))
        {
            CalculateMoistureJob mJob = new CalculateMoistureJob
            {
                topologies = simData.topologies,
                neighborOffsets = simData.neighborOffsets,
                neighborCounts = simData.neighborCounts,
                neighbors = simData.neighbors,
                rainFactors = simData.rainFactors,
                waterLevel = body.waterLevel,
                isTidallyLocked = body.isTidallyLocked,
                sunDirection = Vector3.right
            };
            JobHandle mHandle = mJob.Schedule(simData.topologies.Length, 64);

            JobHandle bHandle1 = new BlurMoistureJob { inputRainFactors = simData.rainFactors, neighborOffsets = simData.neighborOffsets, neighborCounts = simData.neighborCounts, neighbors = simData.neighbors, outputRainFactors = simData.blurredRainFactors }.Schedule(simData.topologies.Length, 64, mHandle);
            JobHandle bHandle2 = new BlurMoistureJob { inputRainFactors = simData.blurredRainFactors, neighborOffsets = simData.neighborOffsets, neighborCounts = simData.neighborCounts, neighbors = simData.neighbors, outputRainFactors = simData.rainFactors }.Schedule(simData.topologies.Length, 64, bHandle1);
            JobHandle bHandle3 = new BlurMoistureJob { inputRainFactors = simData.rainFactors, neighborOffsets = simData.neighborOffsets, neighborCounts = simData.neighborCounts, neighbors = simData.neighbors, outputRainFactors = simData.blurredRainFactors }.Schedule(simData.topologies.Length, 64, bHandle2);

            currentDependency = bHandle3;
            body.lastCalculatedWaterLevel = body.waterLevel;
        }

        double starSolarMasses = SystemDataGenerator.Instance.star.massKg / AstroMath.SOLAR_MASS_KG;
        double starLuminosity = AstroMath.CalculateLuminosity(starSolarMasses);
        PlanetClimateState state = ClimateStateBuilder.CalculateGlobalState(simData, starLuminosity);

        ClimateEquilibriumJob cJob = new ClimateEquilibriumJob
        {
            topologies = simData.topologies,
            rainFactors = simData.body.HasCoastlineChanged(simData.body.waterLevel) ? simData.blurredRainFactors : simData.rainFactors,
            climates = simData.climates,
            terrainVisuals = simData.terrainVisuals,
            state = state
        };
        currentDependency = cJob.Schedule(simData.topologies.Length, 64, currentDependency);

        PackVisualsJob vJob = new PackVisualsJob
        {
            climates = simData.climates,
            terrainVisuals = simData.terrainVisuals
        };
        currentDependency = vJob.Schedule(simData.topologies.Length, 64, currentDependency);

        CalculateStatsJob sJob = new CalculateStatsJob
        {
            climates = simData.climates,
            topologies = simData.topologies,
            waterLevel = body.waterLevel,
            result = simData.statsResult
        };
        currentDependency = sJob.Schedule(currentDependency);

        return currentDependency;
    }

    private void FinalizePlanetUpdate(PlanetSimulationData simData)
    {
        ApplyStatsToBody(simData.body, simData.statsResult[0], simData.climates.Length);

        simData.body.isVisualsDirty = true;

        simData.terrainVisuals.CopyTo(simData.meshData.terrainVisuals);
        simData.climates.CopyTo(simData.meshData.climates);

        if (simData.body.lastCalculatedWaterLevel == simData.body.waterLevel)
        {
            simData.blurredRainFactors.CopyTo(simData.rainFactors);
            for (int j = 0; j < simData.topologies.Length; j++)
            {
                var t = simData.topologies[j];
                t.rainFactor = simData.rainFactors[j];
                simData.topologies[j] = t;
            }
            simData.topologies.CopyTo(simData.meshData.topologies);
        }

        TerrainVisualData[] highResArray = simData.body.localViewData.terrainVisuals;
        TerrainVisualData[] lowResArray = simData.body.systemViewData.terrainVisuals;
        int[] map = simData.body.lowToHighMap;
        for (int j = 0; j < lowResArray.Length; j++) lowResArray[j] = highResArray[map[j]];

        if (MapModeManager.Instance != null)
        {
            MapModeManager.Instance.ApplyModeToAllPlanets();
        }
    }


    private async Task RunGenerationCycles(List<PlanetSimulationData> simDataList, List<CelestialBody> bodies, int cycles, string phaseName, Action<string> onProgress)
    {
        for (int i = 0; i < cycles; i++)
        {
            NativeList<JobHandle> handles = new NativeList<JobHandle>(simDataList.Count, Allocator.Temp);

            foreach (var simData in simDataList)
            {
                handles.Add(SchedulePlanetUpdate(simData));
            }

            JobHandle.CompleteAll(handles.AsArray());
            handles.Dispose();

            if (i % 10 == 0 || i == cycles - 1)
            {
                onProgress?.Invoke($"Resolving Climate {phaseName}: Cycle {i + 1} / {cycles}");

                foreach (var simData in simDataList)
                {
                    FinalizePlanetUpdate(simData);
                }
                await Task.Yield();
            }
        }
    }

    private async Task RunVolatileSimulationCycles(List<PlanetSimulationData> simDataList, List<CelestialBody> bodies, int cycles, string phaseName, Action<string> onProgress)
    {
        for (int i = 0; i < cycles; i++)
        {
            foreach (var simData in simDataList)
            {
                if (SimulationDirector.Instance != null)
                {
                    SimulationDirector.Instance.RunFastForwardMonth(simData.body);
                }
            }

            NativeList<JobHandle> handles = new NativeList<JobHandle>(simDataList.Count, Allocator.Temp);
            foreach (var simData in simDataList)
            {
                handles.Add(SchedulePlanetUpdate(simData));
            }

            JobHandle.CompleteAll(handles.AsArray());
            handles.Dispose();

            if (i % 10 == 0 || i == cycles - 1)
            {
                onProgress?.Invoke($"Resolving Climate {phaseName}: Cycle {i + 1} / {cycles}");

                foreach (var simData in simDataList)
                {
                    FinalizePlanetUpdate(simData);
                }
                await Task.Yield();
            }
        }
    }

    private void ApplyStatsToBody(CelestialBody body, PlanetStats stats, int totalCells)
    {
        body.globalMinTemperature = stats.minTemp;
        body.globalMaxTemperature = stats.maxTemp;

        body.globalOceanCoverage = (float)stats.oceanCount / totalCells;
        body.globalIceCoverage = (float)stats.iceCount / totalCells;
        body.globalSnowCoverage = (float)stats.snowCount / totalCells;
        body.globalBiomassCoverage = (float)stats.bioCount / totalCells;
        body.globalDesertCoverage = (float)stats.barrenCount / totalCells;

        long pop = 0;
        if (body.localViewData != null && body.localViewData.economies != null)
        {
            for (int i = 0; i < body.localViewData.economies.Length; i++)
            {
                pop += body.localViewData.economies[i].population;
            }
        }
        body.globalPopulation = pop;
    }
}