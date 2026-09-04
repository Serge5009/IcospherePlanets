using UnityEngine;

public class PlanetMeshData
{
    public Mesh sharedMesh;

    public CellTopology[] topologies;
    public CellClimate[] climates;
    public CellEconomy[] economies;
    public TerrainVisualData[] terrainVisuals;
    public OverlayVisualData[] overlayVisuals;
    public WindVisualData[] windVisuals;
}