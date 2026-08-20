using UnityEngine;

public class Planet : MonoBehaviour
{
    public CelestialBody bodyData;
    public PlanetMeshData meshData;

    private ComputeBuffer visualBuffer;
    public MeshRenderer terrainRenderer;
    public MeshRenderer politicalRenderer;

    public void InitializeFromData(CelestialBody body, PlanetMeshData data, Material terrainMat, Material polMat)
    {
        this.bodyData = body;
        this.meshData = data;

        MeshFilter filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = data.sharedMesh;

        terrainRenderer = gameObject.AddComponent<MeshRenderer>();
        terrainRenderer.sharedMaterial = terrainMat;

        GameObject polObj = new GameObject("Political Overlay");
        polObj.transform.SetParent(transform);
        polObj.transform.localPosition = Vector3.zero;
        polObj.transform.localScale = Vector3.one * 1.002f;

        MeshFilter polFilter = polObj.AddComponent<MeshFilter>();
        polFilter.sharedMesh = data.sharedMesh;

        politicalRenderer = polObj.AddComponent<MeshRenderer>();
        politicalRenderer.sharedMaterial = polMat;

        visualBuffer = new ComputeBuffer(data.visualDataArray.Length, 36);
        visualBuffer.SetData(data.visualDataArray);

        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        propBlock.SetBuffer("_CellVisualData", visualBuffer);

        terrainRenderer.SetPropertyBlock(propBlock);
        politicalRenderer.SetPropertyBlock(propBlock);
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