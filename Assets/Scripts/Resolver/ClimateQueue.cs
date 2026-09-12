using System.Collections.Generic;
using Unity.Collections;

public class PlanetSimulationData
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

public static class ClimateQueue
{
    public static Dictionary<CelestialBody, PlanetSimulationData> activeSimulations = new Dictionary<CelestialBody, PlanetSimulationData>();

    public static PlanetSimulationData InitializePlanet(CelestialBody body)
    {
        if (activeSimulations.ContainsKey(body))
        {
            activeSimulations[body].Dispose();
            activeSimulations.Remove(body);
        }

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

        activeSimulations.Add(body, simData);
        return simData;
    }

    public static void DisposeAll()
    {
        foreach (var sim in activeSimulations.Values)
        {
            sim.Dispose();
        }
        activeSimulations.Clear();
    }
}