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

    [Header("Subdivision Math")]
    public float targetCellRadiusKm = 50f;
    public int maxDataSubdivisions = 7;
    public int maxGiantSubdivisions = 5;
    public int systemViewMaxSubdivisions = 4;

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

    public void GenerateSystem()
    {
        allBodies.Clear();
        mainPlanets.Clear();

        float starMass = UnityEngine.Random.Range(starClass.minSolarMass, starClass.maxSolarMass);
        double starMassKg = starMass * AstroMath.SOLAR_MASS_KG;
        star = new CelestialBody(systemName + " Prime", BodyType.Star, starMassKg, 696340);

        star.dataSubdivisions = CalculateSubdivisions(star.radiusKm, maxGiantSubdivisions);
        allBodies.Add(star);

        double luminosity = AstroMath.CalculateLuminosity(starMass);
        (double habInner, double habOuter) = AstroMath.CalculateHabitableZone(luminosity);
        double frostLine = AstroMath.CalculateFrostLine(luminosity);

        GeneratePlanets(habInner, frostLine);
        GenerateMoons();
        GenerateBelts(frostLine);
        GenerateComets();

        foreach (var body in allBodies)
        {
            int renderSubs = Mathf.Min(body.dataSubdivisions, systemViewMaxSubdivisions);
            body.systemViewData = PlanetGenerator.GenerateData((float)body.radiusKm, renderSubs, body.bodyType);
            body.localViewData = PlanetGenerator.GenerateData((float)body.radiusKm, body.dataSubdivisions, body.bodyType);
            SpawnVisualHexSphere(body);
        }
    }

    private int CalculateSubdivisions(double radiusKm, int hardCap)
    {
        float targetHexArea = 2.598076f * (targetCellRadiusKm * targetCellRadiusKm);
        float surfaceArea = 4f * Mathf.PI * (float)(radiusKm * radiusKm);
        float targetCellCount = surfaceArea / targetHexArea;
        float nFloat = Mathf.Log(Mathf.Max(1, targetCellCount - 2) / 10f, 4f);
        return Mathf.Clamp(Mathf.RoundToInt(nFloat), 0, hardCap);
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

                int cap = (type == BodyType.GasGiant || type == BodyType.IceGiant) ? maxGiantSubdivisions : maxDataSubdivisions;
                planet.dataSubdivisions = CalculateSubdivisions(planet.radiusKm, cap);

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
                moon.dataSubdivisions = CalculateSubdivisions(moon.radiusKm, maxDataSubdivisions);

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
            asteroid.dataSubdivisions = CalculateSubdivisions(asteroid.radiusKm, maxDataSubdivisions);

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
            allBodies.Add(asteroid);
        }
    }

    private void GenerateComets()
    {
        int cometCount = UnityEngine.Random.Range(2, 6);
        for (int i = 0; i < cometCount; i++)
        {
            CelestialBody comet = new CelestialBody($"Comet {i + 1}", BodyType.Comet, AstroMath.EARTH_MASS_KG * 0.00001, 50);
            comet.dataSubdivisions = CalculateSubdivisions(comet.radiusKm, maxDataSubdivisions);

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
            allBodies.Add(comet);
        }
    }

    private void SpawnVisualHexSphere(CelestialBody body)
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