#ifndef TERRAIN_DATA_INCLUDED
#define TERRAIN_DATA_INCLUDED

struct TerrainVisualData
{
    float4 bedrockColor;
    float4 liquidColor;
    float4 surfaceData;
    float iceColorR;
    float iceColorG;
    float iceColorB;
    float padding;
};

StructuredBuffer<TerrainVisualData> _TerrainVisualData;

void GetTerrainData_float(float2 encodedId, out float3 OutColor)
{
    OutColor = float3(0.5, 0.5, 0.5);
    
#if !defined(SHADERGRAPH_PREVIEW)
    uint id = (uint) round(encodedId.y) * 2000 + (uint) round(encodedId.x);
        
    TerrainVisualData data = _TerrainVisualData[id];
    
    float3 finalColor = data.bedrockColor.rgb;
    
    float hasLiquid = step(0.0001, data.surfaceData.z);
    finalColor = lerp(finalColor, data.liquidColor.rgb, hasLiquid);
    
    float3 biomassColor = float3(0.15, 0.45, 0.15);
    finalColor = lerp(finalColor, biomassColor, data.surfaceData.y);

    float3 iceColor = float3(data.iceColorR, data.iceColorG, data.iceColorB);
    finalColor = lerp(finalColor, iceColor, data.surfaceData.x);
    
    OutColor = finalColor;
#endif
}

#endif