using System.Collections.Generic;
using UnityEngine;

public class ViewManager : MonoBehaviour
{
    public static ViewManager Instance { get; private set; }

    [Header("Local View Settings")]
    public float localViewUnityRadius = 500f;
    public float maxProxyDistance = 15000f;

    private GameObject currentLocalPlanet;
    private CelestialBody currentFocusedBody;
    private List<(GameObject obj, CelestialBody body)> proxyBodies = new List<(GameObject, CelestialBody)>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Update()
    {
        if (currentLocalPlanet != null && currentFocusedBody != null && TimeManager.Instance != null)
        {
            UpdateProxyBodies(TimeManager.Instance.totalSeconds);
        }
    }

    public void TransitionToLocalView(CelestialBody body)
    {
        if (!body.isHighResReady) return;

        currentFocusedBody = body;

        currentLocalPlanet = new GameObject($"{body.name} (Local View)");
        currentLocalPlanet.transform.position = Vector3.zero;

        float localScale = localViewUnityRadius * 2f;
        currentLocalPlanet.transform.localScale = new Vector3(localScale, localScale, localScale);

        Planet planet = currentLocalPlanet.AddComponent<Planet>();
        planet.InitializeFromData(body, body.localViewData, SystemDisplayManager.Instance.terrainMaterial, SystemDisplayManager.Instance.politicalMaterial);

        SystemDisplayManager.Instance.gameObject.SetActive(false);
        SpawnProxyBodies(body);

        SystemDisplayManager.Instance.UpdateTrailContext(body, CameraState.LocalView);
    }

    public void TransitionToSystemView()
    {
        if (currentLocalPlanet != null) Destroy(currentLocalPlanet);

        foreach (var proxy in proxyBodies) Destroy(proxy.obj);
        proxyBodies.Clear();

        SystemDisplayManager.Instance.gameObject.SetActive(true);

        SystemDisplayManager.Instance.UpdateTrailContext(currentFocusedBody, CameraState.SystemView);

        currentFocusedBody = null;
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
            proxyBodies.Add((proxy, body));
        }
    }

    private void UpdateProxyBodies(double time)
    {
        Vector3 focusSysPos = SystemDisplayManager.Instance.CalculateSystemViewPosition(currentFocusedBody, time).ToVector3();
        float focusSysRadius = SystemDisplayManager.Instance.CalculateSystemViewRadius(currentFocusedBody);

        float scaleRatio = localViewUnityRadius / focusSysRadius;

        foreach (var proxy in proxyBodies)
        {
            Vector3 proxySysPos = SystemDisplayManager.Instance.CalculateSystemViewPosition(proxy.body, time).ToVector3();
            float proxySysRadius = SystemDisplayManager.Instance.CalculateSystemViewRadius(proxy.body);

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

            proxy.obj.transform.position = localPos;
            proxy.obj.transform.localScale = new Vector3(localScale, localScale, localScale);
        }
    }
}