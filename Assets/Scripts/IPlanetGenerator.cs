public interface IPlanetGenerator
{
    PlanetMeshData Generate(HexSphereTemplate template, CelestialBody body, float noiseScale, float noiseOffset, float waterLevel);
}