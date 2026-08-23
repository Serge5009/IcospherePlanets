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
}

[BurstCompile]
public struct ClimateEquilibriumJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<CellTopology> topologies;
    public NativeArray<CellClimate> climates;
    public NativeArray<CellVisualData> visuals;

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
            localTemp = baseTemp * (0.8f + 0.4f * topo.baseInsolation);
        }

        float altitudeAboveSeaLevel = Mathf.Max(0f, topo.altitude - state.waterLevel);
        if (altitudeAboveSeaLevel > 0)
        {
            localTemp -= (altitudeAboveSeaLevel / 1000f) * state.lapseRate;
        }

        float blendFactor = Mathf.Clamp01(state.atmosphericPressure / 5.0f);
        localTemp = Mathf.Lerp(localTemp, baseTemp, blendFactor);
        clim.localTemperature = localTemp;

        if (state.waterLevel > -5000f && topo.altitude < state.waterLevel)
        {
            clim.liquidDepth = (state.waterLevel - topo.altitude) / 1000f;
        }
        else
        {
            clim.liquidDepth = 0f;
        }

        if (clim.liquidDepth > 0) clim.moisture = 1.0f;
        else clim.moisture = topo.rainFactor * state.globalRainStrength;

        if (localTemp < state.freezingPoint && state.waterLevel > -5000f && state.atmosphericPressure >= 0.05f)
        {
            float degreesBelow = state.freezingPoint - localTemp;
            float baselineFrost = degreesBelow * 0.02f;
            clim.snowDepth = Mathf.Max(baselineFrost, clim.moisture * degreesBelow * 0.1f);

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

        if (localTemp > 273.15f && localTemp < 323.15f && clim.moisture > 0.2f && clim.liquidDepth <= 0f)
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

        thickness -= topo.rainFactor * tAlt * 0.5f;
        thickness *= state.globalSoilThickness;
        thickness = Mathf.Clamp01(thickness);

        Vector4 bedrockCol = (topo.bedrockId == state.secondaryBedrockId) ? state.secondaryBedrockColor : state.dominantBedrockColor;
        Vector4 currentSoilCol = Vector4.Lerp(state.soilDryColor, state.soilWetColor, clim.moisture);
        Vector4 finalGroundCol = Vector4.Lerp(bedrockCol, currentSoilCol, thickness);

        CellVisualData vis = visuals[i];
        vis.bedrockColor = finalGroundCol;
        vis.surfaceData = new Vector4(clim.iceCover, clim.biomass, clim.liquidDepth, 0f);
        vis.iceColorR = state.iceColor.x;
        vis.iceColorG = state.iceColor.y;
        vis.iceColorB = state.iceColor.z;
        visuals[i] = vis;
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
        public NativeArray<CellVisualData> visuals;

        public void Dispose()
        {
            if (topologies.IsCreated) topologies.Dispose();
            if (climates.IsCreated) climates.Dispose();
            if (visuals.IsCreated) visuals.Dispose();
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

            simDataList.Add(new PlanetSimulationData
            {
                body = body,
                meshData = body.localViewData,
                topologies = new NativeArray<CellTopology>(body.localViewData.topologies, Allocator.Persistent),
                climates = new NativeArray<CellClimate>(body.localViewData.climates, Allocator.Persistent),
                visuals = new NativeArray<CellVisualData>(body.localViewData.visualDataArray, Allocator.Persistent)
            });
        }

        try
        {
            double starSolarMasses = SystemDataGenerator.Instance.star.massKg / AstroMath.SOLAR_MASS_KG;
            double starLuminosity = AstroMath.CalculateLuminosity(starSolarMasses);

            for (int i = 0; i < cycles; i++)
            {
                NativeList<JobHandle> jobHandles = new NativeList<JobHandle>(simDataList.Count, Allocator.Temp);

                for (int s = 0; s < simDataList.Count; s++)
                {
                    var simData = simDataList[s];
                    CelestialBody body = simData.body;

                    float totalAlbedo = 0f;
                    float oceanArea = 0f;

                    for (int c = 0; c < simData.climates.Length; c++)
                    {
                        CellClimate clim = simData.climates[c];
                        if (clim.iceCover > 0) totalAlbedo += 0.7f;
                        else if (clim.liquidDepth > 0) { totalAlbedo += 0.1f; oceanArea += 1f; }
                        else if (clim.biomass > 0) totalAlbedo += 0.15f;
                        else totalAlbedo += 0.3f;
                    }
                    float globalAlbedo = totalAlbedo / simData.climates.Length;
                    float oceanFraction = oceanArea / simData.climates.Length;

                    double distanceAU = body.orbit.semiMajorAxis / AstroMath.AU_TO_KM;
                    float blackbody = AstroMath.CalculateBlackbodyTemperature(starLuminosity, distanceAU, globalAlbedo);

                    float freezingPt = body.oceanLiquid != null ? body.oceanLiquid.baseFreezingPointKelvin : 273.15f;
                    float boilingPt = body.oceanLiquid != null ? body.oceanLiquid.baseBoilingPointKelvin : 373.15f;

                    float baseTemp = blackbody + body.greenhouseHeatContribution;
                    if (baseTemp > boilingPt && body.waterLevel > -5000f)
                    {
                        body.waterLevel = -9999f;
                    }

                    float dynamicLapseRate = (float)(body.surfaceGravity / 9.8) * 6.5f;
                    float tempFactor = Mathf.Clamp01(blackbody / 288f);
                    float pressureFactor = Mathf.Clamp01((float)body.surfacePressureAtm / 0.05f);
                    float rainStrength = oceanFraction * tempFactor * pressureFactor;

                    float globalSoil = body.soilBaseThickness * (1.0f + (float)body.surfacePressureAtm) * (float)(body.surfaceGravity / 9.8) * (1.0f + rainStrength);
                    globalSoil = Mathf.Clamp(globalSoil, 0.1f, 3.0f);

                    Color iceCol = body.oceanLiquid != null ? body.oceanLiquid.iceColor : Color.white;

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
                        iceColor = new Vector3(iceCol.r, iceCol.g, iceCol.b)
                    };

                    ClimateEquilibriumJob job = new ClimateEquilibriumJob
                    {
                        topologies = simData.topologies,
                        climates = simData.climates,
                        visuals = simData.visuals,
                        state = state
                    };

                    jobHandles.Add(job.Schedule(simData.topologies.Length, 64));
                }

                JobHandle.CompleteAll(jobHandles.AsArray());
                jobHandles.Dispose();

                if (i % 10 == 0 || i == cycles - 1)
                {
                    onProgress?.Invoke($"Resolving Climate: Cycle {i + 1} / {cycles}");

                    foreach (var simData in simDataList)
                    {
                        simData.visuals.CopyTo(simData.meshData.visualDataArray);
                        simData.climates.CopyTo(simData.meshData.climates);

                        CellVisualData[] highResArray = simData.body.localViewData.visualDataArray;
                        CellVisualData[] lowResArray = simData.body.systemViewData.visualDataArray;
                        int[] map = simData.body.lowToHighMap;

                        for (int j = 0; j < lowResArray.Length; j++) lowResArray[j] = highResArray[map[j]];
                    }

                    foreach (var body in bodies)
                    {
                        if (body.visualObject != null)
                        {
                            Planet p = body.visualObject.GetComponent<Planet>();
                            if (p != null) p.UpdateVisualBuffer();
                        }
                    }
                    await Task.Yield();
                }
            }
        }
        finally
        {
            foreach (var simData in simDataList) simData.Dispose();
        }

        Debug.Log("Climate Equilibrium Resolved!");
    }
}