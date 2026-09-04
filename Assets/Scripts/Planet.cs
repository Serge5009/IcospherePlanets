using UnityEngine;
using System.Runtime.InteropServices;

public class Planet : MonoBehaviour
{
    public CelestialBody bodyData;
    public PlanetMeshData meshData;

    private ComputeBuffer terrainBuffer;
    private ComputeBuffer overlayBuffer;
    private ComputeBuffer windBuffer;

    public MeshRenderer terrainRenderer;
    public MeshRenderer overlayRenderer;

    private int currentHoveredCellId = -1;

    public void InitializeFromData(CelestialBody body, PlanetMeshData data, Material terrainMat, Material overlayMat, bool isLocalView)
    {
        this.bodyData = body;
        this.meshData = data;

        MeshFilter filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = data.sharedMesh;

        terrainRenderer = gameObject.AddComponent<MeshRenderer>();
        terrainRenderer.sharedMaterial = terrainMat;

        GameObject overlayObj = new GameObject("Overlay Shell");
        overlayObj.transform.SetParent(transform);
        overlayObj.transform.localPosition = Vector3.zero;
        overlayObj.transform.localScale = Vector3.one * 1.001f;

        MeshFilter overlayFilter = overlayObj.AddComponent<MeshFilter>();
        overlayFilter.sharedMesh = data.sharedMesh;

        overlayRenderer = overlayObj.AddComponent<MeshRenderer>();
        overlayRenderer.sharedMaterial = overlayMat;

        overlayRenderer.enabled = isLocalView;

        SphereCollider sc = gameObject.GetComponent<SphereCollider>();
        if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
        sc.radius = 1f;

        CelestialBodyLink link = gameObject.GetComponent<CelestialBodyLink>();
        if (link == null)
        {
            link = gameObject.AddComponent<CelestialBodyLink>();
            link.body = body;
        }

        terrainBuffer = new ComputeBuffer(data.terrainVisuals.Length, Marshal.SizeOf(typeof(TerrainVisualData)));
        terrainBuffer.SetData(data.terrainVisuals);

        overlayBuffer = new ComputeBuffer(data.overlayVisuals.Length, Marshal.SizeOf(typeof(OverlayVisualData)));
        overlayBuffer.SetData(data.overlayVisuals);

        windBuffer = new ComputeBuffer(data.windVisuals.Length, Marshal.SizeOf(typeof(WindVisualData)));
        windBuffer.SetData(data.windVisuals);

        MaterialPropertyBlock terrainBlock = new MaterialPropertyBlock();
        terrainBlock.SetBuffer("_TerrainVisualData", terrainBuffer);
        terrainRenderer.SetPropertyBlock(terrainBlock);

        MaterialPropertyBlock overlayBlock = new MaterialPropertyBlock();
        overlayBlock.SetBuffer("_OverlayVisualData", overlayBuffer);
        overlayRenderer.SetPropertyBlock(overlayBlock);

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
                    overlayRenderer.enabled = false;
                }
            }
        }
    }

    public void SetHoveredCell(int cellId)
    {
        if (currentHoveredCellId == cellId) return;

        float baseAlpha = (MapModeManager.Instance != null && MapModeManager.Instance.ActiveMode != null) ? 0.85f : 0f;

        if (currentHoveredCellId >= 0 && currentHoveredCellId < meshData.overlayVisuals.Length)
        {
            Vector4 col = meshData.overlayVisuals[currentHoveredCellId].overlayColor;
            col.w = baseAlpha;
            meshData.overlayVisuals[currentHoveredCellId].overlayColor = col;
        }

        currentHoveredCellId = cellId;
        if (currentHoveredCellId >= 0 && currentHoveredCellId < meshData.overlayVisuals.Length)
        {
            Vector4 col = meshData.overlayVisuals[currentHoveredCellId].overlayColor;
            col.w = 1f;
            meshData.overlayVisuals[currentHoveredCellId].overlayColor = col;
        }

        UpdateOverlayBuffer();
    }

    public void UpdateTerrainBuffer()
    {
        if (terrainBuffer != null && meshData != null && meshData.terrainVisuals != null)
        {
            terrainBuffer.SetData(meshData.terrainVisuals);
        }
    }

    public void UpdateOverlayBuffer()
    {
        if (overlayBuffer != null && meshData != null && meshData.overlayVisuals != null)
        {
            overlayBuffer.SetData(meshData.overlayVisuals);
        }
    }

    private void OnDestroy()
    {
        if (terrainBuffer != null) terrainBuffer.Release();
        if (overlayBuffer != null) overlayBuffer.Release();
        if (windBuffer != null) windBuffer.Release();
    }
}