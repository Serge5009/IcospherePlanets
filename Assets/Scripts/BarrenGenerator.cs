using UnityEngine;

public class BarrenGenerator : IPlanetGenerator
{
    public PlanetMeshData Generate(HexSphereTemplate template, CelestialBody body, float noiseScale, float noiseOffset, float waterLevel)
    {
        PlanetMeshData data = new PlanetMeshData();
        data.sharedMesh = template.bakedMesh;
        int cellCount = template.cellCenters.Length;
        GeneratorUtility.InitializeDataArrays(data, cellCount);

        BedrockTemplate defaultBedrock = DataLibrary.Instance != null ? DataLibrary.Instance.GetBedrock(1) : null;
        byte bedrockId = body.dominantBedrock != null ? body.dominantBedrock.bedrockId : (byte)1;
        Color bColor = body.dominantBedrock != null ? body.dominantBedrock.baseColor : (defaultBedrock != null ? defaultBedrock.baseColor : Color.gray);

        for (int i = 0; i < cellCount; i++)
        {
            Vector3 localPos = template.cellCenters[i];

            float baseAlt = (GeneratorUtility.FBM(localPos, noiseScale, noiseOffset, 4) - 0.5f) * 4000f;
            float ridges = GeneratorUtility.RidgedNoise(localPos, noiseScale * 3f, noiseOffset + 100f, 4) * 2000f;
            float altitude = baseAlt + ridges;

            data.topologies[i] = new CellTopology { id = i, localPosition = localPos, altitude = altitude, bedrockId = bedrockId, windNeighborId = -1 };
            data.climates[i] = new CellClimate { localTemperature = 200f, storedHeat = 50f, moisture = 0f, iceCover = 0f, liquidDepth = 0f, biomass = 0f };
            data.economies[i] = new CellEconomy { ownerId = 0, population = 0, infrastructureCap = 0.1f, developmentCap = 0.05f };
            data.visualDataArray[i] = new CellVisualData { bedrockColor = bColor, liquidColor = Vector4.zero, surfaceData = Vector4.zero, isHovered = 0 };
        }

        return data;
    }
}