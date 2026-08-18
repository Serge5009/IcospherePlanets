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
    public float linearDistanceMultiplier = 0.0000001f;
    public float logDistanceMultiplier = 5f;

    [Header("Planet Size Scaling (System View)")]
    public ScaleMode sizeScaleMode = ScaleMode.Linear;
    public float linearSizeMultiplier = 0.0001f;
    public float logSizeMultiplier = 1.5f;

    [Tooltip("Extra multiplier applied ONLY to the Star so it doesn't swallow the inner planets")]
    public float starSizeMultiplier = 0.05f;

    [Header("Visuals")]
    public Material defaultPlanetMaterial;
    public Material defaultAsteroidMaterial;

    private CelestialBody star;
    private List<CelestialBody> allBodies = new List<CelestialBody>();
    private List<CelestialBody> mainPlanets = new List<CelestialBody>();

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
        mainPlanets.Clear();

        float starMass = UnityEngine.Random.Range(starClass.minSolarMass, starClass.maxSolarMass);
        double starMassKg = starMass * AstroMath.SOLAR_MASS_KG;
        star = new CelestialBody(systemName + " Prime", BodyType.Star, starMassKg, 696340);
        SpawnVisualSphere(star);
        allBodies.Add(star);

        double luminosity = AstroMath.CalculateLuminosity(starMass);
        (double habInner, double habOuter) = AstroMath.CalculateHabitableZone(luminosity);
        double frostLine = AstroMath.CalculateFrostLine(luminosity);

        GeneratePlanets(habInner, frostLine);
        GenerateMoons();
        GenerateBelts(frostLine);
        GenerateComets();
    }

    private void GeneratePlanets(double habInner, double frostLine)
    {
        List<double> orbitalDistancesAU = new List<double>();
        double currentDistance = UnityEngine.Random.Range(0.2f, (float)habInner);
        int planetCount = UnityEngine.Random.Range(5, 10);

        for (int i = 0; i < planetCount; i++)
        {
            orbitalDistancesAU.Add(currentDistance);
            currentDistance *= UnityEngine.Random.Range(1.4f, 2.0f);
        }

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
            }

            if (planetMassEarths > 0)
            {
                double massKg = planetMassEarths * AstroMath.EARTH_MASS_KG;
                CelestialBody planet = new CelestialBody($"{systemName} {i + 1}", type, massKg, radiusKm);

                planet.axialTilt = UnityEngine.Random.Range(0f, 45f);

                if (distanceAU < 0.3) planet.isTidallyLocked = true;

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
                mainPlanets.Add(planet);
            }
        }
    }

    private void GenerateMoons()
    {
        foreach (var planet in mainPlanets)
        {
            int moonCount = 0;
            if (planet.bodyType == BodyType.GasGiant || planet.bodyType == BodyType.IceGiant) moonCount = UnityEngine.Random.Range(2, 8);
            else if (planet.bodyType == BodyType.RockyPlanet && planet.massEarths > 0.5) moonCount = UnityEngine.Random.Range(0, 3);

            double distanceToStarKm = planet.orbit.semiMajorAxis;
            double hillSphereKm = distanceToStarKm * Math.Pow(planet.massKg / (3 * star.massKg), 1.0 / 3.0);

            for (int i = 0; i < moonCount; i++)
            {
                double moonMass = planet.massKg * UnityEngine.Random.Range(0.0001f, 0.01f);
                double moonRadius = planet.radiusKm * UnityEngine.Random.Range(0.1f, 0.3f);

                CelestialBody moon = new CelestialBody($"{planet.name}{((char)('a' + i))}", BodyType.Moon, moonMass, moonRadius);
                moon.isTidallyLocked = true;

                double minDistance = planet.radiusKm * 3;
                double maxDistance = hillSphereKm * 0.4;
                double moonDistKm = UnityEngine.Random.Range((float)minDistance, (float)maxDistance);

                OrbitalParameters parameters = new OrbitalParameters
                {
                    semiMajorAxis = moonDistKm,
                    eccentricity = UnityEngine.Random.Range(0.0f, 0.1f),
                    inclination = UnityEngine.Random.Range(-5f, 5f) * Mathf.Deg2Rad,
                    longitudeOfAscendingNode = UnityEngine.Random.Range(0f, 2f * Mathf.PI),
                    argumentOfPeriapsis = UnityEngine.Random.Range(0f, 2f * Mathf.PI),
                    meanAnomalyAtEpoch = UnityEngine.Random.Range(0f, 2f * Mathf.PI)
                };

                planet.AddOrbitingBody(moon, parameters);
                SpawnVisualSphere(moon, true);
                allBodies.Add(moon);
            }
        }
    }

    private void GenerateBelts(double frostLine)
    {
        GenerateAsteroidRing("Asteroid Belt", frostLine * 0.9, frostLine * 1.1, 50);

        if (mainPlanets.Count > 0)
        {
            double lastPlanetAU = mainPlanets[mainPlanets.Count - 1].orbit.semiMajorAxis / AstroMath.AU_TO_KM;
            GenerateAsteroidRing("Kuiper Belt", lastPlanetAU * 1.2, lastPlanetAU * 1.8, 100);
        }
    }

    private void GenerateAsteroidRing(string prefix, double minAU, double maxAU, int count)
    {
        for (int i = 0; i < count; i++)
        {
            double mass = AstroMath.EARTH_MASS_KG * 0.0001;
            CelestialBody asteroid = new CelestialBody($"{prefix} Obj-{i}", BodyType.Asteroid, mass, 500);

            OrbitalParameters parameters = new OrbitalParameters
            {
                semiMajorAxis = UnityEngine.Random.Range((float)minAU, (float)maxAU) * AstroMath.AU_TO_KM,
                eccentricity = UnityEngine.Random.Range(0.0f, 0.2f),
                inclination = UnityEngine.Random.Range(-15f, 15f) * Mathf.Deg2Rad,
                longitudeOfAscendingNode = UnityEngine.Random.Range(0f, 2f * Mathf.PI),
                argumentOfPeriapsis = UnityEngine.Random.Range(0f, 2f * Mathf.PI),
                meanAnomalyAtEpoch = UnityEngine.Random.Range(0f, 2f * Mathf.PI)
            };

            star.AddOrbitingBody(asteroid, parameters);
            SpawnVisualSphere(asteroid, true);
            allBodies.Add(asteroid);
        }
    }

    private void GenerateComets()
    {
        int cometCount = UnityEngine.Random.Range(2, 6);
        for (int i = 0; i < cometCount; i++)
        {
            CelestialBody comet = new CelestialBody($"Comet {i + 1}", BodyType.Comet, AstroMath.EARTH_MASS_KG * 0.00001, 50);

            OrbitalParameters parameters = new OrbitalParameters
            {
                semiMajorAxis = UnityEngine.Random.Range(10f, 30f) * AstroMath.AU_TO_KM,
                eccentricity = UnityEngine.Random.Range(0.85f, 0.98f),
                inclination = UnityEngine.Random.Range(-60f, 60f) * Mathf.Deg2Rad,
                longitudeOfAscendingNode = UnityEngine.Random.Range(0f, 2f * Mathf.PI),
                argumentOfPeriapsis = UnityEngine.Random.Range(0f, 2f * Mathf.PI),
                meanAnomalyAtEpoch = UnityEngine.Random.Range(0f, 2f * Mathf.PI)
            };

            star.AddOrbitingBody(comet, parameters);
            SpawnVisualSphere(comet, true);
            allBodies.Add(comet);
        }
    }

    private void SpawnVisualSphere(CelestialBody body, bool isAsteroid = false)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = body.name;
        sphere.transform.SetParent(this.transform);

        Material matToUse = isAsteroid && defaultAsteroidMaterial != null ? defaultAsteroidMaterial : defaultPlanetMaterial;
        if (matToUse != null)
        {
            sphere.GetComponent<MeshRenderer>().sharedMaterial = matToUse;
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

        if (body.bodyType == BodyType.Star)
        {
            finalScale *= starSizeMultiplier;
        }

        body.visualObject.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
    }
}