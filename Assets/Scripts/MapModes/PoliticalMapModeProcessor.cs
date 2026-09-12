using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

public class PoliticalMapModeProcessor : IMapModeProcessor
{
    [BurstCompile]
    private struct PaintPoliticalJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<CellEconomy> economies;
        [ReadOnly] public NativeArray<CellClimate> climates;
        [ReadOnly] public NativeArray<Vector4> nationColors;
        public NativeArray<PoliticalVisualData> politicalVisuals;

        public void Execute(int i)
        {
            int ownerId = economies[i].ownerId;
            float liquidDepth = climates[i].liquidDepth;

            Vector4 finalColor;

            if (liquidDepth > 0)
            {
                finalColor = new Vector4(0, 0, 0, 0);
            }
            else
            {
                if (ownerId >= 0 && ownerId < nationColors.Length) finalColor = nationColors[ownerId];
                else finalColor = nationColors[0];

                if (ownerId != 0) finalColor.w = 0.7f;
                else finalColor.w = 0.3f;
            }

            PoliticalVisualData polData = politicalVisuals[i];
            polData.politicalColor = finalColor;
            politicalVisuals[i] = polData;
        }
    }

    public void ApplyMode(PlanetMeshData meshData, MapModeTemplate template, byte subModeId)
    {
        NativeArray<CellEconomy> nativeEconomies = new NativeArray<CellEconomy>(meshData.economies, Allocator.TempJob);
        NativeArray<CellClimate> nativeClimates = new NativeArray<CellClimate>(meshData.climates, Allocator.TempJob);
        NativeArray<PoliticalVisualData> nativePolitical = new NativeArray<PoliticalVisualData>(meshData.politicalVisuals, Allocator.TempJob);

        Vector4[] colors = GeopoliticsManager.Instance.GetNationColorsAsArray();
        NativeArray<Vector4> nativeColors = new NativeArray<Vector4>(colors, Allocator.TempJob);

        PaintPoliticalJob job = new PaintPoliticalJob
        {
            economies = nativeEconomies,
            climates = nativeClimates,
            nationColors = nativeColors,
            politicalVisuals = nativePolitical
        };

        job.Schedule(meshData.politicalVisuals.Length, 64).Complete();

        nativePolitical.CopyTo(meshData.politicalVisuals);

        nativeEconomies.Dispose();
        nativeClimates.Dispose();
        nativePolitical.Dispose();
        nativeColors.Dispose();
    }
}