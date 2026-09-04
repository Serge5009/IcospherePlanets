using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

public class ClearMapMode : IMapModeProcessor
{
    [BurstCompile]
    private struct ClearJob : IJobParallelFor
    {
        public NativeArray<OverlayVisualData> overlays;

        public void Execute(int i)
        {
            OverlayVisualData overlay = overlays[i];

            if (overlay.overlayColor.w < 0.99f)
            {
                overlay.overlayColor = new Vector4(0, 0, 0, 0);
            }

            overlays[i] = overlay;
        }
    }

    public void ApplyMode(PlanetMeshData meshData, MapModeTemplate template, byte subModeId)
    {
        NativeArray<OverlayVisualData> nativeOverlays = new NativeArray<OverlayVisualData>(meshData.overlayVisuals, Allocator.TempJob);

        ClearJob job = new ClearJob { overlays = nativeOverlays };

        job.Schedule(meshData.overlayVisuals.Length, 64).Complete();

        nativeOverlays.CopyTo(meshData.overlayVisuals);

        nativeOverlays.Dispose();
    }
}