#ifndef POLITICAL_DATA_INCLUDED
#define POLITICAL_DATA_INCLUDED

struct PoliticalVisualData
{
    float4 politicalColor;
};

StructuredBuffer<PoliticalVisualData> _PoliticalVisualData;

void GetPoliticalData_float(float2 encodedId, out float3 OutRGB, out float OutAlpha)
{
    OutRGB = float3(0.0, 0.0, 0.0);
    OutAlpha = 0.0;
    
#if !defined(SHADERGRAPH_PREVIEW)
    uint id = (uint) round(encodedId.y) * 2000 + (uint) round(encodedId.x);
        
    PoliticalVisualData data = _PoliticalVisualData[id];
    
    OutRGB = data.politicalColor.rgb;
    OutAlpha = data.politicalColor.a;
#endif
}

#endif