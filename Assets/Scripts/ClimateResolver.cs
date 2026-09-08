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

        if (localTemp < state.freezingPoint && state.waterLevel > -5000f && state.atmosphericPressure >= 0.05f)
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

public class ClimateResolver : MonoBehaviour
{
    public static ClimateResolver Instance { get; private set; }

    private class PlanetSimulationData
    {
        public CelestialBody body;
        public PlanetMeshData meshData;
        public NativeArray<CellTopology> topologies;
        public NativeArray<CellClimate> climates;
        public NativeArray<TerrainVisualData> terrainVisuals;

        public NativeArray<int> neighborOffsets;
        public NativeArray<byte> neighborCounts;
        public NativeArray<int> neighbors;

        public NativeArray<float> rainFactors;
        public NativeArray<float> blurredRainFactors;

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
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public async Task ResolveEquilibriumAsync(List<CelestialBody> bodies, int cycles, Action<string> onProgress = null)
    {
        Debug.Log($"Resolving Climate Equilibrium for {cycles} cycles...");

        List<PlanetSimulationData> simDataList = new List<PlanetSimulationData>();
        float[] maxAltitudes = new float[bodies.Count];

        for (int i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body.bodyType == BodyType.Star || body.bodyType == BodyType.GasGiant) continue;

            float maxAlt = 1f;
            foreach (var topo in body.localViewData.topologies)
            {
                if (topo.altitude > maxAlt) maxAlt = topo.altitude;
            }
            maxAltitudes[simDataList.Count] = maxAlt;

            NativeArray<float> initialRain = new NativeArray<float>(body.localViewData.topologies.Length, Allocator.Persistent);
            NativeArray<float> blurBuffer = new NativeArray<float>(body.localViewData.topologies.Length, Allocator.Persistent);

            for (int j = 0; j < initialRain.Length; j++) initialRain[j] = body.localViewData.topologies[j].rainFactor;

            simDataList.Add(new PlanetSimulationData
            {
                body = body,
                meshData = body.localViewData,
                topologies = new NativeArray<CellTopology>(body.localViewData.topologies, Allocator.Persistent),
                climates = new NativeArray<CellClimate>(body.localViewData.climates, Allocator.Persistent),
                terrainVisuals = new NativeArray<TerrainVisualData>(body.localViewData.terrainVisuals, Allocator.Persistent),

                neighborOffsets = new NativeArray<int>(body.geometryTemplate.neighborOffsets, Allocator.Persistent),
                neighborCounts = new NativeArray<byte>(body.geometryTemplate.neighborCounts, Allocator.Persistent),
                neighbors = new NativeArray<int>(body.geometryTemplate.neighbors, Allocator.Persistent),

                rainFactors = initialRain,
                blurredRainFactors = blurBuffer
            });
        }

        try
        {
            double starSolarMasses = SystemDataGenerator.Instance.star.massKg / AstroMath.SOLAR_MASS_KG;
            double starLuminosity = AstroMath.CalculateLuminosity(starSolarMasses);

            int phase1Cycles = cycles / 2;
            await RunSimulationCycles(simDataList, bodies, maxAltitudes, phase1Cycles, "Phase 1 (Dry)", onProgress);

            onProgress?.Invoke("Phase 2: Seeding Oceans...");
            for (int s = 0; s < simDataList.Count; s++)
            {
                var simData = simDataList[s];

                float minT = float.MaxValue;
                float maxT = float.MinValue;
                for (int i = 0; i < simData.climates.Length; i++)
                {
                    float t = simData.climates[i].localTemperature;
                    if (t < minT) minT = t;
                    if (t > maxT) maxT = t;
                }
                simData.body.globalMinTemperature = minT;
                simData.body.globalMaxTemperature = maxT;

                bool seeded = SystemDataGenerator.Instance.TrySeedOceans(simData.body, maxAltitudes[s]);
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

            int phase3Cycles = cycles - phase1Cycles;
            await RunSimulationCycles(simDataList, bodies, maxAltitudes, phase3Cycles, "Phase 3 (Wet)", onProgress);

            foreach (var simData in simDataList)
            {
                float minT = float.MaxValue;
                float maxT = float.MinValue;

                int oceanCount = 0;
                int iceCount = 0;
                int snowCount = 0;
                int bioCount = 0;
                int barrenCount = 0;

                for (int i = 0; i < simData.climates.Length; i++)
                {
                    CellClimate clim = simData.climates[i];

                    float t = clim.localTemperature;
                    if (t < minT) minT = t;
                    if (t > maxT) maxT = t;

                    if (simData.body.waterLevel > -5000f && simData.topologies[i].altitude < simData.body.waterLevel)
                    {
                        if (clim.iceCover > 0.5f) iceCount++;
                        else oceanCount++;
                    }
                    else
                    {
                        if (clim.iceCover > 0.5f) snowCount++;
                        else if (clim.biomass > 0.5f) bioCount++;
                        else barrenCount++;
                    }
                }

                simData.body.globalMinTemperature = minT;
                simData.body.globalMaxTemperature = maxT;

                float totalCells = simData.climates.Length;
                simData.body.globalOceanCoverage = oceanCount / totalCells;
                simData.body.globalIceCoverage = iceCount / totalCells;
                simData.body.globalSnowCoverage = snowCount / totalCells;
                simData.body.globalBiomassCoverage = bioCount / totalCells;
                simData.body.globalDesertCoverage = barrenCount / totalCells;
            }
        }
        finally
        {
            foreach (var simData in simDataList) simData.Dispose();
        }

        Debug.Log("Climate Equilibrium Resolved!");

        GeopoliticsGenerator.GenerateNations(bodies);

        if (MapModeManager.Instance != null)
        {
            MapModeManager.Instance.ApplyModeToAllPlanets();
        }
    }

    private async Task RunSimulationCycles(List<PlanetSimulationData> simDataList, List<CelestialBody> bodies, float[] maxAltitudes, int cycles, string phaseName, Action<string> onProgress)
    {
        double starSolarMasses = SystemDataGenerator.Instance.star.massKg / AstroMath.SOLAR_MASS_KG;
        double starLuminosity = AstroMath.CalculateLuminosity(starSolarMasses);

        for (int i = 0; i < cycles; i++)
        {
            NativeList<JobHandle> moistureHandles = new NativeList<JobHandle>(simDataList.Count, Allocator.Temp);

            for (int s = 0; s < simDataList.Count; s++)
            {
                var simData = simDataList[s];
                if (simData.body.HasCoastlineChanged(simData.body.waterLevel))
                {
                    CalculateMoistureJob mJob = new CalculateMoistureJob
                    {
                        topologies = simData.topologies,
                        neighborOffsets = simData.neighborOffsets,
                        neighborCounts = simData.neighborCounts,
                        neighbors = simData.neighbors,
                        rainFactors = simData.rainFactors,
                        waterLevel = simData.body.waterLevel,
                        isTidallyLocked = simData.body.isTidallyLocked,
                        sunDirection = Vector3.right
                    };
                    JobHandle mHandle = mJob.Schedule(simData.topologies.Length, 64);

                    JobHandle bHandle1 = new BlurMoistureJob { inputRainFactors = simData.rainFactors, neighborOffsets = simData.neighborOffsets, neighborCounts = simData.neighborCounts, neighbors = simData.neighbors, outputRainFactors = simData.blurredRainFactors }.Schedule(simData.topologies.Length, 64, mHandle);
                    JobHandle bHandle2 = new BlurMoistureJob { inputRainFactors = simData.blurredRainFactors, neighborOffsets = simData.neighborOffsets, neighborCounts = simData.neighborCounts, neighbors = simData.neighbors, outputRainFactors = simData.rainFactors }.Schedule(simData.topologies.Length, 64, bHandle1);
                    JobHandle bHandle3 = new BlurMoistureJob { inputRainFactors = simData.rainFactors, neighborOffsets = simData.neighborOffsets, neighborCounts = simData.neighborCounts, neighbors = simData.neighbors, outputRainFactors = simData.blurredRainFactors }.Schedule(simData.topologies.Length, 64, bHandle2);

                    moistureHandles.Add(bHandle3);

                    simData.body.lastCalculatedWaterLevel = simData.body.waterLevel;
                }
            }

            if (moistureHandles.Length > 0)
            {
                JobHandle.CompleteAll(moistureHandles.AsArray());

                for (int s = 0; s < simDataList.Count; s++)
                {
                    if (simDataList[s].body.lastCalculatedWaterLevel == simDataList[s].body.waterLevel)
                    {
                        simDataList[s].blurredRainFactors.CopyTo(simDataList[s].rainFactors);
                    }
                }
            }
            moistureHandles.Dispose();

            NativeList<JobHandle> jobHandles = new NativeList<JobHandle>(simDataList.Count, Allocator.Temp);

            for (int s = 0; s < simDataList.Count; s++)
            {
                var simData = simDataList[s];
                CelestialBody body = simData.body;

                float totalAlbedo = 0f;
                float oceanArea = 0f;
                float frozenOceanArea = 0f;

                for (int c = 0; c < simData.climates.Length; c++)
                {
                    CellClimate clim = simData.climates[c];

                    if (body.waterLevel > -5000f && simData.topologies[c].altitude < body.waterLevel)
                    {
                        if (clim.iceCover > 0.5f) { totalAlbedo += 0.6f; frozenOceanArea += 1f; }
                        else { totalAlbedo += 0.1f; oceanArea += 1f; }
                    }
                    else
                    {
                        if (clim.iceCover > 0.5f) totalAlbedo += 0.6f;
                        else if (clim.biomass > 0.5f) totalAlbedo += 0.15f;
                        else totalAlbedo += 0.25f;
                    }
                }
                float surfaceAlbedo = totalAlbedo / simData.climates.Length;

                float atmosThickness = Mathf.Clamp01((float)body.surfacePressureAtm / 2.0f);
                float globalAlbedo = Mathf.Lerp(surfaceAlbedo, 0.3f, atmosThickness);

                float effectiveOceanFraction = (oceanArea + (frozenOceanArea * 0.15f)) / simData.climates.Length;
                float oceanCurve = Mathf.Sqrt(Mathf.Clamp01(effectiveOceanFraction * 2.0f));

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

                float rainStrength = oceanCurve * tempFactor * pressureFactor;

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

                PlanetClimateState state = new PlanetClimateState
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
                    maxAltitude = maxAltitudes[s],
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

                ClimateEquilibriumJob job = new ClimateEquilibriumJob
                {
                    topologies = simData.topologies,
                    rainFactors = simData.rainFactors,
                    climates = simData.climates,
                    terrainVisuals = simData.terrainVisuals,
                    state = state
                };

                jobHandles.Add(job.Schedule(simData.topologies.Length, 64));
            }

            JobHandle.CompleteAll(jobHandles.AsArray());
            jobHandles.Dispose();

            if (i % 10 == 0 || i == cycles - 1)
            {
                onProgress?.Invoke($"Resolving Climate {phaseName}: Cycle {i + 1} / {cycles}");

                foreach (var simData in simDataList)
                {
                    UpdateVisuals(simData);
                    simData.terrainVisuals.CopyTo(simData.meshData.terrainVisuals);
                    simData.climates.CopyTo(simData.meshData.climates);

                    for (int j = 0; j < simData.topologies.Length; j++)
                    {
                        var t = simData.topologies[j];
                        t.rainFactor = simData.rainFactors[j];
                        simData.topologies[j] = t;
                    }
                    simData.topologies.CopyTo(simData.meshData.topologies);

                    TerrainVisualData[] highResArray = simData.body.localViewData.terrainVisuals;
                    TerrainVisualData[] lowResArray = simData.body.systemViewData.terrainVisuals;
                    int[] map = simData.body.lowToHighMap;

                    for (int j = 0; j < lowResArray.Length; j++) lowResArray[j] = highResArray[map[j]];
                }

                foreach (var body in bodies)
                {
                    if (body.visualObject != null)
                    {
                        Planet p = body.visualObject.GetComponent<Planet>();
                        if (p != null) p.UpdateTerrainBuffer();
                    }
                }
                await Task.Yield();
            }
        }
    }

    private void UpdateVisuals(PlanetSimulationData simData)
    {
        Color iceColor = simData.body.oceanLiquid != null ? simData.body.oceanLiquid.iceColor : Color.white;

        for (int i = 0; i < simData.terrainVisuals.Length; i++)
        {
            TerrainVisualData vis = simData.terrainVisuals[i];
            CellClimate clim = simData.climates[i];

            vis.surfaceData = new Vector4(clim.iceCover, clim.biomass, clim.liquidDepth, 0f);

            vis.iceColorR = iceColor.r;
            vis.iceColorG = iceColor.g;
            vis.iceColorB = iceColor.b;

            simData.terrainVisuals[i] = vis;
        }
    }
}