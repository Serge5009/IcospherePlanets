using System.Collections.Generic;
using UnityEngine;

public class ViewManager : MonoBehaviour
{
    public static ViewManager Instance { get; private set; }

    [Header("Local View Settings")]
    public float maxProxyDistance = 15000f;

    [Header("Dependencies")]
    public SystemGenerator systemGenerator;

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
        currentFocusedBody = body;

        currentLocalPlanet = new GameObject($"{body.name} (Local View)");
        currentLocalPlanet.transform.position = Vector3.zero;

        currentLocalPlanet.transform.localScale = new Vector3(1000f, 1000f, 1000f);

        Planet planet = currentLocalPlanet.AddComponent<Planet>();
        planet.InitializeFromData(body, body.localViewData, systemGenerator.terrainMaterial, systemGenerator.politicalMaterial);

        systemGenerator.gameObject.SetActive(false);

        SpawnProxyBodies(body);

        Debug.Log($"Transitioned to Local View: {body.name} at Sub {body.dataSubdivisions}");
    }

    public void TransitionToSystemView()
    {
        if (currentLocalPlanet != null) Destroy(currentLocalPlanet);

        foreach (var proxy in proxyBodies) Destroy(proxy.obj);
        proxyBodies.Clear();

        currentFocusedBody = null;
        systemGenerator.gameObject.SetActive(true);
        Debug.Log("Transitioned to System View");
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
            p.InitializeFromData(body, body.systemViewData, systemGenerator.terrainMaterial, systemGenerator.politicalMaterial);

            proxyBodies.Add((proxy, body));
        }
    }

    private void UpdateProxyBodies(double time)
    {
        Vector3d focusPos = currentFocusedBody.GetAbsolutePosition(time);

        double localScaleFactor = 1000f / currentFocusedBody.radiusKm;

        foreach (var proxy in proxyBodies)
        {
            Vector3d absolutePos = proxy.body.GetAbsolutePosition(time);
            Vector3d relativePosKm = absolutePos - focusPos;

            Vector3d localPos = relativePosKm * localScaleFactor;
            double dist = localPos.magnitude;

            double trueRadiusUnity = proxy.body.radiusKm * localScaleFactor;
            double visualScale = trueRadiusUnity * 2;

            if (dist > maxProxyDistance)
            {
                double shrinkFactor = maxProxyDistance / dist;
                localPos = localPos.normalized * maxProxyDistance;
                visualScale *= shrinkFactor;
            }

            proxy.obj.transform.position = localPos.ToVector3();
            proxy.obj.transform.localScale = new Vector3((float)visualScale, (float)visualScale, (float)visualScale);
        }
    }
}