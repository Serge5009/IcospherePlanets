using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class Planet : MonoBehaviour
{
    public static HashSet<Planet> ActivePlanets = new HashSet<Planet>();

    public CelestialBody bodyData;
    public PlanetMeshData meshData;

    private ComputeBuffer terrainBuffer;
    private ComputeBuffer overlayBuffer;
    private ComputeBuffer politicalBuffer;
    private ComputeBuffer windBuffer;

    public MeshRenderer terrainRenderer;
    public MeshRenderer overlayRenderer;
    public MeshRenderer politicalRenderer;

    private MaterialPropertyBlock terrainBlock;
    private MaterialPropertyBlock overlayBlock;
    private MaterialPropertyBlock polBlock;

    private int currentHoveredCellId = -1;

    private void OnEnable() { ActivePlanets.Add(this); }
    private void OnDisable() { ActivePlanets.Remove(this); }

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
        overlayRenderer.enabled = true;

        GameObject polObj = new GameObject("Political Shell");
        polObj.transform.SetParent(transform);
        polObj.transform.localPosition = Vector3.zero;
        polObj.transform.localScale = Vector3.one * 1.002f;
        MeshFilter polFilter = polObj.AddComponent<MeshFilter>();
        polFilter.sharedMesh = data.sharedMesh;
        politicalRenderer = polObj.AddComponent<MeshRenderer>();
        politicalRenderer.sharedMaterial = SystemDisplayManager.Instance.politicalMaterial;
        politicalRenderer.enabled = false;

        if (body.variantIndex == 0)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = 1f;
        }
        else
        {
            MeshCollider mc = gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = data.sharedMesh;
        }

        CelestialBodyLink link = gameObject.AddComponent<CelestialBodyLink>();
        link.body = body;

        terrainBuffer = new ComputeBuffer(data.terrainVisuals.Length, Marshal.SizeOf(typeof(TerrainVisualData)));
        terrainBuffer.SetData(data.terrainVisuals);

        overlayBuffer = new ComputeBuffer(data.overlayVisuals.Length, Marshal.SizeOf(typeof(OverlayVisualData)));
        overlayBuffer.SetData(data.overlayVisuals);

        politicalBuffer = new ComputeBuffer(data.politicalVisuals.Length, Marshal.SizeOf(typeof(PoliticalVisualData)));
        politicalBuffer.SetData(data.politicalVisuals);

        windBuffer = new ComputeBuffer(data.windVisuals.Length, Marshal.SizeOf(typeof(WindVisualData)));
        windBuffer.SetData(data.windVisuals);

        terrainBlock = new MaterialPropertyBlock();
        terrainBlock.SetBuffer("_TerrainVisualData", terrainBuffer);
        terrainRenderer.SetPropertyBlock(terrainBlock);

        overlayBlock = new MaterialPropertyBlock();
        overlayBlock.SetBuffer("_OverlayVisualData", overlayBuffer);
        overlayRenderer.SetPropertyBlock(overlayBlock);

        polBlock = new MaterialPropertyBlock();
        polBlock.SetBuffer("_PoliticalVisualData", politicalBuffer);
        politicalRenderer.SetPropertyBlock(polBlock);

        bool isDistorted = body.variantIndex > 0;

        if (!isDistorted && (body.surfacePressureAtm >= 0.05 || body.bodyType == BodyType.Star) && SystemDisplayManager.Instance.atmosphereMaterial != null)
        {
            Mesh atmosMesh = isLocalView ? SystemDisplayManager.Instance.highResAtmosphereMesh : SystemDisplayManager.Instance.lowResAtmosphereMesh;

            if (atmosMesh == null)
            {
                GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                atmosMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tempSphere);
            }

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

                bool showAtmos = MapModeManager.Instance != null ? MapModeManager.Instance.ShowAtmosphere : true;
                atmosObj.SetActive(showAtmos);

                if (body.atmosphereVisualOpacity >= 0.99f && showAtmos)
                {
                    terrainRenderer.enabled = false;
                    overlayRenderer.enabled = false;
                    politicalRenderer.enabled = false;
                }
            }
        }

        if (MapModeManager.Instance != null)
        {
            MapModeManager.Instance.OnAtmosphereToggled += HandleAtmosphereToggled;
        }
    }

    private void Update()
    {
        if (bodyData != null && bodyData.isVisualsDirty)
        {
            UpdateTerrainBuffer();
            bodyData.isVisualsDirty = false;
        }
    }

    private void HandleAtmosphereToggled(bool show)
    {
        if (bodyData.atmosphereObject != null)
        {
            bodyData.atmosphereObject.SetActive(show);

            if (bodyData.atmosphereVisualOpacity >= 0.99f)
            {
                terrainRenderer.enabled = !show;
                overlayRenderer.enabled = !show;

                bool isPolitical = MapModeManager.Instance != null && MapModeManager.Instance.ActiveMode != null && MapModeManager.Instance.ActiveMode.modeType == MapModeType.Political;
                politicalRenderer.enabled = !show && isPolitical;
            }
        }
    }

    public void SetHoveredCell(int cellId)
    {
        if (currentHoveredCellId == cellId) return;

        bool isGradientMode = MapModeManager.Instance != null &&
                              MapModeManager.Instance.ActiveMode != null &&
                              MapModeManager.Instance.ActiveMode.modeType == MapModeType.Gradient;

        float baseAlpha = isGradientMode ? 0.85f : 0f;

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

    public void FastUpdateWaterVisuals()
    {
        if (bodyData == null) return;

        float wl = bodyData.waterLevel;
        float freezePt = bodyData.oceanLiquid != null ? bodyData.oceanLiquid.baseFreezingPointKelvin : 273.15f;
        bool isVacuum = bodyData.surfacePressureAtm < 0.05f;

        UpdateWaterArray(bodyData.localViewData, wl, freezePt, isVacuum);
        UpdateWaterArray(bodyData.systemViewData, wl, freezePt, isVacuum);

        foreach (Planet p in ActivePlanets)
        {
            if (p.bodyData == this.bodyData)
            {
                p.UpdateTerrainBuffer();
            }
        }
    }

    private void UpdateWaterArray(PlanetMeshData data, float wl, float freezePt, bool isVacuum)
    {
        if (data == null || data.topologies == null) return;

        for (int i = 0; i < data.topologies.Length; i++)
        {
            float alt = data.topologies[i].altitude;
            float depth = (wl > 0f && alt < wl) ? (wl - alt) / 1000f : 0f;

            data.climates[i].liquidDepth = depth;

            TerrainVisualData vis = data.terrainVisuals[i];
            vis.surfaceData.z = depth;

            if (isVacuum)
            {
                if (data.climates[i].localTemperature > freezePt)
                {
                    vis.surfaceData.z = 0f;
                    vis.surfaceData.x = 0f;
                }
                else if (depth > 0)
                {
                    vis.surfaceData.x = 1f;
                    vis.surfaceData.z = 0f;
                }
            }
            else
            {
                if (depth > 0 && data.climates[i].localTemperature < freezePt)
                {
                    vis.surfaceData.x = 1f;
                    vis.surfaceData.z = 0f;
                }
                else if (depth > 0)
                {
                    vis.surfaceData.x = 0f;
                }
            }

            if (bodyData.oceanLiquid != null)
            {
                vis.liquidColor = bodyData.oceanColor;
                vis.iceColorR = bodyData.oceanLiquid.iceColor.r;
                vis.iceColorG = bodyData.oceanLiquid.iceColor.g;
                vis.iceColorB = bodyData.oceanLiquid.iceColor.b;
            }

            data.terrainVisuals[i] = vis;
        }
    }

    public void UpdateTerrainBuffer()
    {
        if (terrainBuffer != null && terrainBlock != null)
        {
            terrainBuffer.SetData(meshData.terrainVisuals);
            terrainRenderer.SetPropertyBlock(terrainBlock);
        }
    }

    public void UpdateOverlayBuffer()
    {
        if (overlayBuffer != null && overlayBlock != null)
        {
            overlayBuffer.SetData(meshData.overlayVisuals);
            overlayRenderer.SetPropertyBlock(overlayBlock);
        }
    }

    public void UpdatePoliticalBuffer()
    {
        if (politicalBuffer != null && polBlock != null)
        {
            politicalBuffer.SetData(meshData.politicalVisuals);
            politicalRenderer.SetPropertyBlock(polBlock);
        }
    }

    private void OnDestroy()
    {
        if (MapModeManager.Instance != null)
        {
            MapModeManager.Instance.OnAtmosphereToggled -= HandleAtmosphereToggled;
        }

        if (terrainBuffer != null) terrainBuffer.Release();
        if (overlayBuffer != null) overlayBuffer.Release();
        if (politicalBuffer != null) politicalBuffer.Release();
        if (windBuffer != null) windBuffer.Release();
    }
}