using System.Collections.Generic;

public enum BodyType
{
    Star,
    RockyPlanet,
    GasGiant,
    IceGiant,
    DwarfPlanet
}

public class CelestialBody
{
    public string name;
    public CelestialBody parent;
    public BodyType bodyType;

    public double massKg;
    public double massEarths;
    public double radiusKm;
    public double standardGravitationalParameter;

    public OrbitalParameters orbit;

    public List<CelestialBody> orbitingBodies = new List<CelestialBody>();
    public List<OrbitNode> virtualOrbits = new List<OrbitNode>();

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
    }

    public void AddOrbitingBody(CelestialBody body, OrbitalParameters parameters)
    {
        body.parent = this;
        body.orbit = parameters;
        body.orbit.parentMu = this.standardGravitationalParameter;
        orbitingBodies.Add(body);
    }

    public void AddVirtualOrbit(OrbitNode node)
    {
        node.orbitalParameters.parentMu = this.standardGravitationalParameter;
        virtualOrbits.Add(node);
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
}