using System.Collections.Generic;
using UnityEngine;

public enum BodyType { Star, RockyPlanet, GasGiant, IceGiant, DwarfPlanet, Moon, Asteroid, Comet }

public class CelestialBody
{
    public string name;
    public CelestialBody parent;
    public BodyType bodyType;

    public PlanetArchetype archetype;

    public double massKg;
    public double massEarths;
    public double radiusKm;
    public double standardGravitationalParameter;
    public double surfaceGravity;

    public double axialTilt;
    public double rotationPeriodSeconds;
    public bool isTidallyLocked;

    public float currentRotationAngle;
    public float deltaRotationAngle;

    public OrbitalParameters orbit;
    public string orbitGroupName;

    public double hillSphereRadiusKm;
    public double localSystemBoundaryKm;

    public List<CelestialBody> orbitingBodies = new List<CelestialBody>();
    public List<OrbitNode> virtualOrbits = new List<OrbitNode>();

    public GameObject visualObject;
    public int dataSubdivisions;

    public PlanetMeshData systemViewData;
    public PlanetMeshData localViewData;

    public float noiseScale;
    public float noiseOffset;
    public float waterLevel;
    public bool isHighResReady = false;

    public BedrockTemplate dominantBedrock;
    public BedrockTemplate secondaryBedrock;

    public double coreMassFraction;
    public float coreTemperatureKelvin;
    public bool isCoreActive;
    public float magnetosphereStrength;

    public Dictionary<byte, double> atmosphericGasesKg = new Dictionary<byte, double>();
    public double surfacePressureAtm;
    public float greenhouseHeatContribution;
    public float toxicityLevel;

    public Vector3d[] cachedOrbitPoints;

    private double lastCalculatedTime = -1;
    private Vector3d cachedAbsolutePosition;

    public CelestialBody(string name, BodyType type, double massKg, double radiusKm, CelestialBody parent = null)
    {
        this.name = name;
        this.bodyType = type;
        this.massKg = massKg;
        this.massEarths = massKg / AstroMath.EARTH_MASS_KG;
        this.radiusKm = radiusKm;
        this.parent = parent;
        this.standardGravitationalParameter = AstroMath.GRAVITATIONAL_CONSTANT * massKg;
        this.orbitGroupName = "None";

        double G_SI = 6.67430e-11;
        double radiusMeters = radiusKm * 1000.0;
        this.surfaceGravity = (G_SI * massKg) / (radiusMeters * radiusMeters);
    }

    public void AddOrbitingBody(CelestialBody body, OrbitalParameters parameters)
    {
        body.parent = this;
        body.orbit = parameters;

        body.orbit.parentMu = this.standardGravitationalParameter;
        body.hillSphereRadiusKm = AstroMath.CalculateHillSphere(body.orbit.semiMajorAxis, body.massKg, this.massKg);

        if (body.isTidallyLocked) body.rotationPeriodSeconds = KeplerMath.GetOrbitalPeriod(body.orbit);

        orbitingBodies.Add(body);
    }

    public Vector3d GetAbsolutePosition(double time)
    {
        if (parent == null) return new Vector3d(0, 0, 0);
        if (time == lastCalculatedTime) return cachedAbsolutePosition;
        Vector3d localPos = KeplerMath.GetPositionAtTime(orbit, time);
        Vector3d absolutePos = parent.GetAbsolutePosition(time) + localPos;
        cachedAbsolutePosition = absolutePos;
        lastCalculatedTime = time;
        return absolutePos;
    }

    public void UpdateRotation(double time)
    {
        if (rotationPeriodSeconds <= 0) return;
        double rotations = time / rotationPeriodSeconds;
        double fractionalRotation = rotations - System.Math.Truncate(rotations);
        float newAngle = (float)(fractionalRotation * 360.0);
        if (lastCalculatedTime != -1) deltaRotationAngle = Mathf.DeltaAngle(currentRotationAngle, newAngle);
        else deltaRotationAngle = 0f;
        currentRotationAngle = newAngle;
    }
}