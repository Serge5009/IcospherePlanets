using System;
using System.Collections.Generic;
using UnityEngine;

public class SystemGenerator : MonoBehaviour
{
    public enum ScaleMode { Linear, Logarithmic }

    [Header("Generation Settings")]
    public StarClassTemplate starClass;
    public string systemName = "Sol";
    public float rockBudgetEarths = 15f;
    public float gasBudgetEarths = 500f;

    [Header("Distance Scaling (System View)")]
    public ScaleMode distanceScaleMode = ScaleMode.Linear;
    [Tooltip("Multiplier if using Linear Distance (e.g., 1e-7)")]
    public float linearDistanceMultiplier = 0.0000001f;
    [Tooltip("Multiplier if using Logarithmic Distance (e.g., 5)")]
    public float logDistanceMultiplier = 5f;

    [Header("Planet Size Scaling (System View)")]
    public ScaleMode sizeScaleMode = ScaleMode.Logarithmic;
    [Tooltip("Multiplier if using Linear Size (e.g., 0.0001)")]
    public float linearSizeMultiplier = 0.0001f;
    [Tooltip("Multiplier if using Logarithmic Size (e.g., 1.5)")]
    public float logSizeMultiplier = 1.5f;

    [Header("Visuals")]
    public Material defaultPlanetMaterial;

    private CelestialBody star;
    private List<CelestialBody> allBodies = new List<CelestialBody>();

    private void Start()
    {
        GenerateSystem();
    }

    private void Update()
    {
        if (TimeManager.Instance == null || allBodies.Count == 0) return;

        double currentTime = TimeManager.Instance.totalSeconds;

        foreach (var body in allBodies)
        {
            UpdateVisuals(body, currentTime);
        }
    }

    public void GenerateSystem()
    {
        allBodies.Clear();

        float starMass = UnityEngine.Random.Range(starClass.minSolarMass, starClass.maxSolarMass);
        double starMassKg = starMass * AstroMath.SOLAR_MASS_KG;

        star = new CelestialBody(systemName + " Prime", BodyType.Star, starMassKg, 696340);
        SpawnVisualSphere(star);
        allBodies.Add(star);

        double luminosity = AstroMath.CalculateLuminosity(starMass);
        (double habInner, double habOuter) = AstroMath.CalculateHabitableZone(luminosity);
        double frostLine = AstroMath.CalculateFrostLine(luminosity);

        List<double> orbitalDistancesAU = GenerateOrbitalDistances(habInner);

        double currentRockBudget = rockBudgetEarths;
        double currentGasBudget = gasBudgetEarths;

        for (int i = 0; i < orbitalDistancesAU.Count; i++)
        {
            double distanceAU = orbitalDistancesAU[i];
            bool isInsideFrostLine = distanceAU < frostLine;

            double planetMassEarths = 0;
            BodyType type = BodyType.DwarfPlanet;
            double radiusKm = 5000;

            if (isInsideFrostLine)
            {
                if (currentRockBudget > 0.5)
                {
                    planetMassEarths = UnityEngine.Random.Range(0.1f, Mathf.Min(3f, (float)currentRockBudget));
                    currentRockBudget -= planetMassEarths;
                    type = planetMassEarths > 0.1 ? BodyType.RockyPlanet : BodyType.DwarfPlanet;
                    radiusKm = 6371 * Math.Pow(planetMassEarths, 0.33);
                }
            }
            else
            {
                if (currentGasBudget > 10)
                {
                    planetMassEarths = UnityEngine.Random.Range(10f, Mathf.Min(300f, (float)currentGasBudget));
                    currentGasBudget -= planetMassEarths;

                    double coreMass = UnityEngine.Random.Range(1f, 5f);
                    currentRockBudget -= coreMass;
                    planetMassEarths += coreMass;

                    type = planetMassEarths > 50 ? BodyType.GasGiant : BodyType.IceGiant;
                    radiusKm = 69911 * Math.Pow(planetMassEarths / 317.8, 0.33);
                }
                else if (currentRockBudget > 0.1)
                {
                    planetMassEarths = UnityEngine.Random.Range(0.01f, 0.5f);
                    currentRockBudget -= planetMassEarths;
                    type = BodyType.DwarfPlanet;
                    radiusKm = 2000;
                }
            }

            if (planetMassEarths > 0)
            {
                double massKg = planetMassEarths * AstroMath.EARTH_MASS_KG;
                CelestialBody planet = new CelestialBody($"{systemName} {i + 1}", type, massKg, radiusKm);

                OrbitalParameters parameters = new OrbitalParameters
                {
                    semiMajorAxis = distanceAU * AstroMath.AU_TO_KM,
                    eccentricity = UnityEngine.Random.Range(0.0f, 0.06f),
                    inclination = UnityEngine.Random.Range(-3f, 3f) * Mathf.Deg2Rad,
                    longitudeOfAscendingNode = UnityEngine.Random.Range(0f, 2f * Mathf.PI),
                    argumentOfPeriapsis = UnityEngine.Random.Range(0f, 2f * Mathf.PI),
                    meanAnomalyAtEpoch = UnityEngine.Random.Range(0f, 2f * Mathf.PI)
                };

                star.AddOrbitingBody(planet, parameters);
                SpawnVisualSphere(planet);
                allBodies.Add(planet);
            }
        }
    }

    private List<double> GenerateOrbitalDistances(double habInner)
    {
        List<double> distances = new List<double>();
        double currentDistance = UnityEngine.Random.Range(0.2f, (float)habInner);
        int planetCount = UnityEngine.Random.Range(5, 11);

        for (int i = 0; i < planetCount; i++)
        {
            distances.Add(currentDistance);
            currentDistance *= UnityEngine.Random.Range(1.4f, 2.0f);
        }
        return distances;
    }

    private void SpawnVisualSphere(CelestialBody body)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = body.name;
        sphere.transform.SetParent(this.transform);

        if (defaultPlanetMaterial != null)
        {
            sphere.GetComponent<MeshRenderer>().sharedMaterial = defaultPlanetMaterial;
        }

        body.visualObject = sphere;
    }

    private void UpdateVisuals(CelestialBody body, double time)
    {
        if (body.visualObject == null) return;

        Vector3d absolutePos = body.GetAbsolutePosition(time);
        double distKm = absolutePos.magnitude;

        if (distKm > 0)
        {
            double scaledDist = distanceScaleMode == ScaleMode.Linear
                ? distKm * linearDistanceMultiplier
                : Math.Log10(distKm + 1) * logDistanceMultiplier;

            Vector3d scaledPos = absolutePos.normalized * scaledDist;
            body.visualObject.transform.position = scaledPos.ToVector3();
        }
        else
        {
            body.visualObject.transform.position = Vector3.zero;
        }

        double radiusKm = body.radiusKm;
        double scaledRadius = sizeScaleMode == ScaleMode.Linear
            ? radiusKm * linearSizeMultiplier
            : Math.Log10(radiusKm + 1) * logSizeMultiplier;

        float finalScale = (float)(scaledRadius * 2);
        body.visualObject.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
    }
}