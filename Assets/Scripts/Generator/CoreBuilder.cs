using UnityEngine;

public static class CoreBuilder
{
    public static void CalculateCoreAndMagnetosphere(CelestialBody body)
    {
        if (body.bodyType == BodyType.Star || body.bodyType == BodyType.Comet || body.bodyType == BodyType.Asteroid)
        {
            body.coreMassFraction = 0;
            body.coreTemperatureKelvin = 0;
            body.isCoreActive = false;
            body.magnetosphereStrength = 0;
            return;
        }

        float baseCoreMod = body.dominantBedrock != null ? body.dominantBedrock.coreMassModifier : 0.3f;
        body.coreMassFraction = UnityEngine.Random.Range(baseCoreMod * 0.8f, baseCoreMod * 1.2f);

        float baseHeat = 6000f * Mathf.Pow((float)body.massEarths, 0.5f);

        if (body.bodyType == BodyType.Moon && body.parent != null && body.parent.massEarths > 10)
        {
            double distanceKm = body.orbit.semiMajorAxis;
            float tidalHeat = (float)(2000000.0 * body.parent.massEarths / (distanceKm * distanceKm));
            baseHeat += tidalHeat;
        }

        body.coreTemperatureKelvin = Mathf.Clamp(baseHeat, 0f, 8000f);
        body.isCoreActive = body.coreTemperatureKelvin > 1800f;

        if (body.isCoreActive && body.rotationPeriodSeconds > 0)
        {
            float normalizedCore = (float)body.coreMassFraction / 0.32f;
            float spinFactor = Mathf.Sqrt(86400f / (float)body.rotationPeriodSeconds);
            spinFactor = Mathf.Clamp(spinFactor, 0f, 2.5f);
            float massFactor = Mathf.Pow((float)body.massEarths, 0.33f);
            body.magnetosphereStrength = normalizedCore * spinFactor * massFactor;
        }
        else
        {
            body.magnetosphereStrength = 0f;
        }
    }
}