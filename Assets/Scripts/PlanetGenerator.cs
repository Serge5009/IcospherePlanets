using UnityEngine;

public static class PlanetGenerator
{
    public static PlanetMeshData GeneratePlanetData(HexSphereTemplate template, float radiusKm, BodyType bodyType, float noiseScale, float noiseOffset, float waterLevel)
    {
        PlanetMeshData data = new PlanetMeshData();
        data.sharedMesh = template.bakedMesh;

        int cellCount = template.cellCenters.Length;

        data.topologies = new CellTopology[cellCount];
        data.climates = new CellClimate[cellCount];
        data.economies = new CellEconomy[cellCount];
        data.visualDataArray = new CellVisualData[cellCount];

        BedrockTemplate defaultBedrock = DataLibrary.Instance != null ? DataLibrary.Instance.GetBedrock(1) : null;
        LiquidTemplate defaultLiquid = DataLibrary.Instance != null ? DataLibrary.Instance.GetLiquid(1) : null;

        Color bedrockCol = defaultBedrock != null ? defaultBedrock.baseColor : Color.gray;
        Color liquidCol = defaultLiquid != null ? defaultLiquid.shallowColor : Color.blue;

        for (int i = 0; i < cellCount; i++)
        {
            Vector3 localPos = template.cellCenters[i];

            float noiseVal = Noise3D.Evaluate(localPos, noiseScale, noiseOffset);
            float altitude = noiseVal * noiseVal * 22000f;

            data.topologies[i] = new CellTopology
            {
                id = i,
                localPosition = localPos,
                altitude = altitude,
                bedrockId = 1,
                windNeighborId = -1
            };

            float liquidDepth = altitude < waterLevel ? (waterLevel - altitude) / 1000f : 0f;

            data.climates[i] = new CellClimate
            {
                localTemperature = 280f,
                storedHeat = 100f,
                moisture = liquidDepth > 0 ? 1f : 0f,
                iceCover = 0f,
                liquidDepth = Mathf.Clamp01(liquidDepth),
                biomass = 0f
            };

            data.economies[i] = new CellEconomy
            {
                ownerId = 0,
                population = 0,
                infrastructureCap = 0.1f,
                developmentCap = 0.05f
            };

            if (bodyType == BodyType.Star)
            {
                data.visualDataArray[i] = new CellVisualData
                {
                    bedrockColor = new Vector4(1f, 0.9f, 0.2f, 1f) * 2f,
                    liquidColor = Vector4.zero,
                    surfaceData = Vector4.zero,
                    isHovered = 0
                };
            }
            else
            {
                data.visualDataArray[i] = new CellVisualData
                {
                    bedrockColor = bedrockCol,
                    liquidColor = liquidCol,
                    surfaceData = new Vector4(
                        data.climates[i].iceCover,
                        data.climates[i].biomass,
                        data.climates[i].liquidDepth,
                        0f
                    ),
                    isHovered = 0
                };
            }
        }

        return data;
    }
}