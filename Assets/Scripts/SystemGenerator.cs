using System.Collections.Generic;
using UnityEngine;

public class SystemGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    public StarClassTemplate starClass;
    public string systemName = "Sol";

    [Tooltip("Total rock/metal available in Earth Masses")]
    public float rockBudgetEarths = 15f;

    [Tooltip("Total gas/ice available in Earth Masses")]
    public float gasBudgetEarths = 500f;

    private CelestialBody star;
    private List<CelestialBody> planets = new List<CelestialBody>();

    private void Start()
    {
        GenerateSystem();
    }

    public void GenerateSystem()
    {
        planets.Clear();

        float starMass = Random.Range(starClass.minSolarMass, starClass.maxSolarMass);
        double starMassKg = starMass * AstroMath.SOLAR_MASS_KG;

        star = new CelestialBody(systemName + " Prime", BodyType.Star, starMassKg, 696340);

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

            if (isInsideFrostLine)
            {
                if (currentRockBudget > 0.5)
                {
                    planetMassEarths = Random.Range(0.1f, Mathf.Min(3f, (float)currentRockBudget));
                    currentRockBudget -= planetMassEarths;
                    type = planetMassEarths > 0.1 ? BodyType.RockyPlanet : BodyType.DwarfPlanet;
                }
            }
            else
            {
                if (currentGasBudget > 10)
                {
                    planetMassEarths = Random.Range(10f, Mathf.Min(300f, (float)currentGasBudget));
                    currentGasBudget -= planetMassEarths;

                    double coreMass = Random.Range(1f, 5f);
                    currentRockBudget -= coreMass;
                    planetMassEarths += coreMass;

                    type = planetMassEarths > 50 ? BodyType.GasGiant : BodyType.IceGiant;
                }
                else if (currentRockBudget > 0.1)
                {
                    planetMassEarths = Random.Range(0.01f, 0.5f);
                    currentRockBudget -= planetMassEarths;
                    type = BodyType.DwarfPlanet;
                }
            }

            if (planetMassEarths > 0)
            {
                double massKg = planetMassEarths * AstroMath.EARTH_MASS_KG;
                CelestialBody planet = new CelestialBody($"{systemName} {i + 1}", type, massKg, 5000, star);

                planet.orbit = new OrbitalParameters { semiMajorAxis = distanceAU * AstroMath.AU_TO_KM };

                planets.Add(planet);
            }
        }

        PrintSystemReport(starMass, luminosity, habInner, habOuter, frostLine);
    }

    private List<double> GenerateOrbitalDistances(double habInner)
    {
        List<double> distances = new List<double>();

        double currentDistance = Random.Range(0.2f, (float)habInner);

        int planetCount = Random.Range(5, 11);

        for (int i = 0; i < planetCount; i++)
        {
            distances.Add(currentDistance);
            currentDistance *= Random.Range(1.4f, 2.0f);
        }

        return distances;
    }

    private void PrintSystemReport(float starMass, double luminosity, double habInner, double habOuter, double frostLine)
    {
        Debug.Log($"<color=yellow>=== SYSTEM GENERATED: {systemName} ===</color>");
        Debug.Log($"Star: {starClass.className} | Mass: {starMass:F2} M_sun | Luminosity: {luminosity:F2} L_sun");
        Debug.Log($"Habitable Zone: {habInner:F2} AU to {habOuter:F2} AU | Frost Line: {frostLine:F2} AU");
        Debug.Log("--------------------------------------------------");

        foreach (var planet in planets)
        {
            double distanceAU = planet.orbit.semiMajorAxis / AstroMath.AU_TO_KM;

            string zone = "";
            if (distanceAU >= habInner && distanceAU <= habOuter) zone = "<color=green>[HABITABLE ZONE]</color>";
            else if (distanceAU > frostLine) zone = "<color=cyan>[FROST ZONE]</color>";
            else zone = "<color=red>[HOT ZONE]</color>";

            Debug.Log($"Planet: {planet.name} | Type: {planet.bodyType} | Mass: {planet.massEarths:F2} M_earth | Distance: {distanceAU:F2} AU {zone}");
        }
    }
}