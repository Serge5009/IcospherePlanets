using System.Collections.Generic;
using UnityEngine;

public class ViewManager : MonoBehaviour
{
    public static ViewManager Instance { get; private set; }

    [Header("Local View Settings")]
    public float normalizedPlanetRadius = 1f;
    public float maxProxyDistance = 15000f;

    private GameObject currentLocalPlanet;
    private CelestialBody currentFocusedBody;
    private List<(GameObject obj, CelestialBody body, LineRenderer trail)> proxyBodies = new List<(GameObject, CelestialBody, LineRenderer)>();

    private bool isTopocentric = false;
    private float topocentricWeight = 0f;
    private float visualPlanetAngle = 0f;
    private float visualSkyAngle = 0f;
    private Quaternion skyRotationOffset = Quaternion.identity;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Update()
    {
        if (currentLocalPlanet != null && currentFocusedBody != null && TimeManager.Instance != null)
        {
            float targetWeight = isTopocentric ? 1f : 0f;
            topocentricWeight = Mathf.MoveTowards(topocentricWeight, targetWeight, Time.deltaTime * 2f);

            float deltaAngle = currentFocusedBody.deltaRotationAngle;
            visualPlanetAngle += deltaAngle * (1f - topocentricWeight);
            visualSkyAngle -= deltaAngle * topocentricWeight;

            currentLocalPlanet.transform.rotation = Quaternion.Euler((float)currentFocusedBody.axialTilt, visualPlanetAngle, 0);
            skyRotationOffset = Quaternion.Euler(0, visualSkyAngle, 0);

            UpdateProxyBodies(TimeManager.Instance.totalSeconds);
        }
    }

    public void TransitionToLocalView(CelestialBody body)
    {
        if (!body.isHighResReady) return;

        currentFocusedBody = body;

        visualPlanetAngle = body.currentRotationAngle;
        visualSkyAngle = 0f;
        topocentricWeight = 0f;
        isTopocentric = false;

        if (currentLocalPlanet != null) Destroy(currentLocalPlanet);

        currentLocalPlanet = new GameObject($"{body.name} (Local View)");
        currentLocalPlanet.transform.position = Vector3.zero;

        float localScale = normalizedPlanetRadius * 2f;
        currentLocalPlanet.transform.localScale = new Vector3(localScale, localScale, localScale);

        Planet planet = currentLocalPlanet.AddComponent<Planet>();
        planet.InitializeFromData(body, body.localViewData, SystemDisplayManager.Instance.terrainMaterial, SystemDisplayManager.Instance.politicalMaterial);

        SystemDisplayManager.Instance.gameObject.SetActive(false);
        SpawnProxyBodies(body);

        SystemDisplayManager.Instance.UpdateTrailContext(body, CameraState.PlanetaryHigh);
    }

    public void TransitionToSystemView()
    {
        if (currentLocalPlanet != null) Destroy(currentLocalPlanet);

        foreach (var proxy in proxyBodies) Destroy(proxy.obj);
        proxyBodies.Clear();

        SystemDisplayManager.Instance.gameObject.SetActive(true);
        SystemDisplayManager.Instance.UpdateTrailContext(currentFocusedBody, CameraState.System);

        currentFocusedBody = null;
        isTopocentric = false;
    }

    public void SetTopocentricMode(bool isLowView)
    {
        isTopocentric = isLowView;
    }

    public Quaternion GetSkyRotationOffset()
    {
        return skyRotationOffset;
    }

    private void SpawnProxyBodies(CelestialBody focus)
    {
        List<CelestialBody> bodiesToSpawn = new List<CelestialBody>();

        CelestialBody star = focus;
        while (star.parent != null) star = star.parent;
        if (star != focus) bodiesToSpawn.Add(star);

        foreach (var moon in focus.orbitingBodies) bodiesToSpawn.Add(moon);

        if (focus.parent != null && focus.parent.bodyType != BodyType.Star)
        {
            bodiesToSpawn.Add(focus.parent);
            foreach (var sibling in focus.parent.orbitingBodies)
            {
                if (sibling != focus) bodiesToSpawn.Add(sibling);
            }
        }

        foreach (var body in bodiesToSpawn)
        {
            GameObject proxy = new GameObject($"{body.name} (Proxy)");
            Planet p = proxy.AddComponent<Planet>();
            p.InitializeFromData(body, body.systemViewData, SystemDisplayManager.Instance.terrainMaterial, SystemDisplayManager.Instance.politicalMaterial);

            SphereCollider collider = proxy.AddComponent<SphereCollider>();
            collider.radius = 1f;
            CelestialBodyLink link = proxy.AddComponent<CelestialBodyLink>();
            link.body = body;

            LineRenderer lr = null;
            if (body.parent == focus)
            {
                lr = proxy.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = SystemDisplayManager.Instance.trailResolution;
                lr.material = SystemDisplayManager.Instance.trailMaterial;
                lr.startWidth = 0.02f;
                lr.endWidth = 0.02f;
                lr.loop = true;
                lr.startColor = SystemDisplayManager.Instance.defaultTrailColor;
                lr.endColor = SystemDisplayManager.Instance.defaultTrailColor;
            }

            proxyBodies.Add((proxy, body, lr));
        }
    }

    private void UpdateProxyBodies(double time)
    {
        Vector3 focusSysPos = SystemDisplayManager.Instance.CalculateSystemViewPosition(currentFocusedBody, time).ToVector3();
        float focusSysRadius = SystemDisplayManager.Instance.CalculateBaseSystemViewRadius(currentFocusedBody);

        float scaleRatio = normalizedPlanetRadius / focusSysRadius;

        foreach (var proxy in proxyBodies)
        {
            Vector3 proxySysPos = SystemDisplayManager.Instance.CalculateSystemViewPosition(proxy.body, time).ToVector3();
            float proxySysRadius = SystemDisplayManager.Instance.CalculateBaseSystemViewRadius(proxy.body);

            Vector3 relativeSysPos = proxySysPos - focusSysPos;
            Vector3 localPos = relativeSysPos * scaleRatio;
            float localScale = proxySysRadius * scaleRatio * 2f;

            float dist = localPos.magnitude;
            if (dist > maxProxyDistance)
            {
                float shrinkFactor = maxProxyDistance / dist;
                localPos = localPos.normalized * maxProxyDistance;
                localScale *= shrinkFactor;
            }

            localPos = skyRotationOffset * localPos;

            proxy.obj.transform.position = localPos;
            proxy.obj.transform.localScale = new Vector3(localScale, localScale, localScale);

            Quaternion proxySelfRot = Quaternion.Euler((float)proxy.body.axialTilt, proxy.body.currentRotationAngle, 0);
            proxy.obj.transform.rotation = skyRotationOffset * proxySelfRot;

            if (proxy.trail != null && proxy.body.cachedOrbitPoints != null)
            {
                for (int i = 0; i < SystemDisplayManager.Instance.trailResolution; i++)
                {
                    Vector3 trailPos = (proxy.body.cachedOrbitPoints[i] * SystemDisplayManager.Instance.trueScaleMultiplier).ToVector3();
                    trailPos = (trailPos - focusSysPos) * scaleRatio;
                    trailPos = skyRotationOffset * trailPos;
                    proxy.trail.SetPosition(i, trailPos);
                }
            }
        }
    }
}