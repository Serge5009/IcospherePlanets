using System;
using System.Collections.Generic;
using UnityEngine;

public class SystemDisplayManager : MonoBehaviour
{
    public static SystemDisplayManager Instance { get; private set; }

    [Header("True Scale Foundation")]
    public float trueScaleMultiplier = 0.0000001f;

    [Header("Distance Scaling (System View)")]
    public float linearDistanceMultiplier = 0.0000001f;
    public float logDistanceMultiplier = 5f;

    [Header("Size Scaling (Z-Lerp)")]
    public float maxLogSizeMultiplier = 1.5f;
    public float minClickableSizeUnity = 0.5f;
    public float starMaxScaleMultiplier = 0.05f;
    public AnimationCurve planetSizeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Visibility")]
    public bool showMoonsInSystemView = false;
    public bool drawTrailsForMinorBodies = false;

    [Header("Orbit Trails")]
    public Material trailMaterial;
    public int trailResolution = 60;

    [Tooltip("Multiplier for the logarithmic trail width")]
    public float trailWidthMultiplier = 0.2f;
    public float minTrailWidth = 0.02f;
    public float maxTrailWidth = 1.5f;

    [Header("Trail Colors")]
    [ColorUsage(true, true)]
    public Color defaultTrailColor = new Color(1f, 1f, 1f, 0.2f);

    [ColorUsage(true, true)]
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
        float currentZ = SpaceCameraController.Instance != null ? SpaceCameraController.Instance.currentZLevel : 1f;

        foreach (var body in SystemDataGenerator.Instance.allBodies)
        {
            if (body.visualObject != null && body.visualObject.activeSelf)
            {
                Vector3d pos = CalculateSystemViewPosition(body, currentTime);
                float radius = CalculateSystemViewRadius(body, currentZ);
                float scale = radius * 2f;

                body.visualObject.transform.position = pos.ToVector3();
                body.visualObject.transform.localScale = new Vector3(scale, scale, scale);
                body.visualObject.transform.rotation = Quaternion.Euler((float)body.axialTilt, body.currentRotationAngle, 0);

                if (body.parent != null && (!IsMinorBody(body) || drawTrailsForMinorBodies))
                {
                    UpdateDynamicOrbitTrail(body, radius, currentTime);
                }
            }
        }
    }

    public void SetSystemViewActive(bool isActive)
    {
        foreach (var body in SystemDataGenerator.Instance.allBodies)
        {
            if (body.visualObject != null)
            {
                if (isActive && body.bodyType == BodyType.Moon && !showMoonsInSystemView)
                    body.visualObject.SetActive(false);
                else
                    body.visualObject.SetActive(isActive);
            }
        }
    }

    private bool IsMinorBody(CelestialBody body)
    {
        return body.bodyType == BodyType.Asteroid || body.bodyType == BodyType.Comet;
    }

    public Vector3d CalculateSystemViewPosition(CelestialBody body, double time)
    {
        Vector3d absolutePos = body.GetAbsolutePosition(time);
        return absolutePos * trueScaleMultiplier;
    }

    public float CalculateSystemViewRadius(CelestialBody body, float zLevel)
    {
        float trueRadiusUnity = (float)(body.radiusKm * trueScaleMultiplier);

        float maxRadiusUnity = (float)(Math.Log10(body.radiusKm + 1) * maxLogSizeMultiplier);
        if (maxRadiusUnity < minClickableSizeUnity) maxRadiusUnity = minClickableSizeUnity;
        if (body.bodyType == BodyType.Star) maxRadiusUnity *= starMaxScaleMultiplier;

        float curveZ = planetSizeCurve.Evaluate(zLevel);
        return Mathf.Lerp(trueRadiusUnity, maxRadiusUnity, curveZ);
    }

    public float CalculateBaseSystemViewRadius(CelestialBody body)
    {
        return (float)(body.radiusKm * trueScaleMultiplier);
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

        if (body.parent != null && (!IsMinorBody(body) || drawTrailsForMinorBodies))
        {
            body.cachedOrbitPoints = KeplerMath.GetStaticOrbitPoints(body.orbit, trailResolution);

            LineRenderer lr = planetObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = trailResolution;

            lr.material = new Material(trailMaterial != null ? trailMaterial : new Material(Shader.Find("Sprites/Default")));

            lr.loop = true;

            if (body.parent.bodyType == BodyType.Star)
            {
                for (int i = 0; i < trailResolution; i++)
                {
                    Vector3 visualPos = (body.cachedOrbitPoints[i] * trueScaleMultiplier).ToVector3();
                    lr.SetPosition(i, visualPos);
                }
            }
        }

        body.visualObject = planetObj;

        if (body.bodyType == BodyType.Moon && !showMoonsInSystemView)
        {
            planetObj.SetActive(false);
        }
    }

    private void UpdateDynamicOrbitTrail(CelestialBody body, float currentVisualRadius, double currentTime)
    {
        LineRenderer lr = body.visualObject.GetComponent<LineRenderer>();
        if (lr == null) return;

        float logWidth = Mathf.Log10(currentVisualRadius + 1f) * trailWidthMultiplier;
        lr.widthMultiplier = Mathf.Clamp(logWidth, minTrailWidth, maxTrailWidth);

        Vector3d parentPos = body.parent.GetAbsolutePosition(currentTime);

        for (int i = 0; i < trailResolution; i++)
        {
            Vector3d absolutePos = parentPos + body.cachedOrbitPoints[i];
            Vector3 visualPos = (absolutePos * trueScaleMultiplier).ToVector3();
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

            if (state == CameraState.System || state == CameraState.Interstellar)
            {
                if (focus == null) show = !IsMinorBody(body);
                else
                {
                    if (body == focus) { show = true; highlight = true; }
                    else if (focus.bodyType == BodyType.Asteroid && body.orbitGroupName == focus.orbitGroupName) show = true;
                    else if (!IsMinorBody(body)) show = true;
                }
            }
            else
            {
                show = false;
            }

            lr.enabled = show;
            if (show)
            {
                Color c = highlight ? highlightTrailColor : defaultTrailColor;

                lr.startColor = c;
                lr.endColor = c;

                if (lr.material.HasProperty("_Color"))
                {
                    lr.material.SetColor("_Color", c);
                }

                if (lr.material.HasProperty("_EmissionColor"))
                {
                    lr.material.SetColor("_EmissionColor", c);
                }
            }
        }
    }
}