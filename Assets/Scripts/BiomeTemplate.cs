using UnityEngine;

[CreateAssetMenu(fileName = "New Biome", menuName = "Strategy/Biome Template")]
public class BiomeTemplate : ScriptableObject
{
    public string biomeName;
    public Color biomeColor = Color.white;
    public bool isWater;
}