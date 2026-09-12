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