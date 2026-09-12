using System;
using System.Collections.Generic;
using UnityEngine;

public static class HydrologyBuilder
{
    public static bool TrySeedOceans(CelestialBody body, float maxAltitude)
    {
        if (body.oceanVolumeKm3 > 0 || body.bodyType == BodyType.GasGiant || body.bodyType == BodyType.IceGiant) return false;

        List<WeightedLiquid> allLiquids = new List<WeightedLiquid>(SystemDataGenerator.Instance.primordialLiquids);
        allLiquids.Sort((a, b) => a.template.baseFreezingPointKelvin.CompareTo(b.template.baseFreezingPointKelvin));

        foreach (var wl in allLiquids)
        {
            LiquidTemplate liquid = wl.template;
            if (liquid == null) continue;

            if (body.globalMaxTemperature > liquid.baseBoilingPointKelvin) continue;

            if (body.surfacePressureAtm < 0.05)
            {
                if (body.globalMaxTemperature > liquid.baseFreezingPointKelvin) continue;
            }

            if (body.globalMaxTemperature > liquid.baseFreezingPointKelvin ||
               (body.globalMaxTemperature <= liquid.baseFreezingPointKelvin && UnityEngine.Random.value > 0.5f))
            {
                ApplyLiquid(body, liquid, maxAltitude);
                return true;
            }
        }
        return false;
    }

    private static void ApplyLiquid(CelestialBody body, LiquidTemplate liquid, float maxAltitude)
    {
        body.oceanLiquid = liquid;

        double maxBasinVolume = HypsometricMath.GetVolumeFromLevel(body, maxAltitude);
        float fillPercentage = UnityEngine.Random.Range(0.05f, 0.80f);
        body.oceanVolumeKm3 = maxBasinVolume * fillPercentage;

        body.waterLevel = HypsometricMath.GetLevelFromVolume(body, body.oceanVolumeKm3);
        body.lastCalculatedWaterLevel = -9999f;

        body.oceanColor = liquid.shallowColor;

        if (liquid.evaporatesInto != null)
        {
            byte vaporId = liquid.evaporatesInto.gasId;
            if (!body.atmosphericGasesKg.ContainsKey(vaporId)) body.atmosphericGasesKg[vaporId] = 0;

            int levelInt = Mathf.Clamp(Mathf.FloorToInt(body.waterLevel), 0, body.hypsometricCurveSqKm.Length - 1);
            double oceanAreaSqKm = body.hypsometricCurveSqKm[levelInt];
            double oceanFraction = oceanAreaSqKm / body.totalSurfaceAreaSqKm;

            float tempRange = liquid.baseBoilingPointKelvin - liquid.baseFreezingPointKelvin;
            float tempProgress = Mathf.Clamp01((body.globalMaxTemperature - liquid.baseFreezingPointKelvin) / tempRange);

            double baseCapacity = 5.15e18 * body.massEarths;
            double targetVapor = baseCapacity * 0.02 * oceanFraction * tempProgress;

            double currentVapor = body.atmosphericGasesKg[vaporId];
            double neededVapor = Math.Max(0, targetVapor - currentVapor);

            body.atmosphericGasesKg[vaporId] += neededVapor;

            double volumeDelta = neededVapor / liquid.densityKgPerKm3;
            body.oceanVolumeKm3 -= volumeDelta;
            body.oceanVolumeKm3 = Math.Max(0, body.oceanVolumeKm3);
            body.waterLevel = HypsometricMath.GetLevelFromVolume(body, body.oceanVolumeKm3);

            if (body.frozenVolatilesKg.ContainsKey(vaporId))
            {
                body.atmosphericGasesKg[vaporId] += body.frozenVolatilesKg[vaporId];
                body.frozenVolatilesKg.Remove(vaporId);
            }

            AtmosphereBuilder.UpdateAtmosphericProperties(body);
        }

        Debug.Log($"[Seeding] {body.name} seeded with {liquid.liquidName}. Coverage: {fillPercentage * 100:F0}%. Sea Level: {body.waterLevel:F0}m");
    }

    public static void TriggerRunawayGreenhouse(CelestialBody body)
    {
        if (body.oceanLiquid != null && body.oceanLiquid.evaporatesInto != null)
        {
            byte vaporId = body.oceanLiquid.evaporatesInto.gasId;
            if (!body.atmosphericGasesKg.ContainsKey(vaporId)) body.atmosphericGasesKg[vaporId] = 0;

            double oceanMassKg = body.oceanVolumeKm3 * body.oceanLiquid.densityKgPerKm3;
            body.atmosphericGasesKg[vaporId] += oceanMassKg;

            AtmosphereBuilder.UpdateAtmosphericProperties(body);
        }

        body.oceanVolumeKm3 = 0;
        body.waterLevel = -9999f;
        body.lastCalculatedWaterLevel = -9999f;
        Debug.Log($"[Climate] {body.name} suffered a Runaway Greenhouse effect! Oceans boiled dry.");
    }
}