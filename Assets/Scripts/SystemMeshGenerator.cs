using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class SystemMeshGenerator : MonoBehaviour
{
    public static SystemMeshGenerator Instance { get; private set; }

    public int systemViewMaxSubdivisions = 4;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public async void GenerateMeshes(List<CelestialBody> bodies)
    {
        foreach (var body in bodies)
        {
            int renderSubs = Mathf.Min(body.dataSubdivisions, systemViewMaxSubdivisions);
            body.systemViewData = PlanetGenerator.GenerateData((float)body.radiusKm, renderSubs, body.bodyType, body.noiseScale, body.noiseOffset, body.waterLevel);

            SystemDisplayManager.Instance.SpawnVisualHexSphere(body);
        }

        List<Task> highResTasks = new List<Task>();
        foreach (var body in bodies)
        {
            highResTasks.Add(Task.Run(() =>
            {
                body.localViewData = PlanetGenerator.GenerateData((float)body.radiusKm, body.dataSubdivisions, body.bodyType, body.noiseScale, body.noiseOffset, body.waterLevel);
                body.isHighResReady = true;
            }));
        }

        await Task.WhenAll(highResTasks);
        Debug.Log("All High-Res Meshes generated in background.");
    }
}