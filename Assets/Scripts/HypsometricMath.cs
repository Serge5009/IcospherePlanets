using UnityEngine;

public static class HypsometricMath
{
    public static double GetVolumeFromLevel(CelestialBody body, float targetLevelMeters)
    {
        if (body.hypsometricCurveSqKm == null || body.hypsometricCurveSqKm.Length == 0) return 0;
        if (targetLevelMeters <= 0) return 0;

        double totalVolumeKm3 = 0;
        int maxIndex = body.hypsometricCurveSqKm.Length - 1;

        int targetInt = Mathf.FloorToInt(targetLevelMeters);
        float fractionalMeter = targetLevelMeters - targetInt;

        int limit = Mathf.Min(targetInt, maxIndex);
        for (int i = 0; i < limit; i++)
        {
            totalVolumeKm3 += body.hypsometricCurveSqKm[i] * 0.001;
        }

        if (targetInt <= maxIndex)
        {
            totalVolumeKm3 += (body.hypsometricCurveSqKm[targetInt] * 0.001) * fractionalMeter;
        }

        if (targetLevelMeters > maxIndex)
        {
            float metersAboveMax = targetLevelMeters - maxIndex;
            totalVolumeKm3 += (body.totalSurfaceAreaSqKm * 0.001) * metersAboveMax;
        }

        return totalVolumeKm3;
    }

    public static float GetLevelFromVolume(CelestialBody body, double targetVolumeKm3)
    {
        if (body.hypsometricCurveSqKm == null || body.hypsometricCurveSqKm.Length == 0) return 0f;
        if (targetVolumeKm3 <= 0) return 0f;

        double currentVolume = 0;
        int maxIndex = body.hypsometricCurveSqKm.Length - 1;

        for (int i = 0; i < maxIndex; i++)
        {
            double sliceVolume = body.hypsometricCurveSqKm[i] * 0.001;

            if (currentVolume + sliceVolume >= targetVolumeKm3)
            {
                double remainingVolume = targetVolumeKm3 - currentVolume;
                float fraction = (float)(remainingVolume / sliceVolume);
                return i + fraction;
            }

            currentVolume += sliceVolume;
        }

        double leftoverVolume = targetVolumeKm3 - currentVolume;
        double globalSliceVolume = body.totalSurfaceAreaSqKm * 0.001;
        float metersAboveMax = (float)(leftoverVolume / globalSliceVolume);

        return maxIndex + metersAboveMax;
    }
}