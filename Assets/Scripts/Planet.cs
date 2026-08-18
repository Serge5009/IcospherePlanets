using UnityEngine;

public struct CellVisualData
{
    public Vector4 terrainColor;
    public Vector4 politicalColor;
    public int isHovered;
}

public class Planet : MonoBehaviour
{
    [Header("Logical Data")]
    public string planetName;
    public BodyType bodyType;
    public float radiusKm;

    [Header("Environment")]
    [Range(0, 22000)]
    public float waterLevel = 0f;

    [Header("Visual Data")]
    public float unityRadius = 1000f;

    public Cell[] cells;

    private ComputeBuffer visualBuffer;
    private CellVisualData[] visualDataArray;

    public MeshRenderer terrainRenderer;
    public MeshRenderer politicalRenderer;

    private float noiseScale;
    private float noiseOffset;

    public void InitializeVisuals()
    {
        visualDataArray = new CellVisualData[cells.Length];

        noiseScale = Random.Range(1.5f, 3f);
        noiseOffset = Random.Range(0f, 10000f);

        if (bodyType == BodyType.RockyPlanet)
        {
            waterLevel = Random.value > 0.5f ? Random.Range(8000f, 15000f) : 0f;
        }
        else
        {
            waterLevel = 0f;
        }

        for (int i = 0; i < cells.Length; i++)
        {
            Vector3 normalizedPos = cells[i].localPosition.normalized;
            float noiseVal = Noise3D.Evaluate(normalizedPos, noiseScale, noiseOffset);

            cells[i].altitude = noiseVal * noiseVal * 22000f;

            cells[i].ownerId = 0;
            visualDataArray[i].politicalColor = GeopoliticsManager.Instance.GetNation(0).mapColor;
            visualDataArray[i].isHovered = 0;
        }

        UpdateBiomes();

        visualBuffer = new ComputeBuffer(cells.Length, 36);
        visualBuffer.SetData(visualDataArray);

        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        propBlock.SetBuffer("_CellVisualData", visualBuffer);

        if (terrainRenderer != null) terrainRenderer.SetPropertyBlock(propBlock);
        if (politicalRenderer != null) politicalRenderer.SetPropertyBlock(propBlock);
    }

    private void UpdateBiomes()
    {
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].currentBiome = EnvironmentManager.Instance.EvaluateBiome(cells[i].altitude, waterLevel, bodyType);
            visualDataArray[i].terrainColor = cells[i].currentBiome.biomeColor;
        }
    }

    private void OnDestroy()
    {
        if (visualBuffer != null)
        {
            visualBuffer.Release();
            visualBuffer = null;
        }
    }
}