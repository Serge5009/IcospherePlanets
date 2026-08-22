using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

[BurstCompile]
public struct MapLowToHighJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Vector3> lowCenters;
    [ReadOnly] public NativeArray<Vector3> highCenters;
    public NativeArray<int> mapping;

    public void Execute(int i)
    {
        Vector3 lowPos = lowCenters[i];
        float closestDist = float.MaxValue;
        int closestIdx = 0;

        for (int j = 0; j < highCenters.Length; j++)
        {
            float dist = (lowPos - highCenters[j]).sqrMagnitude;
            if (dist < closestDist)
            {
                closestDist = dist;
                closestIdx = j;
            }
        }
        mapping[i] = closestIdx;
    }
}

public class SystemMeshGenerator : MonoBehaviour
{
    public static SystemMeshGenerator Instance { get; private set; }

    public int systemViewMaxSubdivisions = 4;

    [Header("Baked Templates")]
    public HexSphereTemplate[] bakedTemplates;

    private Dictionary<PlanetArchetype, IPlanetGenerator> generators;
    private Mesh[] cachedMeshes;
    private Vector3[][] cachedCellCenters;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        generators = new Dictionary<PlanetArchetype, IPlanetGenerator>
        {
            { PlanetArchetype.ActiveTerrestrial, new ActiveTerrestrialGenerator() },
            { PlanetArchetype.DeadTerrestrial, new BarrenGenerator() },
            { PlanetArchetype.ActiveIce, new ActiveTerrestrialGenerator() },
            { PlanetArchetype.Barren, new BarrenGenerator() },
            { PlanetArchetype.GasGiant, new GasGiantGenerator() }
        };

        if (bakedTemplates != null)
        {
            cachedMeshes = new Mesh[bakedTemplates.Length];
            cachedCellCenters = new Vector3[bakedTemplates.Length][];
            for (int i = 0; i < bakedTemplates.Length; i++)
            {
                if (bakedTemplates[i] != null)
                {
                    cachedMeshes[i] = bakedTemplates[i].bakedMesh;
                    cachedCellCenters[i] = bakedTemplates[i].cellCenters;
                }
            }
        }
    }

    public async Task GenerateMeshesAsync(List<CelestialBody> bodies, Action<string> onProgress = null)
    {
        if (cachedMeshes == null || cachedMeshes.Length == 0) return;

        for (int i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            int renderSubs = Mathf.Clamp(Mathf.Min(body.dataSubdivisions, systemViewMaxSubdivisions), 0, cachedMeshes.Length - 1);

            Mesh mesh = cachedMeshes[renderSubs];
            Vector3[] centers = cachedCellCenters[renderSubs];

            IPlanetGenerator generator = generators[body.archetype];
            body.systemViewData = generator.Generate(mesh, centers, body, body.noiseScale, body.noiseOffset, body.waterLevel);

            SystemDisplayManager.Instance.SpawnVisualHexSphere(body);

            onProgress?.Invoke($"Spawning System Meshes: {i + 1} / {bodies.Count}");
            await Task.Yield();
        }

        int maxConcurrency = Mathf.Max(1, Environment.ProcessorCount - 1);
        SemaphoreSlim concurrencySemaphore = new SemaphoreSlim(maxConcurrency);

        List<Task> highResTasks = new List<Task>();
        int totalTasks = bodies.Count;
        int completedTasks = 0;

        foreach (var body in bodies)
        {
            highResTasks.Add(Task.Run(() =>
            {
                concurrencySemaphore.Wait();
                try
                {
                    int highResSubs = Mathf.Clamp(body.dataSubdivisions, 0, cachedMeshes.Length - 1);
                    int lowResSubs = Mathf.Clamp(Mathf.Min(body.dataSubdivisions, systemViewMaxSubdivisions), 0, cachedMeshes.Length - 1);

                    Mesh mesh = cachedMeshes[highResSubs];
                    Vector3[] highCenters = cachedCellCenters[highResSubs];
                    Vector3[] lowCenters = cachedCellCenters[lowResSubs];

                    IPlanetGenerator generator = generators[body.archetype];
                    body.localViewData = generator.Generate(mesh, highCenters, body, body.noiseScale, body.noiseOffset, body.waterLevel);

                    NativeArray<Vector3> nativeLow = new NativeArray<Vector3>(lowCenters, Allocator.TempJob);
                    NativeArray<Vector3> nativeHigh = new NativeArray<Vector3>(highCenters, Allocator.TempJob);
                    NativeArray<int> nativeMap = new NativeArray<int>(lowCenters.Length, Allocator.TempJob);

                    MapLowToHighJob mapJob = new MapLowToHighJob
                    {
                        lowCenters = nativeLow,
                        highCenters = nativeHigh,
                        mapping = nativeMap
                    };

                    mapJob.Schedule(lowCenters.Length, 64).Complete();

                    body.lowToHighMap = nativeMap.ToArray();

                    nativeLow.Dispose();
                    nativeHigh.Dispose();
                    nativeMap.Dispose();

                    body.isHighResReady = true;
                }
                finally
                {
                    concurrencySemaphore.Release();
                }
            }));
        }

        while (highResTasks.Count > 0)
        {
            Task finishedTask = await Task.WhenAny(highResTasks);
            highResTasks.Remove(finishedTask);
            completedTasks++;

            onProgress?.Invoke($"Generating High-Res Data: {completedTasks} / {totalTasks}");
        }

        Debug.Log("All High-Res Planet Data generated in background.");
    }
}