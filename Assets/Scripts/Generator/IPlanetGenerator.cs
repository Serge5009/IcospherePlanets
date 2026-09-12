using UnityEngine;

public interface IPlanetGenerator
{
    PlanetMeshData Generate(Mesh mesh, Vector3[] cellCenters, CelestialBody body, float noiseScale, float noiseOffset, float waterLevel);
}