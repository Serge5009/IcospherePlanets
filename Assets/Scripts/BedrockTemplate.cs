using System;
using UnityEngine;

[Serializable]
public struct ResourceYield
{
    public ResourceTemplate resource;
    [Tooltip("Relative weight/chance of this resource generating in this bedrock.")]
    public float weight;
}

[CreateAssetMenu(fileName = "New Bedrock", menuName = "Strategy/Templates/Bedrock")]
public class BedrockTemplate : ScriptableObject
{
    public byte bedrockId;
    public string bedrockName;

    [Tooltip("Temperature at which this rock turns into Lava (Kelvin).")]
    public float meltingPointKelvin = 1473.15f;

    [Header("Visuals")]
    public Color baseColor = Color.gray;

    [Header("Procedural Generation")]
    public ResourceYield[] potentialResources;
}