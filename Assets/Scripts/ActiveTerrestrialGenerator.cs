using UnityEngine;
using System;

public class ActiveTerrestrialGenerator : IPlanetGenerator
{
    private struct TectonicPlate
    {
        public Vector3 center;
        public Vector3 driftVelocity;
        public BedrockTemplate bedrock;
        public float baseElevation;
        public bool isOceanic;
    }

    public PlanetMeshData Generate(HexSphereTemplate template, CelestialBody body, float noiseScale, float noiseOffset, float waterLevel)
    {
        PlanetMeshData data = new PlanetMeshData();
        data.sharedMesh = template.bakedMesh;
        int cellCount = template.cellCenters.Length;
        GeneratorUtility.InitializeDataArrays(data, cellCount);

        System.Random rng = new System.Random(body.name.GetHashCode());
        bool hasOceans = waterLevel > 100f;

        int plateCount = rng.Next(15, 30);
        TectonicPlate[] plates = new TectonicPlate[plateCount];

        for (int i = 0; i < plateCount; i++)
        {
            double u = rng.NextDouble();
            double v = rng.NextDouble();
            double theta = u * 2.0 * Math.PI;
            double phi = Math.Acos(2.0 * v - 1.0);
            Vector3 center = new Vector3((float)(Math.Sin(phi) * Math.Cos(theta)), (float)(Math.Sin(phi) * Math.Sin(theta)), (float)(Math.Cos(phi))).normalized;

            Vector3 randomDir = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f).normalized;
            Vector3 drift = Vector3.ProjectOnPlane(randomDir, center).normalized;

            BedrockTemplate assignedBedrock = rng.NextDouble() > 0.3 ? body.dominantBedrock : body.secondaryBedrock;
            bool isOceanic = hasOceans && rng.NextDouble() > 0.40f;
            float elevation = isOceanic ? waterLevel - rng.Next(2000, 6000) : waterLevel + rng.Next(1000, 4000);

            plates[i] = new TectonicPlate
            {
                center = center,
                driftVelocity = drift,
                bedrock = assignedBedrock,
                baseElevation = elevation,
                isOceanic = isOceanic
            };
        }

        BedrockTemplate defaultBedrock = DataLibrary.Instance != null ? DataLibrary.Instance.GetBedrock(1) : null;
        LiquidTemplate defaultLiquid = DataLibrary.Instance != null ? DataLibrary.Instance.GetLiquid(1) : null;
        Color fallbackLiquidCol = defaultLiquid != null ? defaultLiquid.shallowColor : Color.blue;

        for (int i = 0; i < cellCount; i++)
        {
            Vector3 localPos = template.cellCenters[i];

            float warpStrength = 0.4f;
            Vector3 warpOffset = new Vector3(
                GeneratorUtility.FBM(localPos, noiseScale * 1.2f, noiseOffset, 3),
                GeneratorUtility.FBM(localPos, noiseScale * 1.2f, noiseOffset + 100f, 3),
                GeneratorUtility.FBM(localPos, noiseScale * 1.2f, noiseOffset + 200f, 3)
            );
            warpOffset = (warpOffset - new Vector3(0.5f, 0.5f, 0.5f)) * 2f;
            Vector3 warpedPos = (localPos + warpOffset * warpStrength).normalized;

            int closestPlateIdx = 0, secondClosestPlateIdx = 0;
            float closestDist = float.MaxValue, secondClosestDist = float.MaxValue;

            for (int p = 0; p < plateCount; p++)
            {
                float dist = (warpedPos - plates[p].center).sqrMagnitude;
                if (dist < closestDist)
                {
                    secondClosestDist = closestDist;
                    secondClosestPlateIdx = closestPlateIdx;
                    closestDist = dist;
                    closestPlateIdx = p;
                }
                else if (dist < secondClosestDist)
                {
                    secondClosestDist = dist;
                    secondClosestPlateIdx = p;
                }
            }

            TectonicPlate primary = plates[closestPlateIdx];
            TectonicPlate secondary = plates[secondClosestPlateIdx];

            float borderDistance = secondClosestDist - closestDist;
            float baseAlt = primary.baseElevation;
            float faultLineModifier = 0f;

            if (borderDistance < 0.08f)
            {
                Vector3 borderDir = (secondary.center - primary.center).normalized;
                float primaryApproach = Vector3.Dot(primary.driftVelocity, borderDir);
                float secondaryApproach = Vector3.Dot(secondary.driftVelocity, -borderDir);
                float convergence = primaryApproach + secondaryApproach;

                float borderIntensity = 1f - (borderDistance / 0.08f);
                borderIntensity *= borderIntensity;

                if (convergence > 0.2f)
                {
                    if (!primary.isOceanic && !secondary.isOceanic) faultLineModifier = borderIntensity * 8000f * convergence;
                    else if (primary.isOceanic && secondary.isOceanic) faultLineModifier = borderIntensity * 3000f * convergence;
                    else faultLineModifier = primary.isOceanic ? borderIntensity * -6000f * convergence : borderIntensity * 6000f * convergence;
                }
                else if (convergence < -0.2f)
                {
                    if (!primary.isOceanic && !secondary.isOceanic) faultLineModifier = borderIntensity * -3000f * Mathf.Abs(convergence);
                    else faultLineModifier = borderIntensity * -1500f;
                }
            }

            float detailNoise = (GeneratorUtility.FBM(localPos, noiseScale * 3f, noiseOffset, 4) - 0.5f) * 2f;
            baseAlt += primary.isOceanic ? Mathf.Max(0, detailNoise * 3000f) : Mathf.Min(0, detailNoise * 3000f);

            float altitude = baseAlt + faultLineModifier;
            byte bedrockId = primary.bedrock != null ? primary.bedrock.bedrockId : (byte)1;

            data.topologies[i] = new CellTopology { id = i, localPosition = localPos, altitude = altitude, bedrockId = bedrockId, windNeighborId = -1 };

            float liquidDepth = (hasOceans && altitude < waterLevel) ? (waterLevel - altitude) / 1000f : 0f;
            data.climates[i] = new CellClimate { localTemperature = 280f, storedHeat = 100f, moisture = liquidDepth > 0 ? 1f : 0f, iceCover = 0f, liquidDepth = Mathf.Clamp01(liquidDepth), biomass = 0f };
            data.economies[i] = new CellEconomy { ownerId = 0, population = 0, infrastructureCap = 0.1f, developmentCap = 0.05f };

            Color bColor = primary.bedrock != null ? primary.bedrock.baseColor : (defaultBedrock != null ? defaultBedrock.baseColor : Color.gray);
            data.visualDataArray[i] = new CellVisualData { bedrockColor = bColor, liquidColor = fallbackLiquidCol, surfaceData = new Vector4(0f, 0f, data.climates[i].liquidDepth, 0f), isHovered = 0 };
        }

        return data;
    }
}