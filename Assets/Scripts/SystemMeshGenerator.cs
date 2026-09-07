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
    }

    public async Task GenerateMeshesAsync(List<CelestialBody> bodies, Action<string> onProgress = null)
    {
        if (bakedTemplates == null || bakedTemplates.Length == 0) return;

        foreach (var body in bodies)
        {
            int highResSubs = Mathf.Clamp(body.dataSubdivisions, 0, bakedTemplates.Length - 1);
            body.geometryTemplate = bakedTemplates[highResSubs];

            if ((body.bodyType == BodyType.Asteroid || body.bodyType == BodyType.Comet) && body.geometryTemplate.variants.Length > 1)
            {
                body.variantIndex = UnityEngine.Random.Range(1, body.geometryTemplate.variants.Length);
            }
            else
            {
                body.variantIndex = 0;
            }
        }

        for (int i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            int lowResSubs = Mathf.Clamp(Mathf.Min(body.dataSubdivisions, systemViewMaxSubdivisions), 0, bakedTemplates.Length - 1);
            HexSphereTemplate lowTemplate = bakedTemplates[lowResSubs];

            int lowVariantIdx = (lowResSubs == body.dataSubdivisions) ? body.variantIndex : 0;

            Mesh mesh = lowTemplate.variants[lowVariantIdx].bakedMesh;
            Vector3[] centers = lowTemplate.variants[lowVariantIdx].cellCenters;

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
                    int lowResSubs = Mathf.Clamp(Mathf.Min(body.dataSubdivisions, systemViewMaxSubdivisions), 0, bakedTemplates.Length - 1);
                    int lowVariantIdx = (lowResSubs == body.dataSubdivisions) ? body.variantIndex : 0;
                    Vector3[] lowCenters = bakedTemplates[lowResSubs].variants[lowVariantIdx].cellCenters;

                    Mesh highMesh = body.geometryTemplate.variants[body.variantIndex].bakedMesh;
                    Vector3[] highCenters = body.geometryTemplate.variants[body.variantIndex].cellCenters;

                    IPlanetGenerator generator = generators[body.archetype];
                    body.localViewData = generator.Generate(highMesh, highCenters, body, body.noiseScale, body.noiseOffset, body.waterLevel);

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