using System;

public class EconomyModule : ISimulationModule
{
    public void Initialize()
    {
    }

    public bool SimulateDay(CelestialBody body)
    {
        return false;
    }

    public bool SimulateMonth(CelestialBody body)
    {
        return false;
    }

    public bool SimulateYear(CelestialBody body)
    {
        return false;
    }
}