using UnityEngine;

[CreateAssetMenu(fileName = "New Soil", menuName = "Strategy/Templates/Soil")]
public class SoilTemplate : ScriptableObject
{
    public byte soilId;
    public string soilName;

    [Tooltip("Color of the soil in deep deserts (0 Moisture)")]
    public Color dryColor = new Color(0.7f, 0.6f, 0.4f, 1f);

    [Tooltip("Color of the soil in wet valleys (1 Moisture)")]
    public Color wetColor = new Color(0.3f, 0.2f, 0.1f, 1f);

    [Tooltip("Multiplier for how thick this soil naturally blankets the planet")]
    public float baseThicknessMultiplier = 1.0f;
}