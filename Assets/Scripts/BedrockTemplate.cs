using System;
using UnityEngine;

[Serializable]
public struct ResourceYield
{
    public ResourceTemplate resource;
    public float weight;
}

[CreateAssetMenu(fileName = "New Bedrock", menuName = "Strategy/Templates/Bedrock")]
public class BedrockTemplate : ScriptableObject
{
    public byte bedrockId;
    public string bedrockName;

    public float meltingPointKelvin = 1473.15f;

    [Header("Mechanics")]
    [Tooltip("How much this bedrock contributes to the planet's core mass. (Silicate = ~0.3, Metallic = ~0.6)")]
    public float coreMassModifier = 0.3f;

    [Header("Visuals")]
    public Color baseColor = Color.gray;

    [Header("Procedural Generation")]
    public ResourceYield[] potentialResources;
}