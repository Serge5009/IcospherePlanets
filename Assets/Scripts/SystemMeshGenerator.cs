using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class SystemMeshGenerator : MonoBehaviour
{
    public static SystemMeshGenerator Instance { get; private set; }

    public int systemViewMaxSubdivisions = 4;

    [Header("Baked Templates")]
    [Tooltip("Assign the baked HexSphereTemplates here. Index 0 = Sub 0, Index 7 = Sub 7.")]
    public HexSphereTemplate[] bakedTemplates;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public async void GenerateMeshes(List<CelestialBody> bodies)
    {
        if (bakedTemplates == null || bakedTemplates.Length == 0)
        {
            Debug.LogError("SystemMeshGenerator: Baked Templates array is empty! Please assign them in the Inspector.");
            return;
        }

        foreach (var body in bodies)
        {
            int renderSubs = Mathf.Min(body.dataSubdivisions, systemViewMaxSubdivisions);

            renderSubs = Mathf.Clamp(renderSubs, 0, bakedTemplates.Length - 1);
            HexSphereTemplate template = bakedTemplates[renderSubs];

            body.systemViewData = PlanetGenerator.GeneratePlanetData(template, (float)body.radiusKm, body.bodyType, body.noiseScale, body.noiseOffset, body.waterLevel);

            SystemDisplayManager.Instance.SpawnVisualHexSphere(body);
        }

        List<Task> highResTasks = new List<Task>();
        foreach (var body in bodies)
        {
            highResTasks.Add(Task.Run(() =>
            {
                int highResSubs = Mathf.Clamp(body.dataSubdivisions, 0, bakedTemplates.Length - 1);
                HexSphereTemplate template = bakedTemplates[highResSubs];

                body.localViewData = PlanetGenerator.GeneratePlanetData(template, (float)body.radiusKm, body.bodyType, body.noiseScale, body.noiseOffset, body.waterLevel);
                body.isHighResReady = true;
            }));
        }

        await Task.WhenAll(highResTasks);
        Debug.Log("All High-Res Planet Data generated in background.");
    }
}