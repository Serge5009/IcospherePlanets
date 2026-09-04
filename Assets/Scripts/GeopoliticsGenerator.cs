using System.Collections.Generic;
using UnityEngine;

public static class GeopoliticsGenerator
{
    private struct Capital
    {
        public int cellId;
        public int nationId;
        public Vector3 position;
    }

    public static void GenerateNations(List<CelestialBody> bodies)
    {
        Debug.Log("Generating Geopolitics...");

        int totalNationsAvailable = GeopoliticsManager.Instance.GetTotalNations() - 1;
        if (totalNationsAvailable <= 0)
        {
            Debug.LogWarning("GeopoliticsGenerator: No starting nations defined in GeopoliticsManager!");
            return;
        }

        double starSolarMasses = SystemDataGenerator.Instance.star.massKg / AstroMath.SOLAR_MASS_KG;
        double starLuminosity = AstroMath.CalculateLuminosity(starSolarMasses);

        (double habInner, double habOuter) = AstroMath.CalculateHabitableZone(starLuminosity);

        bool hasSpawnedNations = false;

        foreach (var body in bodies)
        {
            if (body.bodyType != BodyType.RockyPlanet) continue;

            double distanceAU = body.orbit.semiMajorAxis / AstroMath.AU_TO_KM;

            if (distanceAU < habInner || distanceAU > habOuter) continue;

            PlanetMeshData data = body.localViewData;
            if (data == null) continue;

            System.Random rng = new System.Random(body.name.GetHashCode());

            List<int> validLandCells = new List<int>();
            for (int i = 0; i < data.climates.Length; i++)
            {
                if (data.climates[i].liquidDepth <= 0)
                {
                    validLandCells.Add(i);
                }
            }

            if (validLandCells.Count == 0)
            {
                Debug.Log($"Geopolitics: {body.name} is in the Goldilocks Zone, but is a 100% global ocean. Skipping.");
                continue;
            }

            int numNationsToSpawn = Mathf.Min(rng.Next(2, 6), totalNationsAvailable);
            List<Capital> capitals = new List<Capital>();

            for (int i = 0; i < numNationsToSpawn; i++)
            {
                int randomIdx = validLandCells[rng.Next(validLandCells.Count)];
                capitals.Add(new Capital
                {
                    cellId = randomIdx,
                    nationId = i + 1,
                    position = data.topologies[randomIdx].localPosition
                });
            }

            Debug.Log($"Geopolitics: FORCED SPAWN of {numNationsToSpawn} nations on {body.name} (Distance: {distanceAU:F2} AU)!");

            for (int i = 0; i < data.economies.Length; i++)
            {
                if (data.climates[i].liquidDepth > 0)
                {
                    data.economies[i].ownerId = 0;
                    continue;
                }

                Vector3 localPos = data.topologies[i].localPosition;

                float warpStrength = 0.15f;
                Vector3 warpOffset = new Vector3(
                    GeneratorUtility.FBM(localPos, body.noiseScale * 2f, body.noiseOffset, 2),
                    GeneratorUtility.FBM(localPos, body.noiseScale * 2f, body.noiseOffset + 100f, 2),
                    GeneratorUtility.FBM(localPos, body.noiseScale * 2f, body.noiseOffset + 200f, 2)
                );
                warpOffset = (warpOffset - new Vector3(0.5f, 0.5f, 0.5f)) * 2f;
                Vector3 warpedPos = (localPos + warpOffset * warpStrength).normalized;

                float closestDist = float.MaxValue;
                int bestNationId = 0;

                foreach (var cap in capitals)
                {
                    float dist = Vector3.Distance(warpedPos, cap.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        bestNationId = cap.nationId;
                    }
                }

                data.economies[i].ownerId = bestNationId;
            }

            if (body.systemViewData != null && body.lowToHighMap != null)
            {
                for (int i = 0; i < body.systemViewData.economies.Length; i++)
                {
                    int highResIdx = body.lowToHighMap[i];
                    body.systemViewData.economies[i].ownerId = data.economies[highResIdx].ownerId;
                }
            }

            hasSpawnedNations = true;
            break;
        }

        if (!hasSpawnedNations)
        {
            Debug.LogWarning("Geopolitics: No Rocky Planets found in the Goldilocks Zone!");
        }
    }
}