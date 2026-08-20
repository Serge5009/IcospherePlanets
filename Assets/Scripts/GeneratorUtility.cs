using UnityEngine;

public static class GeneratorUtility
{
    public static float FBM(Vector3 pos, float scale, float offset, int octaves)
    {
        float total = 0f;
        float frequency = scale;
        float amplitude = 1f;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            total += Noise3D.Evaluate(pos, frequency, offset) * amplitude;
            maxValue += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return total / maxValue;
    }

    public static float RidgedNoise(Vector3 pos, float scale, float offset, int octaves)
    {
        float total = 0f;
        float frequency = scale;
        float amplitude = 1f;
        float weight = 1f;

        for (int i = 0; i < octaves; i++)
        {
            float n = Noise3D.Evaluate(pos, frequency, offset);
            n = 1f - Mathf.Abs(n);
            n *= n;
            n *= weight;
            weight = Mathf.Clamp01(n * 2f);

            total += n * amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return total;
    }

    public static void InitializeDataArrays(PlanetMeshData data, int cellCount)
    {
        data.topologies = new CellTopology[cellCount];
        data.climates = new CellClimate[cellCount];
        data.economies = new CellEconomy[cellCount];
        data.visualDataArray = new CellVisualData[cellCount];
    }
}