using UnityEngine;

public struct CellTopology
{
    public int id;
    public Vector3 localPosition;
    public float altitude;
    public byte bedrockId;
    public int windNeighborId;

    public float baseInsolation;
    public float rainFactor;
}