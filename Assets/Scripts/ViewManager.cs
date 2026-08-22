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

            float camTilt = Mathf.Lerp(0f, (float)currentFocusedBody.axialTilt, topocentricWeight);
            float remainingPlanetTilt = (float)currentFocusedBody.axialTilt - camTilt;

            currentLocalPlanet.transform.rotation = Quaternion.Euler(0, 0, remainingPlanetTilt) * Quaternion.Euler(0, visualPlanetAngle, 0);

            skyRotationOffset = Quaternion.Euler(0, visualSkyAngle, 0) * Quaternion.Euler(0, 0, -camTilt);

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
        planet.InitializeFromData(body, body.localViewData, SystemDisplayManager.Instance.terrainMaterial, SystemDisplayManager.Instance.politicalMaterial, true);

        SystemDisplayManager.Instance.SetSystemViewActive(false);
        SpawnProxyBodies(body);

        SystemDisplayManager.Instance.UpdateTrailContext(body, CameraState.PlanetaryHigh);
    }

    public void TransitionToSystemView()
    {
        if (currentLocalPlanet != null) Destroy(currentLocalPlanet);

        foreach (var proxy in proxyBodies) Destroy(proxy.obj);
        proxyBodies.Clear();

        SystemDisplayManager.Instance.SetSystemViewActive(true);
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
            p.InitializeFromData(body, body.systemViewData, SystemDisplayManager.Instance.terrainMaterial, SystemDisplayManager.Instance.politicalMaterial, false);

            SphereCollider collider = proxy.AddComponent<SphereCollider>();
            collider.radius = 1f;
            CelestialBodyLink link = proxy.AddComponent<CelestialBodyLink>();
            link.body = body;

            LineRenderer lr = null;

            bool drawLocalTrail = false;
            if (focus.bodyType == BodyType.Moon)
            {
                if (body.parent == focus.parent && body != focus) drawLocalTrail = true;
            }
            else
            {
                if (body.parent == focus) drawLocalTrail = true;
            }

            if (drawLocalTrail)
            {
                lr = proxy.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = SystemDisplayManager.Instance.trailResolution;

                Material mat = SystemDisplayManager.Instance.trailMaterial != null ?
                    new Material(SystemDisplayManager.Instance.trailMaterial) :
                    new Material(Shader.Find("Sprites/Default"));
                lr.material = mat;

                lr.startWidth = 0.02f;
                lr.endWidth = 0.02f;
                lr.loop = true;

                Color c = SystemDisplayManager.Instance.defaultTrailColor;
                lr.startColor = c;
                lr.endColor = c;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", c);
            }

            proxyBodies.Add((proxy, body, lr));
        }
    }

    private void UpdateProxyBodies(double time)
    {
        Vector3d focusAbs = currentFocusedBody.GetAbsolutePosition(time);
        double localScaleFactor = normalizedPlanetRadius / currentFocusedBody.radiusKm;

        foreach (var proxy in proxyBodies)
        {
            Vector3d proxyAbs = proxy.body.GetAbsolutePosition(time);
            Vector3d relativeKm = proxyAbs - focusAbs;

            Vector3 localPos = (relativeKm * localScaleFactor).ToVector3();
            float localScale = (float)(proxy.body.radiusKm * localScaleFactor * 2.0);

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

            Quaternion proxySelfRot = Quaternion.Euler(0, 0, (float)proxy.body.axialTilt) * Quaternion.Euler(0, proxy.body.currentRotationAngle, 0);

            proxy.obj.transform.rotation = skyRotationOffset * proxySelfRot;

            if (proxy.trail != null && proxy.body.cachedOrbitPoints != null)
            {
                Vector3d parentAbsPos = proxy.body.parent.GetAbsolutePosition(time);
                Vector3d parentRelativeKm = parentAbsPos - focusAbs;
                Vector3 parentLocalPos = (parentRelativeKm * localScaleFactor).ToVector3();
                parentLocalPos = skyRotationOffset * parentLocalPos;

                for (int i = 0; i < SystemDisplayManager.Instance.trailResolution; i++)
                {
                    Vector3 trailPointLocal = (proxy.body.cachedOrbitPoints[i] * localScaleFactor).ToVector3();
                    trailPointLocal = skyRotationOffset * trailPointLocal;

                    proxy.trail.SetPosition(i, parentLocalPos + trailPointLocal);
                }
            }
        }
    }
}