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

    [Header("Visuals")]
    [Tooltip("Does this gas condense into visible clouds? (e.g., H2O, CH4)")]
    public bool formsClouds = false;

    [Tooltip("The base color of the sky (Rayleigh scattering). Alpha controls intensity.")]
    public Color skyColor = new Color(0.5f, 0.7f, 1f, 0.5f);

    [Tooltip("The color of the clouds formed by this gas. Alpha controls cloud thickness.")]
    public Color cloudColor = new Color(1f, 1f, 1f, 0.8f);
}