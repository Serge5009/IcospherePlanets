using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

public struct PlanetClimateState
{
    public float blackbodyTemp;
    public float greenhouseHeat;
    public float atmosphericPressure;
    public float lapseRate;
    public float globalRainStrength;
    public float freezingPoint;
    public float boilingPoint;
    public bool isTidallyLocked;
    public Vector3 sunDirection;
    public float waterLevel;

    public float minAltitude;
    public float maxAltitude;
    public float globalSoilThickness;
    public Vector4 soilDryColor;
    public Vector4 soilWetColor;
    public byte dominantBedrockId;
    public byte secondaryBedrockId;
    public Vector4 dominantBedrockColor;
    public Vector4 secondaryBedrockColor;
    public Vector3 iceColor;

    public float eqInsolation;
    public float poleInsolation;
}

public struct PlanetStats
{
    public float minTemp;
    public float maxTemp;
    public int oceanCount;
    public int iceCount;
    public int snowCount;
    public int bioCount;
    public int barrenCount;
}

[BurstCompile]
public struct CalculateMoistureJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<CellTopology> topologies;
    [ReadOnly] public NativeArray<int> neighborOffsets;
    [ReadOnly] public NativeArray<byte> neighborCounts;
    [ReadOnly] public NativeArray<int> neighbors;

    public NativeArray<float> rainFactors;

    public float waterLevel;
    public bool isTidallyLocked;
    public Vector3 sunDirection;

    public void Execute(int i)
    {
        CellTopology topo = topologies[i];

        if (waterLevel > -5000f && topo.altitude < waterLevel)
        {
            rainFactors[i] = 1.5f;
            return;
        }

        Vector3 pos = topo.localPosition.normalized;
        Vector3 windDir;

        if (isTidallyLocked)
        {
            windDir = Vector3.ProjectOnPlane(sunDirection, pos).normalized;
        }
        else
        {
            float lat = Mathf.Asin(pos.y);
            float lon = Mathf.Atan2(pos.z, pos.x);

            Vector3 east = new Vector3(-Mathf.Sin(lon), 0, Mathf.Cos(lon)).normalized;
            Vector3 north = new Vector3(
                -Mathf.Sin(lat) * Mathf.Cos(lon),
                Mathf.Cos(lat),
                -Mathf.Sin(lat) * Mathf.Sin(lon)
            ).normalized;

            float latDeg = lat * Mathf.Rad2Deg;
            float absLat = Mathf.Abs(latDeg);

            if (absLat < 30f)
            {
                float equatorDir = latDeg > 0 ? -0.5f : 0.5f;
                windDir = (-east + north * equatorDir).normalized;
            }
            else if (absLat < 60f)
            {
                float poleDir = latDeg > 0 ? 0.5f : -0.5f;
                windDir = (east + north * poleDir).normalized;
            }
            else
            {
                float equatorDir = latDeg > 0 ? -0.5f : 0.5f;
                windDir = (-east + north * equatorDir).normalized;
            }
        }

        float jitterX = (i % 13) / 13f - 0.5f;
        float jitterY = (i % 17) / 17f - 0.5f;
        float jitterZ = (i % 19) / 19f - 0.5f;
        Vector3 turbulence = new Vector3(jitterX, jitterY, jitterZ) * 0.3f;

        Vector3 rayDir = -(windDir + turbulence).normalized;

        int currentCell = i;
        int previousCell = -1;
        float maxAltCrossed = topo.altitude;
        int steps = 0;
        int maxSteps = 60;

        int waterCellsHit = 0;
        bool hitOcean = false;

        float currentProgress = Vector3.Dot(pos, rayDir);

        while (steps < maxSteps)
        {
            int offset = neighborOffsets[currentCell];
            int count = neighborCounts[currentCell];

            int bestNeighbor = -1;
            float bestDot = -2f;
            float bestProgress = currentProgress;

            for (int n = 0; n < count; n++)
            {
                int neighborIdx = neighbors[offset + n];
                if (neighborIdx == previousCell) continue;

                Vector3 neighborPos = topologies[neighborIdx].localPosition.normalized;

                float progress = Vector3.Dot(neighborPos, rayDir);
                if (progress <= currentProgress + 0.0001f) continue;

                Vector3 dirToNeighbor = (neighborPos - topologies[currentCell].localPosition.normalized).normalized;
                float d = Vector3.Dot(dirToNeighbor, rayDir);

                if (d > bestDot)
                {
                    bestDot = d;
                    bestNeighbor = neighborIdx;
                    bestProgress = progress;
                }
            }

            if (bestNeighbor == -1 || bestDot < -0.5f) break;

            previousCell = currentCell;
            currentCell = bestNeighbor;

            float cellAlt = topologies[currentCell].altitude;
            if (cellAlt > maxAltCrossed) maxAltCrossed = cellAlt;

            if (waterLevel > -5000f && cellAlt < waterLevel)
            {
                hitOcean = true;
                waterCellsHit++;
                if (waterCellsHit >= 5) break;
            }
            else if (hitOcean)
            {
                break;
            }

            steps++;
        }

        if (!hitOcean)
        {
            rainFactors[i] = 0f;
        }
        else
        {
            float sourceCharge = 0.5f + (0.5f * Mathf.Sqrt(waterCellsHit / 5.0f));
            float baseMoisture = 1.5f - ((float)steps / maxSteps);
            float mountainPenalty = Mathf.Max(0, maxAltCrossed - topo.altitude) / 2000f;

            rainFactors[i] = Mathf.Max(0f, (baseMoisture - mountainPenalty) * sourceCharge);
        }
    }
}

[BurstCompile]
public struct BlurMoistureJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float> inputRainFactors;
    [ReadOnly] public NativeArray<int> neighborOffsets;
    [ReadOnly] public NativeArray<byte> neighborCounts;
    [ReadOnly] public NativeArray<int> neighbors;

    public NativeArray<float> outputRainFactors;

    public void Execute(int i)
    {
        float totalRain = inputRainFactors[i];
        int offset = neighborOffsets[i];
        int count = neighborCounts[i];

        for (int n = 0; n < count; n++)
        {
            int neighborIdx = neighbors[offset + n];
            totalRain += inputRainFactors[neighborIdx];
        }

        outputRainFactors[i] = totalRain / (count + 1);
    }
}

[BurstCompile]
public struct ClimateEquilibriumJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<CellTopology> topologies;
    [ReadOnly] public NativeArray<float> rainFactors;

    public NativeArray<CellClimate> climates;
    public NativeArray<TerrainVisualData> terrainVisuals;

    public PlanetClimateState state;

    public void Execute(int i)
    {
        CellTopology topo = topologies[i];
        CellClimate clim = climates[i];

        float baseTemp = state.blackbodyTemp + state.greenhouseHeat;
        float localTemp = baseTemp;

        if (state.isTidallyLocked)
        {
            float dot = Vector3.Dot(topo.localPosition.normalized, state.sunDirection);
            float insolation = Mathf.Max(0, dot);
            if (dot > 0) localTemp = baseTemp * 1.2f * Mathf.Pow(insolation, 0.25f);
            else localTemp = baseTemp * 0.2f;
        }
        else
        {
            Vector3 normPos = topo.localPosition.normalized;
            float cosLat = Mathf.Sqrt(Mathf.Clamp01(1f - normPos.y * normPos.y));
            float insolation = Mathf.Lerp(state.poleInsolation, state.eqInsolation, cosLat);

            localTemp = baseTemp * (0.8f + 0.4f * insolation);
        }

        float elevation = topo.altitude;
        if (state.waterLevel > -5000f)
        {
            elevation = Mathf.Max(0f, topo.altitude - state.waterLevel);
        }

        if (elevation > 0)
        {
            localTemp -= (elevation / 1000f) * state.lapseRate;
        }

        float blendFactor = Mathf.Clamp01(state.atmosphericPressure / 5.0f);
        localTemp = Mathf.Lerp(localTemp, baseTemp, blendFactor);
        clim.localTemperature = localTemp;

        if (state.waterLevel <= -5000f) clim.liquidDepth = 0f;

        if (state.waterLevel > -5000f && topo.altitude < state.waterLevel)
        {
            clim.liquidDepth = (state.waterLevel - topo.altitude) / 1000f;
        }
        else
        {
            clim.liquidDepth = 0f;
        }

        float dryingFactor = 0f;
        if (localTemp > state.freezingPoint)
        {
            float heatRange = state.boilingPoint - state.freezingPoint;
            float heatRatio = Mathf.Clamp01((localTemp - state.freezingPoint) / heatRange);
            dryingFactor = heatRatio * 0.5f;
        }

        if (clim.liquidDepth > 0)
        {
            clim.moisture = 1.0f;
        }
        else
        {
            float rawMoisture = rainFactors[i] * state.globalRainStrength;
            clim.moisture = Mathf.Clamp01(rawMoisture - dryingFactor);
        }

        if (state.atmosphericPressure < 0.05f)
        {
            if (localTemp > state.freezingPoint)
            {
                clim.liquidDepth = 0f;
                clim.snowDepth = 0f;
                clim.iceCover = 0f;
                clim.moisture = 0f;
            }
            else
            {
                if (clim.liquidDepth > 0)
                {
                    clim.iceCover = 1.0f;
                    clim.liquidDepth = 0f;
                }
                else
                {
                    clim.iceCover = Mathf.Clamp01(clim.snowDepth);
                }
            }
        }
        else if (localTemp < state.freezingPoint && state.waterLevel > -5000f)
        {
            float degreesBelow = state.freezingPoint - localTemp;
            float baselineFrost = degreesBelow * 0.02f;
            clim.snowDepth = Mathf.Max(baselineFrost, state.globalRainStrength * degreesBelow * 0.1f);

            if (clim.liquidDepth > 0)
            {
                clim.iceCover = 1.0f;
                clim.liquidDepth = 0f;
            }
            else
            {
                clim.iceCover = Mathf.Clamp01(clim.snowDepth);
            }
        }
        else if (localTemp > state.boilingPoint)
        {
            clim.liquidDepth = 0f;
            clim.snowDepth = 0f;
            clim.iceCover = 0f;
            clim.moisture = 0f;
        }
        else
        {
            clim.snowDepth = 0f;
            clim.iceCover = 0f;
        }

        if (localTemp > state.freezingPoint && localTemp < (state.freezingPoint + 50f) && clim.moisture > 0.2f && clim.liquidDepth <= 0f)
        {
            clim.biomass = Mathf.Clamp01(clim.moisture);
        }
        else
        {
            clim.biomass = 0f;
        }

        climates[i] = clim;

        float tAlt = (topo.altitude - state.minAltitude) / Mathf.Max(1f, state.maxAltitude - state.minAltitude);
        float baseThickness = 1.0f - tAlt;

        float curvePower = 1.0f + (state.atmosphericPressure * 2.0f);
        float thickness = Mathf.Pow(Mathf.Max(0f, baseThickness), curvePower);

        thickness -= rainFactors[i] * tAlt * 0.5f;
        thickness *= state.globalSoilThickness;
        thickness = Mathf.Clamp01(thickness);

        Vector4 bedrockCol = (topo.bedrockId == state.secondaryBedrockId) ? state.secondaryBedrockColor : state.dominantBedrockColor;
        Vector4 currentSoilCol = Vector4.Lerp(state.soilDryColor, state.soilWetColor, clim.moisture);
        Vector4 finalGroundCol = Vector4.Lerp(bedrockCol, currentSoilCol, thickness);

        TerrainVisualData vis = terrainVisuals[i];
        vis.bedrockColor = finalGroundCol;
        vis.surfaceData = new Vector4(clim.iceCover, clim.biomass, clim.liquidDepth, 0f);
        vis.iceColorR = state.iceColor.x;
        vis.iceColorG = state.iceColor.y;
        vis.iceColorB = state.iceColor.z;
        terrainVisuals[i] = vis;
    }
}

[BurstCompile]
public struct PackVisualsJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<CellClimate> climates;
    public NativeArray<TerrainVisualData> terrainVisuals;

    public void Execute(int i)
    {
        TerrainVisualData vis = terrainVisuals[i];
        CellClimate clim = climates[i];
        vis.surfaceData = new Vector4(clim.iceCover, clim.biomass, clim.liquidDepth, 0f);
        terrainVisuals[i] = vis;
    }
}

[BurstCompile]
public struct CalculateStatsJob : IJob
{
    [ReadOnly] public NativeArray<CellClimate> climates;
    [ReadOnly] public NativeArray<CellTopology> topologies;
    public float waterLevel;
    public NativeArray<PlanetStats> result;

    public void Execute()
    {
        float minT = float.MaxValue;
        float maxT = float.MinValue;
        int ocean = 0, ice = 0, snow = 0, bio = 0, barren = 0;

        for (int i = 0; i < climates.Length; i++)
        {
            float t = climates[i].localTemperature;
            if (t < minT) minT = t;
            if (t > maxT) maxT = t;

            if (waterLevel > -5000f && topologies[i].altitude < waterLevel)
            {
                if (climates[i].iceCover > 0.5f) ice++;
                else ocean++;
            }
            else
            {
                if (climates[i].iceCover > 0.5f) snow++;
                else if (climates[i].biomass > 0.5f) bio++;
                else barren++;
            }
        }

        result[0] = new PlanetStats
        {
            minTemp = minT,
            maxTemp = maxT,
            oceanCount = ocean,
            iceCount = ice,
            snowCount = snow,
            bioCount = bio,
            barrenCount = barren
        };
    }
}

public class ClimateResolver : MonoBehaviour
{
    public static ClimateResolver Instance { get; private set; }

    private class PlanetSimulationData
    {
        public CelestialBody body;
        public PlanetMeshData meshData;
        public float maxAltitude;

        public NativeArray<CellTopology> topologies;
        public NativeArray<CellClimate> climates;
        public NativeArray<TerrainVisualData> terrainVisuals;

        public NativeArray<int> neighborOffsets;
        public NativeArray<byte> neighborCounts;
        public NativeArray<int> neighbors;

        public NativeArray<float> rainFactors;
        public NativeArray<float> blurredRainFactors;

        public NativeArray<PlanetStats> statsResult;

        public void Dispose()
        {
            if (topologies.IsCreated) topologies.Dispose();
            if (climates.IsCreated) climates.Dispose();
            if (terrainVisuals.IsCreated) terrainVisuals.Dispose();
            if (neighborOffsets.IsCreated) neighborOffsets.Dispose();
            if (neighborCounts.IsCreated) neighborCounts.Dispose();
            if (neighbors.IsCreated) neighbors.Dispose();
            if (rainFactors.IsCreated) rainFactors.Dispose();
            if (blurredRainFactors.IsCreated) blurredRainFactors.Dispose();
            if (statsResult.IsCreated) statsResult.Dispose();
        }
    }

    private Dictionary<CelestialBody, PlanetSimulationData> activeSimulations = new Dictionary<CelestialBody, PlanetSimulationData>();

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
        foreach (var sim in activeSimulations.Values) sim.Dispose();
        activeSimulations.Clear();
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
            if (activeSimulations.TryGetValue(nextBody, out PlanetSimulationData simData))
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
        foreach (var sim in activeSimulations.Values) sim.Dispose();
        activeSimulations.Clear();
        pendingUpdates.Clear();
        isJobRunning = false;

        List<PlanetSimulationData> simDataList = new List<PlanetSimulationData>();

        for (int i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body.bodyType == BodyType.Star || body.bodyType == BodyType.GasGiant) continue;

            float maxAlt = 1f;
            foreach (var topo in body.localViewData.topologies)
            {
                if (topo.altitude > maxAlt) maxAlt = topo.altitude;
            }

            NativeArray<float> initialRain = new NativeArray<float>(body.localViewData.topologies.Length, Allocator.Persistent);
            NativeArray<float> blurBuffer = new NativeArray<float>(body.localViewData.topologies.Length, Allocator.Persistent);
            for (int j = 0; j < initialRain.Length; j++) initialRain[j] = body.localViewData.topologies[j].rainFactor;

            PlanetSimulationData simData = new PlanetSimulationData
            {
                body = body,
                meshData = body.localViewData,
                maxAltitude = maxAlt,
                topologies = new NativeArray<CellTopology>(body.localViewData.topologies, Allocator.Persistent),
                climates = new NativeArray<CellClimate>(body.localViewData.climates, Allocator.Persistent),
                terrainVisuals = new NativeArray<TerrainVisualData>(body.localViewData.terrainVisuals, Allocator.Persistent),

                neighborOffsets = new NativeArray<int>(body.geometryTemplate.neighborOffsets, Allocator.Persistent),
                neighborCounts = new NativeArray<byte>(body.geometryTemplate.neighborCounts, Allocator.Persistent),
                neighbors = new NativeArray<int>(body.geometryTemplate.neighbors, Allocator.Persistent),

                rainFactors = initialRain,
                blurredRainFactors = blurBuffer,
                statsResult = new NativeArray<PlanetStats>(1, Allocator.Persistent)
            };

            simDataList.Add(simData);
            activeSimulations.Add(body, simData);
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

            bool seeded = SystemDataGenerator.Instance.TrySeedOceans(simData.body, simData.maxAltitude);
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

        await RunVolatileSimulationCycles(simDataList, bodies, simCycles, "Phase 4 (Volatiles)", onProgress);

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
        PlanetClimateState state = CalculateGlobalState(simData, starLuminosity);

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
                    SimulationDirector.Instance.ProcessVolatileExchange(simData.body);
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

    private PlanetClimateState CalculateGlobalState(PlanetSimulationData simData, double starLuminosity)
    {
        CelestialBody body = simData.body;

        float totalCells = simData.climates.Length;
        float surfaceAlbedo = 0.3f;
        float oceanFraction = 0f;
        float oceanCurve = 0f;

        if (totalCells > 0)
        {
            float totalAlbedo = (body.globalIceCoverage * 0.6f) + (body.globalOceanCoverage * 0.1f) + (body.globalSnowCoverage * 0.6f) + (body.globalBiomassCoverage * 0.15f) + (body.globalDesertCoverage * 0.25f);
            surfaceAlbedo = totalAlbedo;

            float effectiveOceanFraction = body.globalOceanCoverage + (body.globalIceCoverage * 0.15f);
            oceanFraction = Mathf.Sqrt(Mathf.Clamp01(effectiveOceanFraction * 2.0f));
            oceanCurve = oceanFraction;
        }

        float atmosThickness = Mathf.Clamp01((float)body.surfacePressureAtm / 2.0f);
        float globalAlbedo = Mathf.Lerp(surfaceAlbedo, 0.3f, atmosThickness);

        double distanceAU = body.orbit.semiMajorAxis / AstroMath.AU_TO_KM;
        float blackbody = AstroMath.CalculateBlackbodyTemperature(starLuminosity, distanceAU, globalAlbedo);

        float freezingPt = body.oceanLiquid != null ? body.oceanLiquid.baseFreezingPointKelvin : 273.15f;
        float boilingPt = body.oceanLiquid != null ? body.oceanLiquid.baseBoilingPointKelvin : 373.15f;

        float baseTemp = blackbody + body.greenhouseHeatContribution;

        float blendFactor = Mathf.Clamp01((float)body.surfacePressureAtm / 5.0f);
        float rawMaxTemp = baseTemp * 1.2f;
        float trueMaxTemp = Mathf.Lerp(rawMaxTemp, baseTemp, blendFactor);

        if (trueMaxTemp > boilingPt && body.waterLevel > -5000f)
        {
            SystemDataGenerator.Instance.TriggerRunawayGreenhouse(body);
        }

        float dynamicLapseRate = (float)(body.surfaceGravity / 9.8) * 6.5f;
        float tempFactor = Mathf.Clamp01(blackbody / 288f);
        float pressureFactor = Mathf.Clamp01((float)body.surfacePressureAtm / 0.05f);

        float relativeHumidity = 0f;
        if (body.oceanLiquid != null && body.oceanLiquid.evaporatesInto != null && body.targetVaporMassKg > 0)
        {
            byte vaporId = body.oceanLiquid.evaporatesInto.gasId;
            double currentVapor = body.atmosphericGasesKg.ContainsKey(vaporId) ? body.atmosphericGasesKg[vaporId] : 0;
            relativeHumidity = Mathf.Clamp01((float)(currentVapor / body.targetVaporMassKg));
        }

        float rainStrength = oceanCurve * relativeHumidity * tempFactor * pressureFactor;
        body.globalRainStrength = rainStrength;

        float globalSoil = body.soilBaseThickness * (1.0f + (float)body.surfacePressureAtm) * (float)(body.surfaceGravity / 9.8) * (1.0f + rainStrength);
        globalSoil = Mathf.Clamp(globalSoil, 0.1f, 3.0f);

        Color iceCol = body.oceanLiquid != null ? body.oceanLiquid.iceColor : Color.white;

        float absTilt = Mathf.Abs((float)body.axialTilt);
        float eqInsolation, poleInsolation;
        if (absTilt <= 54f)
        {
            float t = absTilt / 54f;
            eqInsolation = 1.0f - (t * 0.5f);
            poleInsolation = t * 0.5f;
        }
        else
        {
            float t = (absTilt - 54f) / 36f;
            eqInsolation = 0.5f - (t * 0.5f);
            poleInsolation = 0.5f + (t * 0.5f);
        }

        return new PlanetClimateState
        {
            blackbodyTemp = blackbody,
            greenhouseHeat = body.greenhouseHeatContribution,
            atmosphericPressure = (float)body.surfacePressureAtm,
            lapseRate = dynamicLapseRate,
            globalRainStrength = rainStrength,
            freezingPoint = freezingPt,
            boilingPoint = boilingPt,
            isTidallyLocked = body.isTidallyLocked,
            sunDirection = Vector3.right,
            waterLevel = body.waterLevel,

            minAltitude = 0f,
            maxAltitude = simData.maxAltitude,
            globalSoilThickness = globalSoil,
            soilDryColor = body.soilDryColor,
            soilWetColor = body.soilWetColor,
            dominantBedrockId = body.dominantBedrockId,
            secondaryBedrockId = body.secondaryBedrockId,
            dominantBedrockColor = body.dominantBedrockColor,
            secondaryBedrockColor = body.secondaryBedrockColor,
            iceColor = new Vector3(iceCol.r, iceCol.g, iceCol.b),

            eqInsolation = eqInsolation,
            poleInsolation = poleInsolation
        };
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