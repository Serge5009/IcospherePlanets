using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{
    public static EnvironmentManager Instance { get; private set; }

    [Header("Biomes")]
    public BiomeTemplate oceanBiome;
    public BiomeTemplate[] landBiomes;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public BiomeTemplate EvaluateBiome(float altitude, float waterLevel)
    {
        if (altitude <= waterLevel)
        {
            return oceanBiome;
        }

        int pseudoRandomIndex = Mathf.Abs(Mathf.RoundToInt(altitude)) % landBiomes.Length;
        return landBiomes[pseudoRandomIndex];
    }
}