using UnityEngine;

public static class PlanetGenerator
{
    public static PlanetMeshData GeneratePlanetData(HexSphereTemplate template, float radiusKm, BodyType bodyType, float noiseScale, float noiseOffset, float waterLevel)
    {
        PlanetMeshData data = new PlanetMeshData();
        data.sharedMesh = template.bakedMesh;

        int cellCount = template.cellCenters.Length;
        data.cells = new Cell[cellCount];
        data.visualDataArray = new CellVisualData[cellCount];

        for (int i = 0; i < cellCount; i++)
        {
            Vector3 localPos = template.cellCenters[i];

            data.cells[i].id = i;
            data.cells[i].localPosition = localPos;

            float noiseVal = Noise3D.Evaluate(localPos, noiseScale, noiseOffset);
            data.cells[i].altitude = noiseVal * noiseVal * 22000f;
            data.cells[i].ownerId = 0;

            data.cells[i].currentBiome = EnvironmentManager.Instance.EvaluateBiome(data.cells[i].altitude, waterLevel, bodyType);

            data.visualDataArray[i].terrainColor = data.cells[i].currentBiome.biomeColor;

            data.visualDataArray[i].politicalColor = Color.clear;
            data.visualDataArray[i].isHovered = 0;
        }

        return data;
    }
}