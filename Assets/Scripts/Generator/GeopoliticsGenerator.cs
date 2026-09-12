using System.Collections.Generic;
using UnityEngine;

public static class GeopoliticsGenerator
{
    private class NationExpansionState
    {
        public int nationId;
        public float expansionPower;
        public Queue<int> frontierCells;
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

            if (validLandCells.Count == 0) continue;

            int targetNations = rng.Next(15, 35);
            int numNationsToSpawn = Mathf.Min(targetNations, totalNationsAvailable);
            numNationsToSpawn = Mathf.Min(numNationsToSpawn, validLandCells.Count / 100);

            if (numNationsToSpawn <= 0) continue;

            float expectedDist = Mathf.Sqrt(4f * Mathf.PI / data.topologies.Length);
            float neighborDistSq = (expectedDist * 1.5f) * (expectedDist * 1.5f);
            float hashCellSize = expectedDist * 2.0f;

            Dictionary<Vector3Int, List<int>> spatialHash = new Dictionary<Vector3Int, List<int>>();

            for (int i = 0; i < data.topologies.Length; i++)
            {
                Vector3 pos = data.topologies[i].localPosition;
                Vector3Int key = new Vector3Int(
                    Mathf.RoundToInt(pos.x / hashCellSize),
                    Mathf.RoundToInt(pos.y / hashCellSize),
                    Mathf.RoundToInt(pos.z / hashCellSize)
                );

                if (!spatialHash.TryGetValue(key, out List<int> list))
                {
                    list = new List<int>();
                    spatialHash[key] = list;
                }
                list.Add(i);
            }

            List<NationExpansionState> activeNations = new List<NationExpansionState>();
            bool[] isClaimed = new bool[data.economies.Length];

            for (int i = 0; i < numNationsToSpawn; i++)
            {
                int randomIdx = validLandCells[rng.Next(validLandCells.Count)];

                int safety = 0;
                while (isClaimed[randomIdx] && safety < 100)
                {
                    randomIdx = validLandCells[rng.Next(validLandCells.Count)];
                    safety++;
                }

                data.economies[randomIdx].ownerId = i + 1;
                isClaimed[randomIdx] = true;

                float power = rng.NextDouble() > 0.8 ? 3.0f : 1.0f;

                Queue<int> startingFrontier = new Queue<int>();
                startingFrontier.Enqueue(randomIdx);

                activeNations.Add(new NationExpansionState
                {
                    nationId = i + 1,
                    expansionPower = power,
                    frontierCells = startingFrontier
                });
            }

            Debug.Log($"Geopolitics: Spawning {numNationsToSpawn} nations on {body.name} using Spatial Hash BFS.");

            bool expansionOccurred = true;
            int safetyCounter = 0;

            while (expansionOccurred && safetyCounter < 2000)
            {
                expansionOccurred = false;
                safetyCounter++;

                foreach (var nation in activeNations)
                {
                    int claimsThisTurn = Mathf.RoundToInt(nation.expansionPower);

                    for (int c = 0; c < claimsThisTurn; c++)
                    {
                        if (nation.frontierCells.Count == 0) break;

                        int currentCell = nation.frontierCells.Dequeue();
                        Vector3 currentPos = data.topologies[currentCell].localPosition;

                        Vector3Int centerKey = new Vector3Int(
                            Mathf.RoundToInt(currentPos.x / hashCellSize),
                            Mathf.RoundToInt(currentPos.y / hashCellSize),
                            Mathf.RoundToInt(currentPos.z / hashCellSize)
                        );

                        for (int x = -1; x <= 1; x++)
                        {
                            for (int y = -1; y <= 1; y++)
                            {
                                for (int z = -1; z <= 1; z++)
                                {
                                    Vector3Int checkKey = centerKey + new Vector3Int(x, y, z);

                                    if (spatialHash.TryGetValue(checkKey, out List<int> cellList))
                                    {
                                        foreach (int n in cellList)
                                        {
                                            if (isClaimed[n]) continue;
                                            if (data.climates[n].liquidDepth > 0) continue;

                                            float distSq = (data.topologies[n].localPosition - currentPos).sqrMagnitude;

                                            if (distSq < neighborDistSq)
                                            {
                                                float altDiff = Mathf.Abs(data.topologies[n].altitude - data.topologies[currentCell].altitude);
                                                if (altDiff > 1500f && rng.NextDouble() > 0.2)
                                                {
                                                    continue;
                                                }

                                                data.economies[n].ownerId = nation.nationId;
                                                isClaimed[n] = true;
                                                nation.frontierCells.Enqueue(n);
                                                expansionOccurred = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
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