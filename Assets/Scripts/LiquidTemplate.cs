using UnityEngine;

[CreateAssetMenu(fileName = "New Liquid", menuName = "Strategy/Templates/Liquid")]
public class LiquidTemplate : ScriptableObject
{
    public byte liquidId;
    public string liquidName;

    [Header("Phase Physics (At 1 ATM)")]
    public float baseFreezingPointKelvin = 273.15f;
    public float baseBoilingPointKelvin = 373.15f;

    [Tooltip("The gas this liquid turns into when it boils or evaporates.")]
    public GasTemplate evaporatesInto;

    [Header("Visuals")]
    public Color shallowColor = new Color(0.2f, 0.6f, 1.0f, 1f);
    public Color deepColor = new Color(0.0f, 0.1f, 0.4f, 1f);
    public Color iceColor = Color.white;
}