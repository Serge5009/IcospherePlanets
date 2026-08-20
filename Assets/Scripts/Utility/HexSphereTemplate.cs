using UnityEngine;

[CreateAssetMenu(fileName = "New Hex Sphere", menuName = "Strategy/Hex Sphere Template")]
public class HexSphereTemplate : ScriptableObject
{
    [Tooltip("The subdivision level of this sphere")]
    public int subdivisions;

    [Tooltip("The baked shared mesh")]
    public Mesh bakedMesh;

    [Tooltip("The normalized local center position of every cell. Index matches Cell ID.")]
    public Vector3[] cellCenters;
}