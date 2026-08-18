using UnityEngine;

public static class Noise3D
{
    public static float Evaluate(Vector3 point, float scale, float offset)
    {
        point *= scale;
        point += new Vector3(offset, offset, offset);

        float noise = 0f;
        float frequency = 1f;
        float amplitude = 1f;
        float maxValue = 0f;

        for (int i = 0; i < 4; i++)
        {
            float xy = Mathf.PerlinNoise(point.x * frequency, point.y * frequency);
            float yz = Mathf.PerlinNoise(point.y * frequency, point.z * frequency);
            float xz = Mathf.PerlinNoise(point.x * frequency, point.z * frequency);

            float val = (xy + yz + xz) / 3f;

            noise += val * amplitude;
            maxValue += amplitude;

            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return noise / maxValue;
    }
}