using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{
    public static EnvironmentManager Instance { get; private set; }

    [Header("Special Biomes")]
    public BiomeTemplate starBiome;
    public BiomeTemplate gasGiantBiome;
    public BiomeTemplate iceGiantBiome;
    public BiomeTemplate barrenRockBiome;

    [Header("Terran Biomes (Earth-like)")]
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

    public BiomeTemplate EvaluateBiome(float altitude, float waterLevel, BodyType bodyType)
    {
        switch (bodyType)
        {
            case BodyType.Star: return starBiome;
            case BodyType.GasGiant: return gasGiantBiome;
            case BodyType.IceGiant: return iceGiantBiome;
            case BodyType.Asteroid:
            case BodyType.Comet:
            case BodyType.Moon:
            case BodyType.DwarfPlanet:
                return barrenRockBiome;
        }

        if (altitude <= waterLevel)
        {
            return oceanBiome;
        }

        int pseudoRandomIndex = Mathf.Abs(Mathf.RoundToInt(altitude)) % landBiomes.Length;
        return landBiomes[pseudoRandomIndex];
    }
}