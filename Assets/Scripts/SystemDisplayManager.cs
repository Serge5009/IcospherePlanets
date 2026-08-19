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

    [Header("Dynamic Zoom Scaling")]
    public bool useDynamicZoomScaling = true;
    [Tooltip("How aggressively objects scale up as you zoom out")]
    public float dynamicZoomMultiplier = 0.5f;
    public float minDynamicScale = 1f;
    public float maxDynamicScale = 20f;

    [Header("Orbit Trails")]
    public Material trailMaterial;
    public int trailResolution = 60;
    public float trailWidthMultiplier = 0.5f;
    public float minTrailWidth = 0.05f;
    public float maxTrailWidth = 2.0f;

    [Header("Trail Colors")]
    public Color defaultTrailColor = new Color(1f, 1f, 1f, 0.2f);
    public Color highlightTrailColor = new Color(0f, 1f, 0.5f, 1f);

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

        float camDist = SpaceCameraController.Instance != null ? SpaceCameraController.Instance.currentDistance : 100f;
        float dynamicZoomFactor = 1f;

        if (useDynamicZoomScaling && SpaceCameraController.Instance.currentState == CameraState.SystemView)
        {
            dynamicZoomFactor = Mathf.Clamp(Mathf.Log10(camDist + 10f) * dynamicZoomMultiplier, minDynamicScale, maxDynamicScale);
        }

        foreach (var body in SystemDataGenerator.Instance.allBodies)
        {
            if (body.visualObject != null)
            {
                Vector3d pos = CalculateSystemViewPosition(body, currentTime);

                float baseRadius = CalculateBaseSystemViewRadius(body);
                float finalScale = baseRadius * 2f * dynamicZoomFactor;

                body.visualObject.transform.position = pos.ToVector3();
                body.visualObject.transform.localScale = new Vector3(finalScale, finalScale, finalScale);

                if (body.parent != null && body.parent.bodyType != BodyType.Star)
                {
                    UpdateDynamicOrbitTrail(body, baseRadius, currentTime, dynamicZoomFactor);
                }
            }
        }
    }

    public Vector3d CalculateScaledPosition(Vector3d absolutePos)
    {
        double distKm = absolutePos.magnitude;
        if (distKm == 0) return new Vector3d(0, 0, 0);

        double scaledDist = distanceScaleMode == ScaleMode.Linear
            ? distKm * linearDistanceMultiplier
            : Math.Log10(distKm + 1) * logDistanceMultiplier;

        return absolutePos.normalized * scaledDist;
    }

    public Vector3d CalculateSystemViewPosition(CelestialBody body, double time)
    {
        return CalculateScaledPosition(body.GetAbsolutePosition(time));
    }

    public float CalculateBaseSystemViewRadius(CelestialBody body)
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

        if (body.parent != null)
        {
            body.cachedOrbitPoints = KeplerMath.GetStaticOrbitPoints(body.orbit, trailResolution);

            LineRenderer lr = planetObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = trailResolution;
            lr.material = trailMaterial != null ? trailMaterial : new Material(Shader.Find("Sprites/Default"));

            lr.startWidth = 1f;
            lr.endWidth = 1f;
            lr.loop = true;

            lr.startColor = defaultTrailColor;
            lr.endColor = defaultTrailColor;

            if (body.parent.bodyType == BodyType.Star)
            {
                for (int i = 0; i < trailResolution; i++)
                {
                    Vector3 visualPos = CalculateScaledPosition(body.cachedOrbitPoints[i]).ToVector3();
                    lr.SetPosition(i, visualPos);
                }

                float baseRadius = CalculateBaseSystemViewRadius(body);
                float logWidth = Mathf.Log10(baseRadius + 1f) * trailWidthMultiplier;
                lr.widthMultiplier = Mathf.Clamp(logWidth, minTrailWidth, maxTrailWidth);
            }
        }

        body.visualObject = planetObj;
    }

    private void UpdateDynamicOrbitTrail(CelestialBody body, float baseRadius, double currentTime, float dynamicZoomFactor)
    {
        LineRenderer lr = body.visualObject.GetComponent<LineRenderer>();
        if (lr == null) return;

        float logWidth = Mathf.Log10(baseRadius + 1f) * trailWidthMultiplier;
        lr.widthMultiplier = Mathf.Clamp(logWidth, minTrailWidth, maxTrailWidth) * dynamicZoomFactor;

        Vector3d parentPos = body.parent.GetAbsolutePosition(currentTime);

        for (int i = 0; i < trailResolution; i++)
        {
            Vector3d absolutePos = parentPos + body.cachedOrbitPoints[i];
            Vector3 visualPos = CalculateScaledPosition(absolutePos).ToVector3();
            lr.SetPosition(i, visualPos);
        }
    }

    public void UpdateTrailContext(CelestialBody focus, CameraState state)
    {
        foreach (var body in SystemDataGenerator.Instance.allBodies)
        {
            if (body.visualObject == null) continue;
            LineRenderer lr = body.visualObject.GetComponent<LineRenderer>();
            if (lr == null) continue;

            bool show = false;
            bool highlight = false;

            if (state == CameraState.SystemView)
            {
                if (focus == null)
                {
                    show = (body.bodyType != BodyType.Asteroid);
                }
                else
                {
                    if (body == focus)
                    {
                        show = true;
                        highlight = true;
                    }
                    else if (focus.bodyType == BodyType.Asteroid && body.orbitGroupName == focus.orbitGroupName)
                    {
                        show = true;
                    }
                    else if (body.bodyType != BodyType.Asteroid)
                    {
                        show = true;
                    }
                }
            }
            else if (state == CameraState.LocalView)
            {
                if (body == focus)
                {
                    show = false;
                }
                else if (body.parent == focus)
                {
                    show = true;
                }
                else if (focus.parent != null && body.parent == focus.parent)
                {
                    show = true;
                }
            }

            lr.enabled = show;

            if (show)
            {
                Color c = highlight ? highlightTrailColor : defaultTrailColor;
                lr.startColor = c;
                lr.endColor = c;
            }
        }
    }
}