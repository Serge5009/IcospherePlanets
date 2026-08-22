using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct WeightedBedrock { public BedrockTemplate template; public float weight; }

[Serializable]
public struct WeightedGas { public GasTemplate template; public float weight; }

[Serializable]
public struct WeightedLiquid { public LiquidTemplate template; public float weight; }

public class SystemDataGenerator : MonoBehaviour
{
    public static SystemDataGenerator Instance { get; private set; }

    [Header("Generation Settings")]
    public StarClassTemplate starClass;
    public string systemName = "Sol";
    public float rockBudgetEarths = 15f;
    public float gasBudgetEarths = 500f;

    [Header("Accretion Pools: Bedrock")]
    public List<WeightedBedrock> innerBedrocks;
    public List<WeightedBedrock> habitableBedrocks;
    public List<WeightedBedrock> outerBedrocks;

    [Header("Accretion Pools: Gases")]
    public List<WeightedGas> innerGases;
    public List<WeightedGas> outerGases;
    public List<WeightedGas> giantGases;

    [Header("Accretion Pools: Oceans")]
    public List<WeightedLiquid> habitableLiquids;
    public List<WeightedLiquid> outerLiquids;

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

    private void Update()
    {
        if (TimeManager.Instance == null) return;
        double time = TimeManager.Instance.totalSeconds;
        foreach (var body in allBodies) body.UpdateRotation(time);
    }

    public void GenerateData()
    {
        allBodies.Clear();
        mainPlanets.Clear();

        float starMass = UnityEngine.Random.Range(starClass.minSolarMass, starClass.maxSolarMass);
        double starMassKg = starMass * AstroMath.SOLAR_MASS_KG;
        star = new CelestialBody(systemName + " Prime", BodyType.Star, starMassKg, 696340);
        star.archetype = PlanetArchetype.GasGiant;

        Color starColor = starClass.starColor;
        star.atmosphereSkyColor = new Color(starColor.r * 0.5f, starColor.g * 0.5f, starColor.b * 0.5f, 1f);
        star.atmosphereCloudColor = new Color(starColor.r * 2.5f, starColor.g * 2.5f, starColor.b * 2.5f, 1f);
        star.atmosphereVisualScale = 1.02f;
        star.atmosphereVisualOpacity = 1.0f;
        star.atmosphereCloudCoverage = UnityEngine.Random.Range(0.6f, 0.8f);
        star.surfacePressureAtm = 100.0;
        star.atmosphereCloudScale = Mathf.Pow(20f, UnityEngine.Random.value);

        star.dataSubdivisions = CalculateSubdivisions(star.radiusKm, maxGiantSubdivisions);
        star.rotationPeriodSeconds = 2592000;
        SetThreadSafeParams(star);
        allBodies.Add(star);

        double luminosity = AstroMath.CalculateLuminosity(starMass);
        (double habInner, double habOuter) = AstroMath.CalculateHabitableZone(luminosity);
        double frostLine = AstroMath.CalculateFrostLine(luminosity);

        GenerateAccretionDisk(habInner, frostLine);
        GenerateMoons(frostLine);
        GenerateComets(frostLine);

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

        body.waterLevel = (body.bodyType == BodyType.RockyPlanet && UnityEngine.Random.value > 0.5f) ? UnityEngine.Random.Range(-2000f, 2000f) : -9999f;
    }

    private BedrockTemplate GetRandomBedrock(List<WeightedBedrock> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        float total = 0;
        foreach (var w in pool) total += w.weight;
        float roll = UnityEngine.Random.Range(0, total);
        foreach (var w in pool)
        {
            roll -= w.weight;
            if (roll <= 0) return w.template;
        }
        return pool[0].template;
    }

    private LiquidTemplate GetRandomLiquid(List<WeightedLiquid> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        float total = 0;
        foreach (var w in pool) total += w.weight;
        float roll = UnityEngine.Random.Range(0, total);
        foreach (var w in pool)
        {
            roll -= w.weight;
            if (roll <= 0) return w.template;
        }
        return pool[0].template;
    }

    private void AssignAccretionMaterials(CelestialBody body, double distanceAU, double frostLine)
    {
        if (distanceAU < frostLine * 0.5)
        {
            body.dominantBedrock = GetRandomBedrock(innerBedrocks);
            body.secondaryBedrock = GetRandomBedrock(innerBedrocks);
            body.oceanLiquid = null;
        }
        else if (distanceAU < frostLine * 1.2)
        {
            body.dominantBedrock = GetRandomBedrock(habitableBedrocks);
            body.secondaryBedrock = GetRandomBedrock(habitableBedrocks);
            body.oceanLiquid = GetRandomLiquid(habitableLiquids);
        }
        else
        {
            body.dominantBedrock = GetRandomBedrock(outerBedrocks);
            body.secondaryBedrock = GetRandomBedrock(outerBedrocks);
            body.oceanLiquid = GetRandomLiquid(outerLiquids);
        }

        body.dominantBedrockId = body.dominantBedrock != null ? body.dominantBedrock.bedrockId : (byte)1;
        body.dominantBedrockColor = body.dominantBedrock != null ? body.dominantBedrock.baseColor : Color.gray;

        body.secondaryBedrockId = body.secondaryBedrock != null ? body.secondaryBedrock.bedrockId : (byte)1;
        body.secondaryBedrockColor = body.secondaryBedrock != null ? body.secondaryBedrock.baseColor : Color.gray;

        body.oceanColor = body.oceanLiquid != null ? body.oceanLiquid.shallowColor : Color.blue;
    }

    private void CalculateCoreAndMagnetosphere(CelestialBody body)
    {
        if (body.bodyType == BodyType.Star || body.bodyType == BodyType.Comet || body.bodyType == BodyType.Asteroid)
        {
            body.coreMassFraction = 0;
            body.coreTemperatureKelvin = 0;
            body.isCoreActive = false;
            body.magnetosphereStrength = 0;
            return;
        }

        body.coreMassFraction = UnityEngine.Random.Range(0.2f, 0.5f);
        if (body.dominantBedrock != null && body.dominantBedrock.bedrockName.Contains("Metallic")) body.coreMassFraction = UnityEngine.Random.Range(0.5f, 0.75f);
        else if (body.dominantBedrock != null && body.dominantBedrock.bedrockName.Contains("Carbon")) body.coreMassFraction = UnityEngine.Random.Range(0.05f, 0.15f);

        float baseHeat = 6000f * Mathf.Pow((float)body.massEarths, 0.5f);

        if (body.bodyType == BodyType.Moon && body.parent != null && body.parent.massEarths > 10)
        {
            double distanceKm = body.orbit.semiMajorAxis;
            float tidalHeat = (float)(2000000.0 * body.parent.massEarths / (distanceKm * distanceKm));
            baseHeat += tidalHeat;
        }

        body.coreTemperatureKelvin = Mathf.Clamp(baseHeat, 0f, 8000f);
        body.isCoreActive = body.coreTemperatureKelvin > 1800f;

        if (body.isCoreActive && body.rotationPeriodSeconds > 0)
        {
            float normalizedCore = (float)body.coreMassFraction / 0.32f;
            float spinFactor = Mathf.Sqrt(86400f / (float)body.rotationPeriodSeconds);
            spinFactor = Mathf.Clamp(spinFactor, 0f, 2.5f);
            float massFactor = Mathf.Pow((float)body.massEarths, 0.33f);
            body.magnetosphereStrength = normalizedCore * spinFactor * massFactor;
        }
        else
        {
            body.magnetosphereStrength = 0f;
        }
    }

    private void CalculateAtmosphere(CelestialBody body, double distanceAU, double frostLine)
    {
        body.atmosphericGasesKg.Clear();
        body.surfacePressureAtm = 0;
        body.greenhouseHeatContribution = 0;
        body.toxicityLevel = 0;

        if (body.bodyType == BodyType.Star || body.bodyType == BodyType.Asteroid || body.bodyType == BodyType.Comet) return;

        double baseCapacity = 5.15e18 * body.massEarths;
        double magFactor = Math.Max(0.01, body.magnetosphereStrength);
        double gravFactor = Math.Max(0.01, body.surfaceGravity / 9.8);
        double atmosphericCapacityKg = baseCapacity * magFactor * gravFactor;

        double accretedMass = baseCapacity;
        if (distanceAU > frostLine) accretedMass *= UnityEngine.Random.Range(5f, 50f);

        double finalMass = Math.Min(accretedMass, atmosphericCapacityKg);

        if (finalMass > 0)
        {
            List<WeightedGas> gasPool;
            if (body.bodyType == BodyType.GasGiant || body.bodyType == BodyType.IceGiant) gasPool = giantGases;
            else if (distanceAU < frostLine) gasPool = innerGases;
            else gasPool = outerGases;

            if (gasPool != null && gasPool.Count > 0)
            {
                float totalWeight = 0;
                foreach (var wg in gasPool) totalWeight += wg.weight;

                foreach (var wg in gasPool)
                {
                    if (wg.template == null) continue;
                    double gasMass = finalMass * (wg.weight / totalWeight);
                    gasMass *= UnityEngine.Random.Range(0.8f, 1.2f);
                    if (gasMass > 0) body.atmosphericGasesKg[wg.template.gasId] = gasMass;
                }
            }

            UpdateAtmosphericProperties(body);
        }

        if (body.surfacePressureAtm < 0.05)
        {
            body.waterLevel = 0f;
            if (body.archetype == PlanetArchetype.ActiveTerrestrial)
                body.archetype = PlanetArchetype.DeadTerrestrial;
        }

        if (body.bodyType == BodyType.GasGiant || body.bodyType == BodyType.IceGiant)
            body.atmosphereCloudScale = Mathf.Pow(20f, UnityEngine.Random.value);
        else
            body.atmosphereCloudScale = UnityEngine.Random.Range(2.5f, 10f);
    }

    public void UpdateAtmosphericProperties(CelestialBody body)
    {
        double totalMassKg = 0;
        double vaporMassKg = 0;
        float totalGreenhouse = 0;
        float totalToxicity = 0;

        Color blendedSky = Color.black;
        Color blendedCloud = Color.black;

        foreach (var kvp in body.atmosphericGasesKg)
        {
            totalMassKg += kvp.Value;
            GasTemplate gas = DataLibrary.Instance.GetGas(kvp.Key);
            if (gas != null && gas.formsClouds) vaporMassKg += kvp.Value;
        }

        foreach (var kvp in body.atmosphericGasesKg)
        {
            GasTemplate gas = DataLibrary.Instance.GetGas(kvp.Key);
            if (gas != null)
            {
                float fractionOfTotal = (float)(kvp.Value / Math.Max(1.0, totalMassKg));
                totalGreenhouse += gas.greenhouseMultiplier * fractionOfTotal;
                totalToxicity += gas.toxicity * fractionOfTotal;

                if (!gas.formsClouds) blendedSky += gas.skyColor * fractionOfTotal;

                if (gas.formsClouds && vaporMassKg > 0)
                {
                    float fractionOfVapor = (float)(kvp.Value / vaporMassKg);
                    blendedCloud += gas.cloudColor * fractionOfVapor;
                }
            }
        }

        double surfaceAreaM2 = 4.0 * Math.PI * Math.Pow(body.radiusKm * 1000.0, 2);
        double pressurePascals = (totalMassKg * body.surfaceGravity) / surfaceAreaM2;
        body.surfacePressureAtm = pressurePascals / 101325.0;

        double vaporPressureAtm = ((vaporMassKg * body.surfaceGravity) / surfaceAreaM2) / 101325.0;

        body.greenhouseHeatContribution = totalGreenhouse * (float)body.surfacePressureAtm * 30f;
        body.toxicityLevel = totalToxicity;

        body.atmosphereSkyColor = new Color(Mathf.Clamp01(blendedSky.r), Mathf.Clamp01(blendedSky.g), Mathf.Clamp01(blendedSky.b), Mathf.Clamp01(blendedSky.a));
        body.atmosphereCloudColor = new Color(Mathf.Clamp01(blendedCloud.r), Mathf.Clamp01(blendedCloud.g), Mathf.Clamp01(blendedCloud.b), Mathf.Clamp01(blendedCloud.a));

        body.atmosphereVisualScale = 1.01f + Mathf.Log10(1f + (float)body.surfacePressureAtm) * 0.05f;
        body.atmosphereVisualScale = Mathf.Clamp(body.atmosphereVisualScale, 1.01f, 1.15f);

        float pressureFloat = (float)body.surfacePressureAtm;
        body.atmosphereVisualOpacity = Mathf.Clamp01(Mathf.Sqrt(pressureFloat / 5.0f));

        if (vaporPressureAtm > 0)
        {
            float rawCoverage = Mathf.Sqrt((float)vaporPressureAtm / 0.1f);
            body.atmosphereCloudCoverage = Mathf.Clamp(rawCoverage, 0.1f, 0.8f);
        }
        else
        {
            body.atmosphereCloudCoverage = 0f;
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
                GenerateAsteroidBelt("Main Belt", distanceAU, frostLine, beltMass, false);
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
                AssignAccretionMaterials(planet, distanceAU, frostLine);

                CalculateCoreAndMagnetosphere(planet);
                CalculateAtmosphere(planet, distanceAU, frostLine);

                if (type == BodyType.GasGiant || type == BodyType.IceGiant) planet.archetype = PlanetArchetype.GasGiant;
                else if (planet.isCoreActive) planet.archetype = PlanetArchetype.ActiveTerrestrial;
                else planet.archetype = PlanetArchetype.DeadTerrestrial;

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
            GenerateAsteroidBelt("Kuiper Belt", kuiperDistance, frostLine, kuiperMass, true);
        }
    }

    private void GenerateAsteroidBelt(string groupName, double distanceAU, double frostLine, double totalMassEarths, bool isIcy)
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
            AssignAccretionMaterials(dwarf, distanceAU, frostLine);
            CalculateCoreAndMagnetosphere(dwarf);
            CalculateAtmosphere(dwarf, distanceAU, frostLine);
            dwarf.archetype = PlanetArchetype.Barren;
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
            AssignAccretionMaterials(major, distanceAU, frostLine);
            CalculateCoreAndMagnetosphere(major);
            CalculateAtmosphere(major, distanceAU, frostLine);
            major.archetype = PlanetArchetype.Barren;
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
            AssignAccretionMaterials(minor, distanceAU, frostLine);
            CalculateCoreAndMagnetosphere(minor);
            CalculateAtmosphere(minor, distanceAU, frostLine);
            minor.archetype = PlanetArchetype.Barren;
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

    private void GenerateMoons(double frostLine)
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

                double distanceAU = planet.orbit.semiMajorAxis / AstroMath.AU_TO_KM;
                AssignAccretionMaterials(moon, distanceAU, frostLine);

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

                CalculateCoreAndMagnetosphere(moon);
                CalculateAtmosphere(moon, distanceAU, frostLine);

                if (moonMass / AstroMath.EARTH_MASS_KG > 0.1 && distanceAU > frostLine && moon.isCoreActive) moon.archetype = PlanetArchetype.ActiveIce;
                else moon.archetype = PlanetArchetype.Barren;

                allBodies.Add(moon);
            }
        }
    }

    private void GenerateComets(double frostLine)
    {
        int cometCount = UnityEngine.Random.Range(2, 6);
        for (int i = 0; i < cometCount; i++)
        {
            CelestialBody comet = new CelestialBody($"Comet {i + 1}", BodyType.Comet, AstroMath.EARTH_MASS_KG * 0.00001, 50);
            comet.dataSubdivisions = CalculateSubdivisions(comet.radiusKm, maxDataSubdivisions);
            comet.rotationPeriodSeconds = UnityEngine.Random.Range(10000f, 50000f);
            SetThreadSafeParams(comet);
            AssignAccretionMaterials(comet, frostLine * 5.0, frostLine);
            CalculateCoreAndMagnetosphere(comet);
            CalculateAtmosphere(comet, frostLine * 5.0, frostLine);
            comet.archetype = PlanetArchetype.Barren;

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