#ifndef CELL_DATA_INCLUDED
#define CELL_DATA_INCLUDED

struct CellVisualData
{
    float4 bedrockColor;
    float4 liquidColor;
    float4 surfaceData;
    float4 politicalColor;
    int isHovered;
    float iceColorR;
    float iceColorG;
    float iceColorB;
    float padding;
};

StructuredBuffer<CellVisualData> _CellVisualData;

void GetCellData_float(float2 encodedId, out float4 terrainColor, out float4 cellColor, out float isHovered)
{
    terrainColor = float4(0.5, 0.5, 0.5, 1.0);
    cellColor = float4(0.0, 0.0, 0.0, 0.0);
    isHovered = 0;
    
#if !defined(SHADERGRAPH_PREVIEW)
    uint id = (uint) round(encodedId.y) * 2000 + (uint) round(encodedId.x);
        
    CellVisualData data = _CellVisualData[id];
    
    float3 finalColor = data.bedrockColor.rgb;
    float hasLiquid = step(0.01, data.surfaceData.z);
    finalColor = lerp(finalColor, data.liquidColor.rgb, hasLiquid);
    
    float3 biomassColor = float3(0.15, 0.45, 0.15);
    finalColor = lerp(finalColor, biomassColor, data.surfaceData.y);

    float3 iceColor = float3(data.iceColorR, data.iceColorG, data.iceColorB);
    finalColor = lerp(finalColor, iceColor, data.surfaceData.x);
    
    terrainColor = float4(finalColor, 1.0);
    
    float4 polColor = data.politicalColor;
    
    if (data.isHovered > 0)
    {
        polColor = float4(1.0, 1.0, 1.0, 0.4);
    }
    
    cellColor = polColor;
    isHovered = (float) data.isHovered;
#endif
}

#endif