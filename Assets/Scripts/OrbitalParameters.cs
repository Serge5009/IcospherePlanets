using System;

[Serializable]
public struct OrbitalParameters
{
    public double semiMajorAxis;
    public double eccentricity;
    public double inclination;
    public double longitudeOfAscendingNode;
    public double argumentOfPeriapsis;
    public double meanAnomalyAtEpoch;
    public double parentMu;
}

public static class KeplerMath
{
    public static Vector3d GetPositionAtTime(OrbitalParameters orbit, double timeSeconds)
    {
        double n = Math.Sqrt(orbit.parentMu / Math.Pow(orbit.semiMajorAxis, 3));

        double M = orbit.meanAnomalyAtEpoch + n * timeSeconds;
        M = M % (2 * Math.PI);
        if (M < 0) M += 2 * Math.PI;

        double E = SolveKeplerEquation(M, orbit.eccentricity);

        double v = 2 * Math.Atan2(
            Math.Sqrt(1 + orbit.eccentricity) * Math.Sin(E / 2),
            Math.Sqrt(1 - orbit.eccentricity) * Math.Cos(E / 2)
        );

        double r = orbit.semiMajorAxis * (1 - orbit.eccentricity * Math.Cos(E));

        double cosVPlusOmega = Math.Cos(v + orbit.argumentOfPeriapsis);
        double sinVPlusOmega = Math.Sin(v + orbit.argumentOfPeriapsis);
        double cosNode = Math.Cos(orbit.longitudeOfAscendingNode);
        double sinNode = Math.Sin(orbit.longitudeOfAscendingNode);
        double cosInc = Math.Cos(orbit.inclination);
        double sinInc = Math.Sin(orbit.inclination);

        double x = r * (cosNode * cosVPlusOmega - sinNode * sinVPlusOmega * cosInc);
        double y = r * (sinNode * cosVPlusOmega + cosNode * sinVPlusOmega * cosInc);
        double z = r * (sinVPlusOmega * sinInc);

        return new Vector3d(x, z, y);
    }

    private static double SolveKeplerEquation(double M, double e)
    {
        double E = M;
        double delta = 1;
        int maxIterations = 15;
        int i = 0;

        while (Math.Abs(delta) > 1e-7 && i < maxIterations)
        {
            delta = (E - e * Math.Sin(E) - M) / (1 - e * Math.Cos(E));
            E -= delta;
            i++;
        }
        return E;
    }
}