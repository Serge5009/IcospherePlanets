using UnityEngine;
using System;

[Serializable]
public struct HexSphereVariant
{
    [Tooltip("The baked mesh for this variant.")]
    public Mesh bakedMesh;

    [Tooltip("The normalized local center position of every cell.")]
    public Vector3[] cellCenters;

    [Tooltip("The fraction of the total surface area this cell occupies (Sum of all = 1.0).")]
    public float[] areaFractions;
}

[CreateAssetMenu(fileName = "New Hex Sphere", menuName = "Strategy/Hex Sphere Template")]
public class HexSphereTemplate : ScriptableObject
{
    [Tooltip("The subdivision level of this sphere")]
    public int subdivisions;

    [Tooltip("The number of Lloyd's Relaxation passes used to generate this base grid.")]
    public int relaxationPasses;

    [Header("Topology (Shared across all variants)")]
    [Tooltip("Index into the neighbors array where this cell's neighbors begin.")]
    public int[] neighborOffsets;

    [Tooltip("How many neighbors this cell has (5 or 6).")]
    public byte[] neighborCounts;

    [Tooltip("Flattened 1D array of all neighbor connections.")]
    public int[] neighbors;

    [Header("Visual & Spatial Variants")]
    [Tooltip("Variant 0 is always the perfect sphere. Variants 1+ are distorted asteroids.")]
    public HexSphereVariant[] variants;
}