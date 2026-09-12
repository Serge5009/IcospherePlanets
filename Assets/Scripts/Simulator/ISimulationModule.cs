public interface ISimulationModule
{
    void Initialize();

    bool SimulateDay(CelestialBody body);

    bool SimulateMonth(CelestialBody body);

    bool SimulateYear(CelestialBody body);
}