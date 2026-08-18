using System.Collections.Generic;

public class OrbitNode
{
    public string name;
    public CelestialBody parentBody;
    public OrbitalParameters orbitalParameters;

    public double capacity;
    public double currentLoad;

    public Dictionary<int, int> factionSatellites = new Dictionary<int, int>();


    public OrbitNode(string name, CelestialBody parent, OrbitalParameters parameters, double capacity)
    {
        this.name = name;
        this.parentBody = parent;
        this.orbitalParameters = parameters;
        this.capacity = capacity;
        this.currentLoad = 0;
    }

    public Vector3d GetPositionOnRing(double time, double anomalyOffset)
    {
        OrbitalParameters tempParams = orbitalParameters;
        tempParams.meanAnomalyAtEpoch += anomalyOffset;

        Vector3d localPos = KeplerMath.GetPositionAtTime(tempParams, time);

        return parentBody.GetAbsolutePosition(time) + localPos;
    }
}