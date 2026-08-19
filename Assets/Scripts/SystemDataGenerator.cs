// SystemDataGenerator.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class SystemDataGenerator : MonoBehaviour
{
    public static SystemDataGenerator Instance { get; private set; }

    [Header("Generation Settings")]
    public StarClassTemplate starClass;
    public string systemName = "Sol";
    public float rockBudgetEarths = 15f;
    public float gasBudgetEarths = 500f;

    [Header("Subdivision Math")]
    public float targetCellRadiusKm = 50f;
    public int maxDataSubdivisions = 7;
    public int maxGiantSubdivisions = 5;

    public List<CelestialBody> allBodies { get; private set; } = new List<CelestialBody>();
    private List<CelestialBody> mainPlanets = new List<CelestialBody>();
    public CelestialBody star { get; private set; }

    public double systemEdgeKm { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    // NEW: Always update rotation, even in Local View
    private void Update()
    {
        if (TimeManager.Instance == null) return;
        double time = TimeManager.Instance.totalSeconds;
        foreach (var body in allBodies)
        {
            body.UpdateRotation(time);
        }
    }

    public void GenerateData()
    {
        allBodies.Clear();
        mainPlanets.Clear();

        float starMass = UnityEngine.Random.Range(starClass.minSolarMass, starClass.maxSolarMass);
        double starMassKg = starMass * AstroMath.SOLAR_MASS_KG;
        star = new CelestialBody(systemName + " Prime", BodyType.Star, starMassKg, 696340);

        star.dataSubdivisions = CalculateSubdivisions(star.radiusKm, maxGiantSubdivisions);
        star.rotationPeriodSeconds = 2592000;
        SetThreadSafeParams(star);
        allBodies.Add(star);

        double luminosity = AstroMath.CalculateLuminosity(starMass);
        (double habInner, double habOuter) = AstroMath.CalculateHabitableZone(luminosity);
        double frostLine = AstroMath.CalculateFrostLine(luminosity);

        GenerateAccretionDisk(habInner, frostLine);
        GenerateMoons();
        GenerateComets();

        CalculateSystemBoundaries();
    }

    private void CalculateSystemBoundaries()
    {
        double innermostDistance = double.MaxValue;
        double outermostDistance = 0;

        foreach (var body in allBodies)
        {
            if (body.parent == star)
            {
                double dist = body.orbit.semiMajorAxis;
                if (dist < innermostDistance) innermostDistance = dist;
                if (dist > outermostDistance) outermostDistance = dist;
            }

            if (body.bodyType == BodyType.RockyPlanet || body.bodyType == BodyType.GasGiant || body.bodyType == BodyType.IceGiant)
                body.localSystemBoundaryKm = body.hillSphereRadiusKm;
            else if (body.bodyType == BodyType.Moon)
                body.localSystemBoundaryKm = body.parent.hillSphereRadiusKm;
            else if (body.bodyType == BodyType.DwarfPlanet || body.bodyType == BodyType.Asteroid || body.bodyType == BodyType.Comet)
                body.localSystemBoundaryKm = body.radiusKm * 100;
        }

        star.localSystemBoundaryKm = innermostDistance * 0.5;
        systemEdgeKm = outermostDistance * 1.5;
    }

    private void SetThreadSafeParams(CelestialBody body)
    {
        body.noiseScale = UnityEngine.Random.Range(1.5f, 3f);
        body.noiseOffset = UnityEngine.Random.Range(0f, 10000f);
        body.waterLevel = (body.bodyType == BodyType.RockyPlanet && UnityEngine.Random.value > 0.5f) ? UnityEngine.Random.Range(8000f, 15000f) : 0f;
    }

    private int CalculateSubdivisions(double radiusKm, int hardCap)
    {
        float targetHexArea = 2.598076f * (targetCellRadiusKm * targetCellRadiusKm);
        float surfaceArea = 4f * Mathf.PI * (float)(radiusKm * radiusKm);
        float targetCellCount = surfaceArea / targetHexArea;
        float nFloat = Mathf.Log(Mathf.Max(1, targetCellCount - 2) / 10f, 4f);
        return Mathf.Clamp(Mathf.RoundToInt(nFloat), 0, hardCap);
    }

    private void GenerateAccretionDisk(double habInner, double frostLine)
    {
        List<double> orbitalDistancesAU = new List<double>();
        double currentDistance = UnityEngine.Random.Range(0.2f, (float)habInner);
        int orbitCount = UnityEngine.Random.Range(8, 14);

        for (int i = 0; i < orbitCount; i++)
        {
            orbitalDistancesAU.Add(currentDistance);
            currentDistance *= UnityEngine.Random.Range(1.4f, 1.9f);
        }

        double currentRockBudget = rockBudgetEarths;
        double currentGasBudget = gasBudgetEarths;
        int planetIndex = 1;
        bool mainBeltGenerated = false;

        for (int i = 0; i < orbitalDistancesAU.Count; i++)
        {
            double distanceAU = orbitalDistancesAU[i];

            if (!mainBeltGenerated && distanceAU >= frostLine * 0.8 && distanceAU <= frostLine * 1.2)
            {
                double beltMass = UnityEngine.Random.Range(0.001f, 0.01f);
                GenerateAsteroidBelt("Main Belt", distanceAU, beltMass, false);
                mainBeltGenerated = true;
                continue;
            }

            double massEarths = 0;
            BodyType type = BodyType.DwarfPlanet;
            double radiusKm = 5000;

            if (distanceAU < frostLine)
            {
                massEarths = UnityEngine.Random.Range(0.1f, Mathf.Min(3f, (float)currentRockBudget));
                currentRockBudget -= massEarths;
                type = BodyType.RockyPlanet;
                radiusKm = 6371 * Math.Pow(massEarths, 0.33);
            }
            else
            {
                if (currentGasBudget > 10)
                {
                    massEarths = UnityEngine.Random.Range(10f, Mathf.Min(300f, (float)currentGasBudget));
                    currentGasBudget -= massEarths;
                    double coreMass = UnityEngine.Random.Range(1f, 5f);
                    currentRockBudget -= coreMass;
                    massEarths += coreMass;

                    type = massEarths > 50 ? BodyType.GasGiant : BodyType.IceGiant;
                    radiusKm = 69911 * Math.Pow(massEarths / 317.8, 0.33);
                }
            }

            if (massEarths > 0.1)
            {
                CelestialBody planet = new CelestialBody($"{systemName} {planetIndex}", type, massEarths * AstroMath.EARTH_MASS_KG, radiusKm);
                planetIndex++;

                planet.axialTilt = UnityEngine.Random.Range(0f, 45f);
                if (type == BodyType.GasGiant || type == BodyType.IceGiant) planet.rotationPeriodSeconds = UnityEngine.Random.Range(36000f, 60000f);
                else planet.rotationPeriodSeconds = UnityEngine.Random.Range(72000f, 172800f);

                if (distanceAU < 0.3) planet.isTidallyLocked = true;

                int cap = (type == BodyType.GasGiant || type == BodyType.IceGiant) ? maxGiantSubdivisions : maxDataSubdivisions;
                planet.dataSubdivisions = CalculateSubdivisions(planet.radiusKm, cap);
                SetThreadSafeParams(planet);

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

        if (mainPlanets.Count > 0)
        {
            double lastPlanetAU = mainPlanets[mainPlanets.Count - 1].orbit.semiMajorAxis / AstroMath.AU_TO_KM;
            double kuiperDistance = lastPlanetAU * UnityEngine.Random.Range(1.3f, 1.6f);
            double kuiperMass = UnityEngine.Random.Range(0.1f, 1.0f);
            GenerateAsteroidBelt("Kuiper Belt", kuiperDistance, kuiperMass, true);
        }
    }

    private void GenerateAsteroidBelt(string groupName, double distanceAU, double totalMassEarths, bool isIcy)
    {
        double totalMassKg = totalMassEarths * AstroMath.EARTH_MASS_KG;
        double densityMultiplier = isIcy ? 1.5 : 1.0;

        int dwarfCount = UnityEngine.Random.Range(1, 3);
        double dwarfMassPool = totalMassKg * 0.40;
        for (int i = 0; i < dwarfCount; i++)
        {
            double mass = dwarfMassPool / dwarfCount;
            double radius = 6371 * Math.Pow(mass / AstroMath.EARTH_MASS_KG, 0.33) * densityMultiplier;
            CelestialBody dwarf = new CelestialBody($"{groupName} Alpha-{i + 1}", BodyType.DwarfPlanet, mass, radius);
            dwarf.orbitGroupName = groupName;
            dwarf.dataSubdivisions = CalculateSubdivisions(dwarf.radiusKm, maxDataSubdivisions);
            dwarf.rotationPeriodSeconds = UnityEngine.Random.Range(20000f, 60000f);
            SetThreadSafeParams(dwarf);
            SpawnBeltObject(dwarf, distanceAU, 0.05f, 5f);
        }

        int majorCount = UnityEngine.Random.Range(3, 6);
        double majorMassPool = totalMassKg * 0.30;
        for (int i = 0; i < majorCount; i++)
        {
            double mass = majorMassPool / majorCount;
            double radius = 6371 * Math.Pow(mass / AstroMath.EARTH_MASS_KG, 0.33) * densityMultiplier;
            CelestialBody major = new CelestialBody($"{groupName} Beta-{i + 1}", BodyType.Asteroid, mass, radius);
            major.orbitGroupName = groupName;
            major.dataSubdivisions = CalculateSubdivisions(major.radiusKm, maxDataSubdivisions);
            major.rotationPeriodSeconds = UnityEngine.Random.Range(10000f, 40000f);
            SetThreadSafeParams(major);
            SpawnBeltObject(major, distanceAU, 0.1f, 10f);
        }

        int minorCount = UnityEngine.Random.Range(5, 9);
        double minorMassPool = totalMassKg * 0.10;
        for (int i = 0; i < minorCount; i++)
        {
            double mass = minorMassPool / minorCount;
            double radius = 6371 * Math.Pow(mass / AstroMath.EARTH_MASS_KG, 0.33) * densityMultiplier;
            CelestialBody minor = new CelestialBody($"{groupName} Gamma-{i + 1}", BodyType.Asteroid, mass, radius);
            minor.orbitGroupName = groupName;
            minor.dataSubdivisions = CalculateSubdivisions(minor.radiusKm, maxDataSubdivisions);
            minor.rotationPeriodSeconds = UnityEngine.Random.Range(5000f, 20000f);
            SetThreadSafeParams(minor);
            SpawnBeltObject(minor, distanceAU, 0.15f, 15f);
        }
    }

    private void SpawnBeltObject(CelestialBody body, double baseDistanceAU, float eccentricitySpread, float inclinationSpread)
    {
        double spreadAU = baseDistanceAU + UnityEngine.Random.Range(-0.2f, 0.2f);
        OrbitalParameters orbit = new OrbitalParameters
        {
            semiMajorAxis = spreadAU * AstroMath.AU_TO_KM,
            eccentricity = UnityEngine.Random.Range(0.0f, eccentricitySpread),
            inclination = UnityEngine.Random.Range(-inclinationSpread, inclinationSpread) * Mathf.Deg2Rad,
            longitudeOfAscendingNode = UnityEngine.Random.Range(0f, 2f * Mathf.PI),
            argumentOfPeriapsis = UnityEngine.Random.Range(0f, 2f * Mathf.PI),
            meanAnomalyAtEpoch = UnityEngine.Random.Range(0f, 2f * Mathf.PI)
        };
        star.AddOrbitingBody(body, orbit);
        allBodies.Add(body);
    }

    private void GenerateMoons()
    {
        foreach (var planet in mainPlanets)
        {
            int moonCount = 0;
            if (planet.bodyType == BodyType.GasGiant || planet.bodyType == BodyType.IceGiant) moonCount = UnityEngine.Random.Range(2, 8);
            else if (planet.bodyType == BodyType.RockyPlanet && planet.massEarths > 0.5) moonCount = UnityEngine.Random.Range(0, 3);

            for (int i = 0; i < moonCount; i++)
            {
                double moonMass = planet.massKg * UnityEngine.Random.Range(0.0001f, 0.01f);
                double moonRadius = planet.radiusKm * UnityEngine.Random.Range(0.1f, 0.3f);

                CelestialBody moon = new CelestialBody($"{planet.name}{((char)('a' + i))}", BodyType.Moon, moonMass, moonRadius);
                moon.isTidallyLocked = true;
                moon.dataSubdivisions = CalculateSubdivisions(moon.radiusKm, maxDataSubdivisions);
                SetThreadSafeParams(moon);

                double minDistance = planet.radiusKm * 3;
                double maxDistance = planet.hillSphereRadiusKm * 0.4;
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

    private void GenerateComets()
    {
        int cometCount = UnityEngine.Random.Range(2, 6);
        for (int i = 0; i < cometCount; i++)
        {
            CelestialBody comet = new CelestialBody($"Comet {i + 1}", BodyType.Comet, AstroMath.EARTH_MASS_KG * 0.00001, 50);
            comet.dataSubdivisions = CalculateSubdivisions(comet.radiusKm, maxDataSubdivisions);
            comet.rotationPeriodSeconds = UnityEngine.Random.Range(10000f, 50000f);
            SetThreadSafeParams(comet);

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
}