using UnityEngine;

[CreateAssetMenu(fileName = "New Gas", menuName = "Strategy/Templates/Gas")]
public class GasTemplate : ScriptableObject
{
    public byte gasId;
    public string gasName;

    public float greenhouseMultiplier;
    public float toxicity;
    public float molarMass;

    [Header("Mechanics")]
    [Tooltip("The temperature at which this gas freezes out of the atmosphere into Volatiles.")]
    public float freezingPointKelvin = 50f;

    [Tooltip("Does this gas condense into visible clouds? (e.g., H2O, CH4)")]
    public bool formsClouds = false;

    [Tooltip("Is this gas scrubbed from the atmosphere by liquid oceans? (e.g., CO2)")]
    public bool isCarbonCycleGas = false;

    [Header("Visuals")]
    public Color skyColor = new Color(0.5f, 0.7f, 1f, 0.5f);
    public Color cloudColor = new Color(1f, 1f, 1f, 0.8f);
}