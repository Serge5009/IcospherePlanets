using UnityEngine;

[CreateAssetMenu(fileName = "New Star Class", menuName = "Strategy/Star Class")]
public class StarClassTemplate : ScriptableObject
{
    public string className = "G-Type (Yellow Dwarf)";

    [Tooltip("Mass relative to our Sun (1.0 = 1 Solar Mass)")]
    public float minSolarMass = 0.8f;
    public float maxSolarMass = 1.04f;

    public Color starColor = Color.yellow;
}