using UnityEngine;

public enum MapModeType
{
    Gradient,
    Discrete,
    Resource,
    Political,
    Wind
}

public enum GradientScale
{
    Relative,
    Absolute,
    Anchored 
}

public enum GradientDataTarget
{
    Altitude,
    Temperature,
    Insolation,
    SoilThickness,
    Moisture,
    RainFactor,
    Biomass,
    CellArea
}

[CreateAssetMenu(fileName = "New Map Mode", menuName = "Strategy/Templates/Map Mode")]
public class MapModeTemplate : ScriptableObject
{
    public string modeName;
    public MapModeType modeType;

    [Header("Gradient Settings (If Applicable)")]
    public GradientDataTarget dataTarget;
    public GradientScale scaleType;

    [Tooltip("Used if ScaleType is Absolute. Ignored if Relative.")]
    public float absoluteMin;
    [Tooltip("Used if ScaleType is Absolute. Ignored if Relative.")]
    public float absoluteMax;

    public Color minColor = Color.black;
    public Color maxColor = Color.white;

    [Header("Anchored Gradient Settings")]
    public bool useAnchors;

    public float anchor1Value;
    public Color anchor1Color;

    public float anchor2Value;
    public Color anchor2Color;
}