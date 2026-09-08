using UnityEngine;

public class BarrenGenerator : IPlanetGenerator
{
    public PlanetMeshData Generate(Mesh mesh, Vector3[] cellCenters, CelestialBody body, float noiseScale, float noiseOffset, float waterLevel)
    {
        PlanetMeshData data = new PlanetMeshData();
        data.sharedMesh = mesh;
        int cellCount = cellCenters.Length;
        GeneratorUtility.InitializeDataArrays(data, cellCount);

        float[] rawAltitudes = new float[cellCount];
        float minAltitude = float.MaxValue;

        for (int i = 0; i < cellCount; i++)
        {
            Vector3 localPos = cellCenters[i];

            float baseAlt = (GeneratorUtility.FBM(localPos, noiseScale, noiseOffset, 4) - 0.5f) * 4000f;
            float ridges = GeneratorUtility.RidgedNoise(localPos, noiseScale * 3f, noiseOffset + 100f, 4) * 2000f;
            float rawAlt = baseAlt + ridges;

            rawAltitudes[i] = rawAlt;
            if (rawAlt < minAltitude) minAltitude = rawAlt;
        }

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

        for (int i = 0; i < cellCount; i++)
        {
            Vector3 localPos = cellCenters[i];
            float finalAltitude = rawAltitudes[i] - minAltitude;

            Vector3 normPos = localPos.normalized;
            float cosLat = Mathf.Sqrt(Mathf.Clamp01(1f - normPos.y * normPos.y));

            float insolation = Mathf.Lerp(poleInsolation, eqInsolation, cosLat);

            data.topologies[i] = new CellTopology
            {
                id = i,
                localPosition = localPos,
                altitude = finalAltitude,
                bedrockId = body.dominantBedrockId,
                windNeighborId = -1,
                baseInsolation = insolation,
                rainFactor = 0f
            };

            data.climates[i] = new CellClimate { localTemperature = 200f, storedHeat = 50f, moisture = 0f, snowDepth = 0f, iceCover = 0f, liquidDepth = 0f, biomass = 0f };
            data.economies[i] = new CellEconomy { ownerId = 0, population = 0, infrastructureCap = 0.1f, developmentCap = 0.05f };

            data.terrainVisuals[i] = new TerrainVisualData { bedrockColor = body.dominantBedrockColor, liquidColor = Vector4.zero, surfaceData = Vector4.zero };
            data.overlayVisuals[i] = new OverlayVisualData { overlayColor = new Vector4(0, 0, 0, 0) };
            data.politicalVisuals[i] = new PoliticalVisualData { politicalColor = new Vector4(0, 0, 0, 0) };
        }

        return data;
    }
}