using UnityEngine;
using System;

public class ActiveTerrestrialGenerator : IPlanetGenerator
{
    private struct TectonicPlate
    {
        public Vector3 center;
        public Vector3 driftVelocity;
        public byte bedrockId;
        public Color bedrockColor;
        public float baseElevation;
        public bool isOceanic;
    }

    public PlanetMeshData Generate(Mesh mesh, Vector3[] cellCenters, CelestialBody body, float noiseScale, float noiseOffset, float waterLevel)
    {
        PlanetMeshData data = new PlanetMeshData();
        data.sharedMesh = mesh;
        int cellCount = cellCenters.Length;
        GeneratorUtility.InitializeDataArrays(data, cellCount);

        System.Random rng = new System.Random(body.name.GetHashCode());
        bool hasOceans = waterLevel > -5000f;

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

            bool useDominant = rng.NextDouble() > 0.3;
            byte assignedId = useDominant ? body.dominantBedrockId : body.secondaryBedrockId;
            Color assignedColor = useDominant ? body.dominantBedrockColor : body.secondaryBedrockColor;

            bool isOceanic = hasOceans && rng.NextDouble() > 0.40f;
            float elevation = isOceanic ? waterLevel - rng.Next(2000, 5000) : waterLevel + rng.Next(1000, 4000);

            plates[i] = new TectonicPlate
            {
                center = center,
                driftVelocity = drift,
                bedrockId = assignedId,
                bedrockColor = assignedColor,
                baseElevation = elevation,
                isOceanic = isOceanic
            };
        }

        float[] rawAltitudes = new float[cellCount];
        float minAltitude = float.MaxValue;

        for (int i = 0; i < cellCount; i++)
        {
            Vector3 localPos = cellCenters[i];

            float warpStrength = 0.5f;
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

            if (borderDistance < 0.05f)
            {
                Vector3 borderDir = (secondary.center - primary.center).normalized;
                float primaryApproach = Vector3.Dot(primary.driftVelocity, borderDir);
                float secondaryApproach = Vector3.Dot(secondary.driftVelocity, -borderDir);
                float convergence = primaryApproach + secondaryApproach;

                float borderIntensity = 1f - (borderDistance / 0.05f);
                borderIntensity *= borderIntensity;

                if (convergence > 0.2f)
                {
                    if (!primary.isOceanic && !secondary.isOceanic) faultLineModifier = borderIntensity * 8000f * convergence;
                    else if (primary.isOceanic && secondary.isOceanic) faultLineModifier = borderIntensity * 2500f * convergence;
                    else faultLineModifier = primary.isOceanic ? borderIntensity * -5000f * convergence : borderIntensity * 5000f * convergence;
                }
                else if (convergence < -0.2f)
                {
                    if (!primary.isOceanic && !secondary.isOceanic) faultLineModifier = borderIntensity * -3000f * Mathf.Abs(convergence);
                    else faultLineModifier = borderIntensity * -1500f;
                }
            }

            float detailNoise = (GeneratorUtility.FBM(localPos, noiseScale * 4f, noiseOffset, 4) - 0.5f) * 600f;
            baseAlt += primary.isOceanic ? Mathf.Max(0, detailNoise) : Mathf.Min(0, detailNoise);

            float rawAlt = baseAlt + faultLineModifier;
            rawAltitudes[i] = rawAlt;

            if (rawAlt < minAltitude) minAltitude = rawAlt;
        }

        float shiftedWaterLevel = hasOceans ? (waterLevel - minAltitude) : 0f;

        for (int i = 0; i < cellCount; i++)
        {
            Vector3 localPos = cellCenters[i];
            float finalAltitude = rawAltitudes[i] - minAltitude;

            float closestDist = float.MaxValue;
            int closestPlateIdx = 0;
            for (int p = 0; p < plateCount; p++)
            {
                float dist = (localPos - plates[p].center).sqrMagnitude;
                if (dist < closestDist) { closestDist = dist; closestPlateIdx = p; }
            }
            TectonicPlate primary = plates[closestPlateIdx];

            float cosLat = Mathf.Sqrt(1f - localPos.y * localPos.y);
            float sinLat = Mathf.Abs(localPos.y);
            float tiltRatio = Mathf.Clamp01((float)body.axialTilt / 90f);

            float insolation = Mathf.Lerp(cosLat, sinLat, tiltRatio);

            float baseRain = 0f;
            if (hasOceans)
            {
                float distToOcean = primary.isOceanic ? 0f : closestDist;
                baseRain = Mathf.Clamp01(1.0f - (distToOcean * 2f));
                if (finalAltitude > shiftedWaterLevel + 2000f) baseRain *= 0.5f;
            }

            data.topologies[i] = new CellTopology
            {
                id = i,
                localPosition = localPos,
                altitude = finalAltitude,
                bedrockId = primary.bedrockId,
                windNeighborId = -1,
                baseInsolation = insolation,
                rainFactor = baseRain
            };

            float liquidDepth = (hasOceans && finalAltitude < shiftedWaterLevel) ? (shiftedWaterLevel - finalAltitude) / 1000f : 0f;

            data.climates[i] = new CellClimate
            {
                localTemperature = 280f,
                storedHeat = 100f,
                moisture = liquidDepth > 0 ? 1f : 0f,
                snowDepth = 0f,
                iceCover = 0f,
                liquidDepth = Mathf.Clamp01(liquidDepth),
                biomass = 0f
            };

            data.economies[i] = new CellEconomy { ownerId = 0, population = 0, infrastructureCap = 0.1f, developmentCap = 0.05f };
            data.visualDataArray[i] = new CellVisualData { bedrockColor = primary.bedrockColor, liquidColor = body.oceanColor, surfaceData = new Vector4(0f, 0f, data.climates[i].liquidDepth, 0f), isHovered = 0 };
        }

        return data;
    }
}