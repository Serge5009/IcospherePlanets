using UnityEngine;
using System.Runtime.InteropServices;

public class Planet : MonoBehaviour
{
    public CelestialBody bodyData;
    public PlanetMeshData meshData;

    private ComputeBuffer visualBuffer;
    public MeshRenderer terrainRenderer;
    public MeshRenderer politicalRenderer;

    private int currentHoveredCellId = -1;

    public void InitializeFromData(CelestialBody body, PlanetMeshData data, Material terrainMat, Material polMat, bool isLocalView)
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

        politicalRenderer.enabled = isLocalView;

        SphereCollider sc = gameObject.GetComponent<SphereCollider>();
        if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
        sc.radius = 1f;

        CelestialBodyLink link = gameObject.GetComponent<CelestialBodyLink>();
        if (link == null)
        {
            link = gameObject.AddComponent<CelestialBodyLink>();
            link.body = body;
        }

        int stride = Marshal.SizeOf(typeof(CellVisualData));
        visualBuffer = new ComputeBuffer(data.visualDataArray.Length, stride);
        visualBuffer.SetData(data.visualDataArray);

        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        propBlock.SetBuffer("_CellVisualData", visualBuffer);

        terrainRenderer.SetPropertyBlock(propBlock);
        politicalRenderer.SetPropertyBlock(propBlock);

        if ((body.surfacePressureAtm >= 0.05 || body.bodyType == BodyType.Star) && SystemDisplayManager.Instance.atmosphereMaterial != null)
        {
            Mesh atmosMesh = isLocalView ? SystemDisplayManager.Instance.highResAtmosphereMesh : SystemDisplayManager.Instance.lowResAtmosphereMesh;

            if (atmosMesh != null)
            {
                GameObject atmosObj = new GameObject("Atmosphere Shell");
                atmosObj.transform.SetParent(transform);
                atmosObj.transform.localPosition = Vector3.zero;

                float scale = body.atmosphereVisualScale;
                atmosObj.transform.localScale = new Vector3(scale, scale, scale);

                MeshFilter atmosFilter = atmosObj.AddComponent<MeshFilter>();
                atmosFilter.sharedMesh = atmosMesh;

                MeshRenderer atmosRenderer = atmosObj.AddComponent<MeshRenderer>();
                atmosRenderer.sharedMaterial = SystemDisplayManager.Instance.atmosphereMaterial;

                MaterialPropertyBlock atmosBlock = new MaterialPropertyBlock();
                atmosBlock.SetColor("_SkyColor", body.atmosphereSkyColor);
                atmosBlock.SetColor("_CloudColor", body.atmosphereCloudColor);
                atmosBlock.SetFloat("_BaseOpacity", body.atmosphereVisualOpacity);
                atmosBlock.SetFloat("_CloudCoverage", body.atmosphereCloudCoverage);
                atmosBlock.SetFloat("_CloudScale", body.atmosphereCloudScale);
                atmosRenderer.SetPropertyBlock(atmosBlock);

                body.atmosphereObject = atmosObj;

                if (body.atmosphereVisualOpacity >= 0.99f)
                {
                    terrainRenderer.enabled = false;
                }
            }
        }
    }

    public void SetHoveredCell(int cellId)
    {
        if (currentHoveredCellId == cellId) return;

        if (currentHoveredCellId >= 0 && currentHoveredCellId < meshData.visualDataArray.Length)
        {
            meshData.visualDataArray[currentHoveredCellId].isHovered = 0;
        }

        currentHoveredCellId = cellId;
        if (currentHoveredCellId >= 0 && currentHoveredCellId < meshData.visualDataArray.Length)
        {
            meshData.visualDataArray[currentHoveredCellId].isHovered = 1;
        }

        UpdateVisualBuffer();
    }

    public void UpdateVisualBuffer()
    {
        if (visualBuffer != null && meshData != null && meshData.visualDataArray != null)
        {
            visualBuffer.SetData(meshData.visualDataArray);
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