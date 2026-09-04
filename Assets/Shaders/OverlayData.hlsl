#ifndef OVERLAY_DATA_INCLUDED
#define OVERLAY_DATA_INCLUDED

struct OverlayVisualData
{
    float4 overlayColor;
};

StructuredBuffer<OverlayVisualData> _OverlayVisualData;

void GetOverlayData_float(float2 encodedId, out float3 OutRGB, out float OutAlpha)
{
    OutRGB = float3(0.0, 0.0, 0.0);
    OutAlpha = 0.0;
    
#if !defined(SHADERGRAPH_PREVIEW)
    uint id = (uint) round(encodedId.y) * 2000 + (uint) round(encodedId.x);
        
    OverlayVisualData data = _OverlayVisualData[id];
    
    OutRGB = data.overlayColor.rgb;
    OutAlpha = data.overlayColor.a;
    
    if (data.overlayColor.a > 0.99)
    {
        OutRGB = float3(1.0, 1.0, 1.0);
        OutAlpha = 0.4;
    }
#endif
}

#endif