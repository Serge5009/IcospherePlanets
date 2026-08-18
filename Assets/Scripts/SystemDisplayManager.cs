using System;
using System.Collections.Generic;
using UnityEngine;

public class SystemDisplayManager : MonoBehaviour
{
    public static SystemDisplayManager Instance { get; private set; }

    public enum ScaleMode { Linear, Logarithmic }

    [Header("Distance Scaling (System View)")]
    public ScaleMode distanceScaleMode = ScaleMode.Linear;
    public float linearDistanceMultiplier = 0.0000001f;
    public float logDistanceMultiplier = 5f;

    [Header("Planet Size Scaling (System View)")]
    public ScaleMode sizeScaleMode = ScaleMode.Linear;
    public float linearSizeMultiplier = 0.0001f;
    public float logSizeMultiplier = 1.5f;
    public float starSizeMultiplier = 0.05f;

    [Header("Visuals")]
    public Material terrainMaterial;
    public Material politicalMaterial;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Update()
    {
        if (TimeManager.Instance == null || SystemDataGenerator.Instance == null) return;

        double currentTime = TimeManager.Instance.totalSeconds;

        foreach (var body in SystemDataGenerator.Instance.allBodies)
        {
            if (body.visualObject != null)
            {
                Vector3d pos = CalculateSystemViewPosition(body, currentTime);
                float scale = CalculateSystemViewRadius(body) * 2f;

                body.visualObject.transform.position = pos.ToVector3();
                body.visualObject.transform.localScale = new Vector3(scale, scale, scale);
            }
        }
    }

    public Vector3d CalculateSystemViewPosition(CelestialBody body, double time)
    {
        Vector3d absolutePos = body.GetAbsolutePosition(time);
        double distKm = absolutePos.magnitude;
        if (distKm == 0) return new Vector3d(0, 0, 0);

        double scaledDist = distanceScaleMode == ScaleMode.Linear
            ? distKm * linearDistanceMultiplier
            : Math.Log10(distKm + 1) * logDistanceMultiplier;

        return absolutePos.normalized * scaledDist;
    }

    public float CalculateSystemViewRadius(CelestialBody body)
    {
        double radiusKm = body.radiusKm;
        double scaledRadius = sizeScaleMode == ScaleMode.Linear
            ? radiusKm * linearSizeMultiplier
            : Math.Log10(radiusKm + 1) * logSizeMultiplier;

        float finalScale = (float)(scaledRadius * 2);
        if (body.bodyType == BodyType.Star) finalScale *= starSizeMultiplier;

        return finalScale / 2f;
    }

    public void SpawnVisualHexSphere(CelestialBody body)
    {
        GameObject planetObj = new GameObject(body.name);
        planetObj.transform.SetParent(this.transform);

        Planet planet = planetObj.AddComponent<Planet>();
        planet.InitializeFromData(body, body.systemViewData, terrainMaterial, politicalMaterial);

        SphereCollider collider = planetObj.AddComponent<SphereCollider>();
        collider.radius = 1f;

        CelestialBodyLink link = planetObj.AddComponent<CelestialBodyLink>();
        link.body = body;

        body.visualObject = planetObj;
    }
}