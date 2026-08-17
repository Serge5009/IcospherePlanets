using UnityEngine;

public struct Cell
{
    public int id;
    public Vector3 localPosition;

    public int ownerId;

    public float altitude;
    public BiomeTemplate currentBiome;
}