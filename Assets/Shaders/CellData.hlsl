#ifndef CELL_DATA_INCLUDED
#define CELL_DATA_INCLUDED

struct CellVisualData
{
    float4 bedrockColor;
    float4 liquidColor;
    float4 surfaceData;
    int isHovered;
    float padding1;
    float padding2;
    float padding3;
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
    
    finalColor = lerp(finalColor, float3(1, 1, 1), data.surfaceData.x);
    finalColor = lerp(finalColor, float3(0.2, 0.6, 0.2), data.surfaceData.y);
    
    terrainColor = float4(finalColor, 1.0);
    cellColor = float4(0.0, 0.0, 0.0, 0.0);
    isHovered = (float) data.isHovered;
#endif
}

#endif