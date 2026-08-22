using UnityEngine;

public class GasGiantGenerator : IPlanetGenerator
{
    public PlanetMeshData Generate(Mesh mesh, Vector3[] cellCenters, CelestialBody body, float noiseScale, float noiseOffset, float waterLevel)
    {
        PlanetMeshData data = new PlanetMeshData();
        data.sharedMesh = mesh;
        int cellCount = cellCenters.Length;
        GeneratorUtility.InitializeDataArrays(data, cellCount);

        for (int i = 0; i < cellCount; i++)
        {
            Vector3 localPos = cellCenters[i];

            data.topologies[i] = new CellTopology { id = i, localPosition = localPos, altitude = 0f, bedrockId = 0, windNeighborId = -1 };
            data.climates[i] = new CellClimate { localTemperature = 150f, storedHeat = 1000f, moisture = 0f, iceCover = 0f, liquidDepth = 0f, biomass = 0f };
            data.economies[i] = new CellEconomy { ownerId = 0, population = 0, infrastructureCap = 0f, developmentCap = 0f };

            float bandNoise = GeneratorUtility.FBM(new Vector3(0, localPos.y * 10f, 0), 1f, noiseOffset, 3);
            Color bandColor = Color.Lerp(new Color(0.8f, 0.7f, 0.6f), new Color(0.6f, 0.4f, 0.3f), bandNoise);

            data.visualDataArray[i] = new CellVisualData { bedrockColor = bandColor, liquidColor = Vector4.zero, surfaceData = Vector4.zero, isHovered = 0 };
        }

        return data;
    }
}