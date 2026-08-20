using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

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

    public async void GenerateMeshes(List<CelestialBody> bodies)
    {
        if (bakedTemplates == null || bakedTemplates.Length == 0) return;

        foreach (var body in bodies)
        {
            int renderSubs = Mathf.Clamp(Mathf.Min(body.dataSubdivisions, systemViewMaxSubdivisions), 0, bakedTemplates.Length - 1);
            HexSphereTemplate template = bakedTemplates[renderSubs];

            IPlanetGenerator generator = generators[body.archetype];
            body.systemViewData = generator.Generate(template, body, body.noiseScale, body.noiseOffset, body.waterLevel);

            SystemDisplayManager.Instance.SpawnVisualHexSphere(body);
        }

        List<Task> highResTasks = new List<Task>();
        foreach (var body in bodies)
        {
            highResTasks.Add(Task.Run(() =>
            {
                int highResSubs = Mathf.Clamp(body.dataSubdivisions, 0, bakedTemplates.Length - 1);
                HexSphereTemplate template = bakedTemplates[highResSubs];

                IPlanetGenerator generator = generators[body.archetype];
                body.localViewData = generator.Generate(template, body, body.noiseScale, body.noiseOffset, body.waterLevel);

                body.isHighResReady = true;
            }));
        }

        await Task.WhenAll(highResTasks);
        Debug.Log("All High-Res Planet Data generated in background.");
    }
}