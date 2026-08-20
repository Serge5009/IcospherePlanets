using UnityEngine;

[CreateAssetMenu(fileName = "New Gas", menuName = "Strategy/Templates/Gas")]
public class GasTemplate : ScriptableObject
{
    public byte gasId;
    public string gasName;

    [Tooltip("How much this gas contributes to the global temperature. 1.0 = Earth normal, higher = Venus runaway.")]
    public float greenhouseMultiplier;

    [Tooltip("How toxic this gas is to Terran life. 0 = Breathable, 1 = Lethal.")]
    public float toxicity;

    [Tooltip("Heavier gases (higher mass) are harder for the solar wind to strip away.")]
    public float molarMass;
}