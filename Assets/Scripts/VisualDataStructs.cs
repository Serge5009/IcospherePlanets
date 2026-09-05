using UnityEngine;

public struct TerrainVisualData
{
    public Vector4 bedrockColor;
    public Vector4 liquidColor;

    public Vector4 surfaceData;

    public float iceColorR;
    public float iceColorG;
    public float iceColorB;
    public float padding;
}

public struct OverlayVisualData
{
    public Vector4 overlayColor;
}

public struct PoliticalVisualData
{
    public Vector4 politicalColor;
}

public struct WindVisualData
{
    public Vector4 windVector;
}