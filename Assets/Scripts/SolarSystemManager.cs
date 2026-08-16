using UnityEngine;

public class SolarSystemManager : MonoBehaviour
{
    [System.Serializable]
    public struct PlanetConfig
    {
        public string name;
        public float radiusKm;
    }

    [Header("System Settings")]
    public float targetCellRadiusKm = 50f;

    [Range(3, 8)]
    public int maxSubdivisions = 7;

    public float unityVisualRadius = 1000f;

    [Header("Planets to Generate")]
    public PlanetConfig[] planetsToGenerate;

    [Header("Dependencies")]
    public Material defaultPlanetMaterial;

    private void Start()
    {
        GenerateSystem();
    }

    private void GenerateSystem()
    {
        float targetHexArea = 2.598076f * (targetCellRadiusKm * targetCellRadiusKm);

        for (int i = 0; i < planetsToGenerate.Length; i++)
        {
            PlanetConfig config = planetsToGenerate[i];

            float planetSurfaceArea = 4f * Mathf.PI * (config.radiusKm * config.radiusKm);
            float targetCellCount = planetSurfaceArea / targetHexArea;

            float nFloat = Mathf.Log(Mathf.Max(1, targetCellCount - 2) / 10f, 4f);
            int subdivisions = Mathf.RoundToInt(nFloat);
            subdivisions = Mathf.Clamp(subdivisions, 0, maxSubdivisions);

            int actualCellCount = 10 * (int)Mathf.Pow(4, subdivisions) + 2;
            float actualHexArea = planetSurfaceArea / actualCellCount;
            float actualCellRadius = Mathf.Sqrt(actualHexArea / 2.598076f);

            Debug.Log($"--- Generating {config.name} ---");
            Debug.Log($"Target Cell Radius: {targetCellRadiusKm}km | Actual Cell Radius: {actualCellRadius:F2}km");
            Debug.Log($"Subdivisions: {subdivisions} | Total Cells: {actualCellCount}");

            SpawnPlanet(config, subdivisions, i);
        }
    }

    private void SpawnPlanet(PlanetConfig config, int subdivisions, int index)
    {
        GameObject planetObj = new GameObject(config.name);

        planetObj.transform.position = new Vector3(index * unityVisualRadius * 2.5f, 0, 0);

        Planet planet = planetObj.AddComponent<Planet>();
        planet.planetName = config.name;
        planet.radiusKm = config.radiusKm;
        planet.unityRadius = unityVisualRadius;

        PlanetGenerator generator = planetObj.AddComponent<PlanetGenerator>();

        if (defaultPlanetMaterial != null)
        {
            planetObj.GetComponent<MeshRenderer>().sharedMaterial = defaultPlanetMaterial;
        }

        generator.Generate(planet, subdivisions);
    }
}