using UnityEngine;
using System;

public static class PlanetGenerator
{
    private struct TectonicPlate
    {
        public Vector3 center;
        public BedrockTemplate bedrock;
        public float baseElevation;
        public bool isOceanic;
    }

    public static PlanetMeshData GeneratePlanetData(HexSphereTemplate template, CelestialBody body, float noiseScale, float noiseOffset, float waterLevel)
    {
        PlanetMeshData data = new PlanetMeshData();
        data.sharedMesh = template.bakedMesh;

        int cellCount = template.cellCenters.Length;

        data.topologies = new CellTopology[cellCount];
        data.climates = new CellClimate[cellCount];
        data.economies = new CellEconomy[cellCount];
        data.visualDataArray = new CellVisualData[cellCount];

        System.Random rng = new System.Random(body.name.GetHashCode());

        bool hasOceans = waterLevel > 100f;

        // 1. Generate Tectonic Plates
        int plateCount = rng.Next(15, 30);
        TectonicPlate[] plates = new TectonicPlate[plateCount];

        for (int i = 0; i < plateCount; i++)
        {
            double u = rng.NextDouble();
            double v = rng.NextDouble();
            double theta = u * 2.0 * Math.PI;
            double phi = Math.Acos(2.0 * v - 1.0);
            float x = (float)(Math.Sin(phi) * Math.Cos(theta));
            float y = (float)(Math.Sin(phi) * Math.Sin(theta));
            float z = (float)(Math.Cos(phi));

            BedrockTemplate assignedBedrock = rng.NextDouble() > 0.3 ? body.dominantBedrock : body.secondaryBedrock;

            bool isOceanic = false;
            float elevation;

            if (hasOceans)
            {
                // 60% chance of Oceanic Plate, 40% chance of Continental Plate
                isOceanic = rng.NextDouble() > 0.40f;
                elevation = isOceanic ? waterLevel - rng.Next(2000, 6000) : waterLevel + rng.Next(1000, 4000);
            }
            else
            {
                elevation = rng.Next(-4000, 5000);
            }

            plates[i] = new TectonicPlate
            {
                center = new Vector3(x, y, z).normalized,
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

            // 2. Strong Domain Warping (Destroys the straight Voronoi lines)
            // We use FBM here to make the warp highly organic and fractal
            float warpStrength = 0.6f; // Much stronger than before
            Vector3 warpOffset = new Vector3(
                FBM(localPos, noiseScale * 1.2f, noiseOffset, 3),
                FBM(localPos, noiseScale * 1.2f, noiseOffset + 100f, 3),
                FBM(localPos, noiseScale * 1.2f, noiseOffset + 200f, 3)
            );
            warpOffset = (warpOffset - new Vector3(0.5f, 0.5f, 0.5f)) * 2f;
            Vector3 warpedPos = (localPos + warpOffset * warpStrength).normalized;

            // 3. Find closest plates
            int closestPlateIdx = 0;
            int secondClosestPlateIdx = 0;
            float closestDist = float.MaxValue;
            float secondClosestDist = float.MaxValue;

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

            TectonicPlate primaryPlate = plates[closestPlateIdx];
            TectonicPlate secondaryPlate = plates[secondClosestPlateIdx];

            float borderDistance = secondClosestDist - closestDist;
            float baseAlt = primaryPlate.baseElevation;
            float faultLineModifier = 0f;

            // 4. Feature Generation based on Plate Types
            if (hasOceans)
            {
                // Continental Shelves (Smooth transition from land to ocean)
                if (primaryPlate.isOceanic != secondaryPlate.isOceanic && borderDistance < 0.15f)
                {
                    float shelfBlend = Mathf.SmoothStep(0.0f, 0.15f, borderDistance);
                    baseAlt = Mathf.Lerp((primaryPlate.baseElevation + secondaryPlate.baseElevation) * 0.5f, primaryPlate.baseElevation, shelfBlend);
                }

                // Fault Lines (Mountains and Trenches)
                if (borderDistance < 0.05f)
                {
                    float borderIntensity = 1f - (borderDistance / 0.05f);
                    borderIntensity *= borderIntensity; // Non-linear spike

                    if (!primaryPlate.isOceanic && !secondaryPlate.isOceanic)
                    {
                        // Continent vs Continent = Massive fold mountains (Himalayas)
                        faultLineModifier = borderIntensity * 8000f;
                    }
                    else if (primaryPlate.isOceanic && secondaryPlate.isOceanic)
                    {
                        // Ocean vs Ocean = Mid-Ocean Ridge (Stays underwater)
                        faultLineModifier = borderIntensity * 2500f;
                        // Clamp to ensure it rarely breaks the surface
                        if (baseAlt + faultLineModifier > waterLevel - 200f)
                            faultLineModifier = (waterLevel - 200f) - baseAlt;
                    }
                    else
                    {
                        // Subduction Zone (Ocean vs Continent)
                        if (primaryPlate.isOceanic)
                        {
                            // The ocean side gets a deep trench
                            faultLineModifier = borderIntensity * -5000f;
                        }
                        else
                        {
                            // The continent side gets coastal mountains (Andes)
                            faultLineModifier = borderIntensity * 5000f;
                        }
                    }
                }

                // Detail Noise (Lakes and Islands)
                float detailNoise = (FBM(localPos, noiseScale * 3f, noiseOffset, 4) - 0.5f) * 2f;

                if (primaryPlate.isOceanic)
                {
                    // Oceans get positive noise to form islands and seamounts
                    baseAlt += Mathf.Max(0, detailNoise * 3000f);
                }
                else
                {
                    // Continents get negative noise to carve out lakes, valleys, and inland seas
                    baseAlt += Mathf.Min(0, detailNoise * 3000f);
                }
            }
            else
            {
                // Dry Planet Logic (Asteroids, Moons)
                float detailNoise = (Noise3D.Evaluate(localPos, noiseScale * 4f, noiseOffset) - 0.5f) * 3000f;
                baseAlt += detailNoise;

                if (borderDistance < 0.05f)
                {
                    float borderIntensity = 1f - (borderDistance / 0.05f);
                    faultLineModifier = borderIntensity * 3000f; // Just generic ridges
                }
            }

            float altitude = baseAlt + faultLineModifier;

            byte bedrockId = primaryPlate.bedrock != null ? primaryPlate.bedrock.bedrockId : (byte)1;

            data.topologies[i] = new CellTopology
            {
                id = i,
                localPosition = localPos,
                altitude = altitude,
                bedrockId = bedrockId,
                windNeighborId = -1
            };

            float liquidDepth = (hasOceans && altitude < waterLevel) ? (waterLevel - altitude) / 1000f : 0f;

            data.climates[i] = new CellClimate
            {
                localTemperature = 280f,
                storedHeat = 100f,
                moisture = liquidDepth > 0 ? 1f : 0f,
                iceCover = 0f,
                liquidDepth = Mathf.Clamp01(liquidDepth),
                biomass = 0f
            };

            data.economies[i] = new CellEconomy
            {
                ownerId = 0,
                population = 0,
                infrastructureCap = 0.1f,
                developmentCap = 0.05f
            };

            if (body.bodyType == BodyType.Star)
            {
                data.visualDataArray[i] = new CellVisualData
                {
                    bedrockColor = new Vector4(1f, 0.9f, 0.2f, 1f) * 2f,
                    liquidColor = Vector4.zero,
                    surfaceData = Vector4.zero,
                    isHovered = 0
                };
            }
            else
            {
                Color bColor = primaryPlate.bedrock != null ? primaryPlate.bedrock.baseColor : (defaultBedrock != null ? defaultBedrock.baseColor : Color.gray);

                data.visualDataArray[i] = new CellVisualData
                {
                    bedrockColor = bColor,
                    liquidColor = fallbackLiquidCol,
                    surfaceData = new Vector4(
                        data.climates[i].iceCover,
                        data.climates[i].biomass,
                        data.climates[i].liquidDepth,
                        0f
                    ),
                    isHovered = 0
                };
            }
        }

        return data;
    }

    private static float FBM(Vector3 pos, float scale, float offset, int octaves)
    {
        float total = 0f;
        float frequency = scale;
        float amplitude = 1f;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            total += Noise3D.Evaluate(pos, frequency, offset) * amplitude;
            maxValue += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return total / maxValue;
    }
}