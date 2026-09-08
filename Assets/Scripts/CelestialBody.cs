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

    public double totalSurfaceAreaSqKm;

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

    public HexSphereTemplate geometryTemplate;
    public int variantIndex;

    public PlanetMeshData systemViewData;
    public PlanetMeshData localViewData;

    public int[] lowToHighMap;

    public float noiseScale;
    public float noiseOffset;

    public double[] hypsometricCurveSqKm;
    public double oceanVolumeKm3;
    public float waterLevel;

    public float lastCalculatedWaterLevel = -9999f;

    public bool isHighResReady = false;

    public BedrockTemplate dominantBedrock;
    public BedrockTemplate secondaryBedrock;
    public LiquidTemplate oceanLiquid;
    public SoilTemplate surfaceSoil;

    public byte dominantBedrockId;
    public Color dominantBedrockColor;
    public byte secondaryBedrockId;
    public Color secondaryBedrockColor;
    public Color oceanColor;

    public byte soilId;
    public Color soilDryColor;
    public Color soilWetColor;
    public float soilBaseThickness;

    public double coreMassFraction;
    public float coreTemperatureKelvin;
    public bool isCoreActive;
    public float magnetosphereStrength;

    public Dictionary<byte, double> atmosphericGasesKg = new Dictionary<byte, double>();
    public double surfacePressureAtm;
    public float greenhouseHeatContribution;
    public float toxicityLevel;

    public float globalMinTemperature;
    public float globalMaxTemperature;

    public Color atmosphereSkyColor;
    public Color atmosphereCloudColor;
    public float atmosphereVisualScale;
    public float atmosphereVisualOpacity;
    public float atmosphereCloudCoverage;
    public float atmosphereCloudScale;
    public GameObject atmosphereObject;

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

        this.totalSurfaceAreaSqKm = 4.0 * System.Math.PI * (radiusKm * radiusKm);
    }

    public double GetCellAreaSqKm(int cellId)
    {
        if (geometryTemplate == null || geometryTemplate.variants == null || variantIndex >= geometryTemplate.variants.Length)
            return 0;

        float fraction = geometryTemplate.variants[variantIndex].areaFractions[cellId];
        return totalSurfaceAreaSqKm * fraction;
    }

    public void RebuildHypsometricCurve()
    {
        if (localViewData == null || localViewData.topologies == null) return;

        float maxAlt = 0f;
        foreach (var topo in localViewData.topologies)
        {
            if (topo.altitude > maxAlt) maxAlt = topo.altitude;
        }

        int maxMeter = Mathf.CeilToInt(maxAlt);
        hypsometricCurveSqKm = new double[maxMeter + 1];

        for (int i = 0; i < localViewData.topologies.Length; i++)
        {
            int altMeter = Mathf.FloorToInt(localViewData.topologies[i].altitude);
            altMeter = Mathf.Clamp(altMeter, 0, maxMeter);

            double area = GetCellAreaSqKm(i);
            hypsometricCurveSqKm[altMeter] += area;
        }

        for (int i = 1; i < hypsometricCurveSqKm.Length; i++)
        {
            hypsometricCurveSqKm[i] += hypsometricCurveSqKm[i - 1];
        }
    }

    public bool HasCoastlineChanged(float newWaterLevel)
    {
        if (hypsometricCurveSqKm == null || hypsometricCurveSqKm.Length == 0) return false;

        int oldIdx = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, lastCalculatedWaterLevel)), 0, hypsometricCurveSqKm.Length - 1);
        int newIdx = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, newWaterLevel)), 0, hypsometricCurveSqKm.Length - 1);

        return hypsometricCurveSqKm[oldIdx] != hypsometricCurveSqKm[newIdx];
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