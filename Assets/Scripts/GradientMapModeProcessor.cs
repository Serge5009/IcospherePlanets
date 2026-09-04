using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

public class GradientMapModeProcessor : IMapModeProcessor
{
    [BurstCompile]
    private struct PaintGradientJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<CellTopology> topologies;
        [ReadOnly] public NativeArray<CellClimate> climates;
        public NativeArray<OverlayVisualData> overlays;

        public bool isAltitude;
        public bool useAnchors;

        public float minVal;
        public float maxVal;
        public Vector4 minColor;
        public Vector4 maxColor;

        public float anchor1Val;
        public Vector4 anchor1Color;
        public float anchor2Val;
        public Vector4 anchor2Color;

        public void Execute(int i)
        {
            float val = isAltitude ? topologies[i].altitude : climates[i].localTemperature;
            Vector4 finalColor;

            if (useAnchors)
            {
                if (val < anchor1Val)
                {
                    float t = Mathf.InverseLerp(minVal, anchor1Val, val);
                    finalColor = Vector4.Lerp(minColor, anchor1Color, t);
                }
                else if (val < anchor2Val)
                {
                    float t = Mathf.InverseLerp(anchor1Val, anchor2Val, val);
                    finalColor = Vector4.Lerp(anchor1Color, anchor2Color, t);
                }
                else
                {
                    float t = Mathf.InverseLerp(anchor2Val, maxVal, val);
                    finalColor = Vector4.Lerp(anchor2Color, maxColor, t);
                }
            }
            else
            {
                float t = Mathf.InverseLerp(minVal, maxVal, val);
                finalColor = Vector4.Lerp(minColor, maxColor, t);
            }

            OverlayVisualData overlay = overlays[i];

            if (overlay.overlayColor.w > 0.99f) finalColor = new Vector4(1f, 1f, 1f, 1f);
            else finalColor.w = 0.85f;

            overlay.overlayColor = finalColor;
            overlays[i] = overlay;
        }
    }

    public void ApplyMode(PlanetMeshData meshData, MapModeTemplate template, byte subModeId)
    {
        NativeArray<CellTopology> nativeTopos = new NativeArray<CellTopology>(meshData.topologies, Allocator.TempJob);
        NativeArray<CellClimate> nativeClimates = new NativeArray<CellClimate>(meshData.climates, Allocator.TempJob);
        NativeArray<OverlayVisualData> nativeOverlays = new NativeArray<OverlayVisualData>(meshData.overlayVisuals, Allocator.TempJob);

        bool isAltitude = template.modeName.ToLower().Contains("altitude");

        float actualMin = template.absoluteMin;
        float actualMax = template.absoluteMax;

        if (template.scaleType == GradientScale.Relative)
        {
            actualMin = float.MaxValue;
            actualMax = float.MinValue;

            for (int i = 0; i < meshData.topologies.Length; i++)
            {
                float val = isAltitude ? meshData.topologies[i].altitude : meshData.climates[i].localTemperature;
                if (val < actualMin) actualMin = val;
                if (val > actualMax) actualMax = val;
            }
        }

        PaintGradientJob job = new PaintGradientJob
        {
            topologies = nativeTopos,
            climates = nativeClimates,
            overlays = nativeOverlays,

            isAltitude = isAltitude,
            useAnchors = template.useAnchors,

            minVal = actualMin,
            maxVal = actualMax,
            minColor = template.minColor,
            maxColor = template.maxColor,

            anchor1Val = template.anchor1Value,
            anchor1Color = template.anchor1Color,
            anchor2Val = template.anchor2Value,
            anchor2Color = template.anchor2Color
        };

        job.Schedule(meshData.overlayVisuals.Length, 64).Complete();

        nativeOverlays.CopyTo(meshData.overlayVisuals);

        nativeTopos.Dispose();
        nativeClimates.Dispose();
        nativeOverlays.Dispose();
    }
}