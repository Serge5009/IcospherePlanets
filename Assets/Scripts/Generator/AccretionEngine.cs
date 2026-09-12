using System;
using System.Collections.Generic;
using UnityEngine;

public static class AccretionEngine
{
    public static void BuildSystem(SystemDataGenerator director)
    {
        director.allBodies.Clear();
        director.mainPlanets.Clear();

        float starMass = UnityEngine.Random.Range(director.starClass.minSolarMass, director.starClass.maxSolarMass);
        double starMassKg = starMass * AstroMath.SOLAR_MASS_KG;
        director.star = new CelestialBody(director.systemName + " Prime", BodyType.Star, starMassKg, 696340);
        director.star.archetype = PlanetArchetype.GasGiant;

        Color starColor = director.starClass.starColor;
        director.star.atmosphereSkyColor = new Color(starColor.r * 0.5f, starColor.g * 0.5f, starColor.b * 0.5f, 1f);
        director.star.atmosphereCloudColor = new Color(starColor.r * 2.5f, starColor.g * 2.5f, starColor.b * 2.5f, 1f);
        director.star.atmosphereVisualScale = 1.02f;
        director.star.atmosphereVisualOpacity = 1.0f;
        director.star.atmosphereCloudCoverage = UnityEngine.Random.Range(0.6f, 0.8f);
        director.star.surfacePressureAtm = 100.0;
        director.star.atmosphereCloudScale = Mathf.Pow(20f, UnityEngine.Random.value);

        director.star.dataSubdivisions = CalculateSubdivisions(director.star.radiusKm, director.maxGiantSubdivisions, director.targetCellRadiusKm);
        director.star.rotationPeriodSeconds = 2592000;
        SetThreadSafeParams(director.star);
        director.allBodies.Add(director.star);

        double luminosity = AstroMath.CalculateLuminosity(starMass);
        (double habInner, double habOuter) = AstroMath.CalculateHabitableZone(luminosity);
        double frostLine = AstroMath.CalculateFrostLine(luminosity);

        GenerateAccretionDisk(director, habInner, frostLine);
        GenerateMoons(director, frostLine);
        GenerateComets(director, frostLine);

        CalculateSystemBoundaries(director);
    }

    private static void GenerateAccretionDisk(SystemDataGenerator director, double habInner, double frostLine)
    {
        List<double> orbitalDistancesAU = new List<double>();
        double currentDistance = UnityEngine.Random.Range(0.2f, (float)habInner);
        int orbitCount = UnityEngine.Random.Range(8, 14);

        for (int i = 0; i < orbitCount; i++)
        {
            orbitalDistancesAU.Add(currentDistance);
            currentDistance *= UnityEngine.Random.Range(1.4f, 1.9f);
        }

        double currentRockBudget = director.rockBudgetEarths;
        double currentGasBudget = director.gasBudgetEarths;
        int planetIndex = 1;
        bool mainBeltGenerated = false;

        for (int i = 0; i < orbitalDistancesAU.Count; i++)
        {
            double distanceAU = orbitalDistancesAU[i];

            if (!mainBeltGenerated && distanceAU >= frostLine * 0.8 && distanceAU <= frostLine * 1.2)
            {
                double beltMass = UnityEngine.Random.Range(0.001f, 0.01f);
                GenerateAsteroidBelt(director, "Main Belt", distanceAU, frostLine, beltMass, false);
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
                    massEarths = UnityEngine.Random.Range(50f, Mathf.Min(300f, (float)currentGasBudget));
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
                CelestialBody planet = new CelestialBody($"{director.systemName} {planetIndex}", type, massEarths * AstroMath.EARTH_MASS_KG, radiusKm);
                planetIndex++;

                planet.axialTilt = UnityEngine.Random.Range(0f, 45f);
                if (type == BodyType.GasGiant || type == BodyType.IceGiant) planet.rotationPeriodSeconds = UnityEngine.Random.Range(36000f, 60000f);
                else planet.rotationPeriodSeconds = UnityEngine.Random.Range(72000f, 172800f);

                if (distanceAU < 0.3) planet.isTidallyLocked = true;

                int cap = (type == BodyType.GasGiant || type == BodyType.IceGiant) ? director.maxGiantSubdivisions : director.maxDataSubdivisions;
                planet.dataSubdivisions = CalculateSubdivisions(planet.radiusKm, cap, director.targetCellRadiusKm);
                SetThreadSafeParams(planet);
                AssignAccretionMaterials(director, planet, distanceAU, frostLine);

                CoreBuilder.CalculateCoreAndMagnetosphere(planet);
                AtmosphereBuilder.CalculateAtmosphere(planet, distanceAU);

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

                director.star.AddOrbitingBody(planet, parameters);
                director.allBodies.Add(planet);
                director.mainPlanets.Add(planet);
            }
        }

        if (director.mainPlanets.Count > 0)
        {
            double lastPlanetAU = director.mainPlanets[director.mainPlanets.Count - 1].orbit.semiMajorAxis / AstroMath.AU_TO_KM;
            double kuiperDistance = lastPlanetAU * UnityEngine.Random.Range(1.3f, 1.6f);
            double kuiperMass = UnityEngine.Random.Range(0.1f, 1.0f);
            GenerateAsteroidBelt(director, "Kuiper Belt", kuiperDistance, frostLine, kuiperMass, true);
        }
    }

    private static void GenerateAsteroidBelt(SystemDataGenerator director, string groupName, double distanceAU, double frostLine, double totalMassEarths, bool isIcy)
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
            dwarf.dataSubdivisions = CalculateSubdivisions(dwarf.radiusKm, director.maxDataSubdivisions, director.targetCellRadiusKm);
            dwarf.rotationPeriodSeconds = UnityEngine.Random.Range(20000f, 60000f);
            SetThreadSafeParams(dwarf);
            AssignAccretionMaterials(director, dwarf, distanceAU, frostLine);
            CoreBuilder.CalculateCoreAndMagnetosphere(dwarf);
            AtmosphereBuilder.CalculateAtmosphere(dwarf, distanceAU);
            dwarf.archetype = PlanetArchetype.Barren;
            SpawnBeltObject(director, dwarf, distanceAU, 0.05f, 5f);
        }

        int majorCount = UnityEngine.Random.Range(3, 6);
        double majorMassPool = totalMassKg * 0.30;
        for (int i = 0; i < majorCount; i++)
        {
            double mass = majorMassPool / majorCount;
            double radius = 6371 * Math.Pow(mass / AstroMath.EARTH_MASS_KG, 0.33) * densityMultiplier;
            CelestialBody major = new CelestialBody($"{groupName} Beta-{i + 1}", BodyType.Asteroid, mass, radius);
            major.orbitGroupName = groupName;
            major.dataSubdivisions = CalculateSubdivisions(major.radiusKm, director.maxDataSubdivisions, director.targetCellRadiusKm);
            major.rotationPeriodSeconds = UnityEngine.Random.Range(10000f, 40000f);
            SetThreadSafeParams(major);
            AssignAccretionMaterials(director, major, distanceAU, frostLine);
            CoreBuilder.CalculateCoreAndMagnetosphere(major);
            AtmosphereBuilder.CalculateAtmosphere(major, distanceAU);
            major.archetype = PlanetArchetype.Barren;
            SpawnBeltObject(director, major, distanceAU, 0.1f, 10f);
        }

        int minorCount = UnityEngine.Random.Range(5, 9);
        double minorMassPool = totalMassKg * 0.10;
        for (int i = 0; i < minorCount; i++)
        {
            double mass = minorMassPool / minorCount;
            double radius = 6371 * Math.Pow(mass / AstroMath.EARTH_MASS_KG, 0.33) * densityMultiplier;
            CelestialBody minor = new CelestialBody($"{groupName} Gamma-{i + 1}", BodyType.Asteroid, mass, radius);
            minor.orbitGroupName = groupName;
            minor.dataSubdivisions = CalculateSubdivisions(minor.radiusKm, director.maxDataSubdivisions, director.targetCellRadiusKm);
            minor.rotationPeriodSeconds = UnityEngine.Random.Range(5000f, 20000f);
            SetThreadSafeParams(minor);
            AssignAccretionMaterials(director, minor, distanceAU, frostLine);
            CoreBuilder.CalculateCoreAndMagnetosphere(minor);
            AtmosphereBuilder.CalculateAtmosphere(minor, distanceAU);
            minor.archetype = PlanetArchetype.Barren;
            SpawnBeltObject(director, minor, distanceAU, 0.15f, 15f);
        }
    }

    private static void SpawnBeltObject(SystemDataGenerator director, CelestialBody body, double baseDistanceAU, float eccentricitySpread, float inclinationSpread)
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
        director.star.AddOrbitingBody(body, orbit);
        director.allBodies.Add(body);
    }

    private static void GenerateMoons(SystemDataGenerator director, double frostLine)
    {
        foreach (var planet in director.mainPlanets)
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
                moon.dataSubdivisions = CalculateSubdivisions(moon.radiusKm, director.maxDataSubdivisions, director.targetCellRadiusKm);
                SetThreadSafeParams(moon);

                double distanceAU = planet.orbit.semiMajorAxis / AstroMath.AU_TO_KM;
                AssignAccretionMaterials(director, moon, distanceAU, frostLine);

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

                CoreBuilder.CalculateCoreAndMagnetosphere(moon);
                AtmosphereBuilder.CalculateAtmosphere(moon, distanceAU);

                if (moonMass / AstroMath.EARTH_MASS_KG > 0.1 && distanceAU > frostLine && moon.isCoreActive) moon.archetype = PlanetArchetype.ActiveIce;
                else moon.archetype = PlanetArchetype.Barren;

                director.allBodies.Add(moon);
            }
        }
    }

    private static void GenerateComets(SystemDataGenerator director, double frostLine)
    {
        int cometCount = UnityEngine.Random.Range(2, 6);
        for (int i = 0; i < cometCount; i++)
        {
            CelestialBody comet = new CelestialBody($"Comet {i + 1}", BodyType.Comet, AstroMath.EARTH_MASS_KG * 0.00001, 50);
            comet.dataSubdivisions = CalculateSubdivisions(comet.radiusKm, director.maxDataSubdivisions, director.targetCellRadiusKm);
            comet.rotationPeriodSeconds = UnityEngine.Random.Range(10000f, 50000f);
            SetThreadSafeParams(comet);
            AssignAccretionMaterials(director, comet, frostLine * 5.0, frostLine);
            CoreBuilder.CalculateCoreAndMagnetosphere(comet);
            AtmosphereBuilder.CalculateAtmosphere(comet, frostLine * 5.0);
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

            director.star.AddOrbitingBody(comet, parameters);
            director.allBodies.Add(comet);
        }
    }

    private static void CalculateSystemBoundaries(SystemDataGenerator director)
    {
        double innermostDistance = double.MaxValue;
        double outermostDistance = 0;

        foreach (var body in director.allBodies)
        {
            if (body.parent == director.star)
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

        director.star.localSystemBoundaryKm = innermostDistance * 0.5;
        director.systemEdgeKm = outermostDistance * 1.5;
    }

    private static int CalculateSubdivisions(double radiusKm, int hardCap, float targetCellRadiusKm)
    {
        float targetHexArea = 2.598076f * (targetCellRadiusKm * targetCellRadiusKm);
        float surfaceArea = 4f * Mathf.PI * (float)(radiusKm * radiusKm);
        float targetCellCount = surfaceArea / targetHexArea;
        float nFloat = Mathf.Log(Mathf.Max(1, targetCellCount - 2) / 10f, 4f);
        return Mathf.Clamp(Mathf.RoundToInt(nFloat), 0, hardCap);
    }

    private static void SetThreadSafeParams(CelestialBody body)
    {
        body.noiseScale = UnityEngine.Random.Range(1.5f, 3f);
        body.noiseOffset = UnityEngine.Random.Range(0f, 10000f);
    }

    private static void AssignAccretionMaterials(SystemDataGenerator director, CelestialBody body, double distanceAU, double frostLine)
    {
        if (distanceAU < frostLine * 0.5)
        {
            body.dominantBedrock = GetRandomBedrock(director.innerBedrocks);
            body.secondaryBedrock = GetRandomBedrock(director.innerBedrocks);
            body.surfaceSoil = GetRandomSoil(director.innerSoils);
        }
        else if (distanceAU < frostLine * 1.2)
        {
            body.dominantBedrock = GetRandomBedrock(director.habitableBedrocks);
            body.secondaryBedrock = GetRandomBedrock(director.habitableBedrocks);
            body.surfaceSoil = GetRandomSoil(director.habitableSoils);
        }
        else
        {
            body.dominantBedrock = GetRandomBedrock(director.outerBedrocks);
            body.secondaryBedrock = GetRandomBedrock(director.outerBedrocks);
            body.surfaceSoil = GetRandomSoil(director.outerSoils);
        }

        body.dominantBedrockId = body.dominantBedrock != null ? body.dominantBedrock.bedrockId : (byte)0;
        body.dominantBedrockColor = body.dominantBedrock != null ? body.dominantBedrock.baseColor : Color.gray;

        body.secondaryBedrockId = body.secondaryBedrock != null ? body.secondaryBedrock.bedrockId : (byte)0;
        body.secondaryBedrockColor = body.secondaryBedrock != null ? body.secondaryBedrock.baseColor : Color.gray;

        body.soilId = body.surfaceSoil != null ? body.surfaceSoil.soilId : (byte)0;
        body.soilDryColor = body.surfaceSoil != null ? body.surfaceSoil.dryColor : new Color(0.7f, 0.6f, 0.4f);
        body.soilWetColor = body.surfaceSoil != null ? body.surfaceSoil.wetColor : new Color(0.3f, 0.2f, 0.1f);
        body.soilBaseThickness = body.surfaceSoil != null ? body.surfaceSoil.baseThicknessMultiplier : 1.0f;

        body.oceanLiquid = null;
        body.oceanColor = Color.black;
        body.oceanVolumeKm3 = 0;
        body.waterLevel = -9999f;
        body.lastCalculatedWaterLevel = -9999f;
    }

    private static BedrockTemplate GetRandomBedrock(List<WeightedBedrock> pool)
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

    private static SoilTemplate GetRandomSoil(List<WeightedSoil> pool)
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
}