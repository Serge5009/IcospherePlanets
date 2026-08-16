#ifndef CELL_DATA_INCLUDED
#define CELL_DATA_INCLUDED

struct CellVisualData
{
    int ownerId;
    int isHovered;
};

#if defined(UNITY_COMPILER_HLSL) || defined(SHADER_API_D3D11) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PSSL)
    StructuredBuffer<CellVisualData> _CellVisualData;
#endif

void GetCellData_float(float2 encodedId, out float ownerId, out float isHovered)
{
    ownerId = 0;
    isHovered = 0;
    
#if !defined(SHADERGRAPH_PREVIEW)
    int id = (int) round(encodedId.y) * 2000 + (int) round(encodedId.x);
        
    CellVisualData data = _CellVisualData[id];
    ownerId = (float) data.ownerId;
    isHovered = (float) data.isHovered;
#endif
}

#endif