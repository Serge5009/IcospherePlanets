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
        [ReadOnly] public NativeArray<float> areaFractions;

        public NativeArray<OverlayVisualData> overlays;

        public GradientDataTarget dataTarget;

        public bool useAnchors;

        public float minVal;
        public float maxVal;
        public Vector4 minColor;
        public Vector4 maxColor;

        public float anchor1Val;
        public Vector4 anchor1Color;
        public float anchor2Val;
        public Vector4 anchor2Color;

        public float planetMinAltitude;
        public float planetMaxAltitude;
        public float atmosphericPressure;
        public float globalSoilThickness;
        public float totalSurfaceAreaSqKm;

        public void Execute(int i)
        {
            CellTopology topo = topologies[i];
            CellClimate clim = climates[i];

            float val = 0f;

            switch (dataTarget)
            {
                case GradientDataTarget.Altitude:
                    val = topo.altitude;
                    break;
                case GradientDataTarget.Temperature:
                    val = clim.localTemperature;
                    break;
                case GradientDataTarget.Insolation:
                    val = topo.baseInsolation;
                    break;
                case GradientDataTarget.Moisture:
                    val = clim.moisture;
                    break;
                case GradientDataTarget.RainFactor:
                    val = topo.rainFactor;
                    break;
                case GradientDataTarget.Biomass:
                    val = clim.biomass;
                    break;
                case GradientDataTarget.CellArea:
                    val = areaFractions[i] * totalSurfaceAreaSqKm;
                    break;
                case GradientDataTarget.SoilThickness:
                    float tAlt = (topo.altitude - planetMinAltitude) / Mathf.Max(1f, planetMaxAltitude - planetMinAltitude);
                    float baseThickness = 1.0f - tAlt;
                    float curvePower = 1.0f + (atmosphericPressure * 2.0f);
                    float thickness = Mathf.Pow(Mathf.Max(0f, baseThickness), curvePower);
                    thickness -= topo.rainFactor * tAlt * 0.5f;
                    thickness *= globalSoilThickness;
                    val = Mathf.Clamp01(thickness);
                    break;
            }

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
        CelestialBody ownerBody = null;
        foreach (var body in SystemDataGenerator.Instance.allBodies)
        {
            if (body.localViewData == meshData || body.systemViewData == meshData)
            {
                ownerBody = body;
                break;
            }
        }

        if (ownerBody == null) return;

        NativeArray<CellTopology> nativeTopos = new NativeArray<CellTopology>(meshData.topologies, Allocator.TempJob);
        NativeArray<CellClimate> nativeClimates = new NativeArray<CellClimate>(meshData.climates, Allocator.TempJob);
        NativeArray<OverlayVisualData> nativeOverlays = new NativeArray<OverlayVisualData>(meshData.overlayVisuals, Allocator.TempJob);

        float[] fractions = ownerBody.geometryTemplate.variants[ownerBody.variantIndex].areaFractions;
        NativeArray<float> nativeFractions = new NativeArray<float>(fractions, Allocator.TempJob);

        float actualMin = template.absoluteMin;
        float actualMax = template.absoluteMax;

        float planetMinAlt = float.MaxValue;
        float planetMaxAlt = float.MinValue;

        for (int i = 0; i < meshData.topologies.Length; i++)
        {
            float alt = meshData.topologies[i].altitude;
            if (alt < planetMinAlt) planetMinAlt = alt;
            if (alt > planetMaxAlt) planetMaxAlt = alt;
        }

        float oceanFraction = 0f;
        if (ownerBody.waterLevel > -5000f)
        {
            int submergedCells = 0;
            foreach (var topo in meshData.topologies) if (topo.altitude < ownerBody.waterLevel) submergedCells++;
            oceanFraction = (float)submergedCells / meshData.topologies.Length;
        }

        float tempFactor = Mathf.Clamp01(AstroMath.CalculateBlackbodyTemperature(AstroMath.CalculateLuminosity(ownerBody.parent.massKg / AstroMath.SOLAR_MASS_KG), ownerBody.orbit.semiMajorAxis / AstroMath.AU_TO_KM, 0.3f) / 288f);
        float pressureFactor = Mathf.Clamp01((float)ownerBody.surfacePressureAtm / 0.05f);
        float rainStrength = oceanFraction * tempFactor * pressureFactor;
        float globalSoil = ownerBody.soilBaseThickness * (1.0f + (float)ownerBody.surfacePressureAtm) * (float)(ownerBody.surfaceGravity / 9.8) * (1.0f + rainStrength);

        if (template.scaleType == GradientScale.Relative)
        {
            actualMin = float.MaxValue;
            actualMax = float.MinValue;

            for (int i = 0; i < meshData.topologies.Length; i++)
            {
                float val = 0f;
                switch (template.dataTarget)
                {
                    case GradientDataTarget.Altitude: val = meshData.topologies[i].altitude; break;
                    case GradientDataTarget.Temperature: val = meshData.climates[i].localTemperature; break;
                    case GradientDataTarget.Insolation: val = meshData.topologies[i].baseInsolation; break;
                    case GradientDataTarget.Moisture: val = meshData.climates[i].moisture; break;
                    case GradientDataTarget.RainFactor: val = meshData.topologies[i].rainFactor; break;
                    case GradientDataTarget.Biomass: val = meshData.climates[i].biomass; break;
                    case GradientDataTarget.CellArea: val = fractions[i] * (float)ownerBody.totalSurfaceAreaSqKm; break;
                    case GradientDataTarget.SoilThickness:
                        float tAlt = (meshData.topologies[i].altitude - planetMinAlt) / Mathf.Max(1f, planetMaxAlt - planetMinAlt);
                        float baseThickness = 1.0f - tAlt;
                        float curvePower = 1.0f + ((float)ownerBody.surfacePressureAtm * 2.0f);
                        float thickness = Mathf.Pow(Mathf.Max(0f, baseThickness), curvePower);
                        thickness -= meshData.topologies[i].rainFactor * tAlt * 0.5f;
                        thickness *= globalSoil;
                        val = Mathf.Clamp01(thickness);
                        break;
                }

                if (val < actualMin) actualMin = val;
                if (val > actualMax) actualMax = val;
            }

            if (actualMin == actualMax) actualMax += 0.001f;
        }

        PaintGradientJob job = new PaintGradientJob
        {
            topologies = nativeTopos,
            climates = nativeClimates,
            areaFractions = nativeFractions,
            overlays = nativeOverlays,

            dataTarget = template.dataTarget,
            useAnchors = template.useAnchors,

            minVal = actualMin,
            maxVal = actualMax,
            minColor = template.minColor,
            maxColor = template.maxColor,

            anchor1Val = template.anchor1Value,
            anchor1Color = template.anchor1Color,
            anchor2Val = template.anchor2Value,
            anchor2Color = template.anchor2Color,

            planetMinAltitude = planetMinAlt,
            planetMaxAltitude = planetMaxAlt,
            atmosphericPressure = (float)ownerBody.surfacePressureAtm,
            globalSoilThickness = globalSoil,
            totalSurfaceAreaSqKm = (float)ownerBody.totalSurfaceAreaSqKm
        };

        job.Schedule(meshData.overlayVisuals.Length, 64).Complete();

        nativeOverlays.CopyTo(meshData.overlayVisuals);

        nativeTopos.Dispose();
        nativeClimates.Dispose();
        nativeFractions.Dispose();
        nativeOverlays.Dispose();
    }
}