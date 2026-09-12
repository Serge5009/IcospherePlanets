using UnityEngine;

public static class ClimateStateBuilder
{
    public static PlanetClimateState CalculateGlobalState(PlanetSimulationData simData, double starLuminosity)
    {
        CelestialBody body = simData.body;

        float totalCells = simData.climates.Length;
        float surfaceAlbedo = 0.3f;
        float oceanFraction = 0f;
        float oceanCurve = 0f;

        if (totalCells > 0)
        {
            float totalAlbedo = (body.globalIceCoverage * 0.6f) + (body.globalOceanCoverage * 0.1f) + (body.globalSnowCoverage * 0.6f) + (body.globalBiomassCoverage * 0.15f) + (body.globalDesertCoverage * 0.25f);
            surfaceAlbedo = totalAlbedo;

            float effectiveOceanFraction = body.globalOceanCoverage + (body.globalIceCoverage * 0.15f);
            oceanFraction = Mathf.Sqrt(Mathf.Clamp01(effectiveOceanFraction * 2.0f));
            oceanCurve = oceanFraction;
        }

        float atmosThickness = Mathf.Clamp01((float)body.surfacePressureAtm / 2.0f);
        float globalAlbedo = Mathf.Lerp(surfaceAlbedo, 0.3f, atmosThickness);

        double distanceAU = body.orbit.semiMajorAxis / AstroMath.AU_TO_KM;
        float blackbody = AstroMath.CalculateBlackbodyTemperature(starLuminosity, distanceAU, globalAlbedo);

        float freezingPt = body.oceanLiquid != null ? body.oceanLiquid.baseFreezingPointKelvin : 273.15f;
        float boilingPt = body.oceanLiquid != null ? body.oceanLiquid.baseBoilingPointKelvin : 373.15f;

        float baseTemp = blackbody + body.greenhouseHeatContribution;

        float blendFactor = Mathf.Clamp01((float)body.surfacePressureAtm / 5.0f);
        float rawMaxTemp = baseTemp * 1.2f;
        float trueMaxTemp = Mathf.Lerp(rawMaxTemp, baseTemp, blendFactor);

        if (trueMaxTemp > boilingPt && body.waterLevel > -5000f)
        {
            HydrologyBuilder.TriggerRunawayGreenhouse(body);
        }

        float dynamicLapseRate = (float)(body.surfaceGravity / 9.8) * 6.5f;
        float tempFactor = Mathf.Clamp01(blackbody / 288f);
        float pressureFactor = Mathf.Clamp01((float)body.surfacePressureAtm / 0.05f);

        float relativeHumidity = 0f;
        if (body.oceanLiquid != null && body.oceanLiquid.evaporatesInto != null && body.targetVaporMassKg > 0)
        {
            byte vaporId = body.oceanLiquid.evaporatesInto.gasId;
            double currentVapor = body.atmosphericGasesKg.ContainsKey(vaporId) ? body.atmosphericGasesKg[vaporId] : 0;
            relativeHumidity = Mathf.Clamp01((float)(currentVapor / body.targetVaporMassKg));
        }

        float rainStrength = oceanCurve * relativeHumidity * tempFactor * pressureFactor;
        body.globalRainStrength = rainStrength;

        float globalSoil = body.soilBaseThickness * (1.0f + (float)body.surfacePressureAtm) * (float)(body.surfaceGravity / 9.8) * (1.0f + rainStrength);
        globalSoil = Mathf.Clamp(globalSoil, 0.1f, 3.0f);

        Color iceCol = body.oceanLiquid != null ? body.oceanLiquid.iceColor : Color.white;

        float absTilt = Mathf.Abs((float)body.axialTilt);
        float eqInsolation, poleInsolation;
        if (absTilt <= 54f)
        {
            float t = absTilt / 54f;
            eqInsolation = 1.0f - (t * 0.5f);
            poleInsolation = t * 0.5f;
        }
        else
        {
            float t = (absTilt - 54f) / 36f;
            eqInsolation = 0.5f - (t * 0.5f);
            poleInsolation = 0.5f + (t * 0.5f);
        }

        return new PlanetClimateState
        {
            blackbodyTemp = blackbody,
            greenhouseHeat = body.greenhouseHeatContribution,
            atmosphericPressure = (float)body.surfacePressureAtm,
            lapseRate = dynamicLapseRate,
            globalRainStrength = rainStrength,
            freezingPoint = freezingPt,
            boilingPoint = boilingPt,
            isTidallyLocked = body.isTidallyLocked,
            sunDirection = Vector3.right,
            waterLevel = body.waterLevel,

            minAltitude = 0f,
            maxAltitude = simData.maxAltitude,
            globalSoilThickness = globalSoil,
            soilDryColor = body.soilDryColor,
            soilWetColor = body.soilWetColor,
            dominantBedrockId = body.dominantBedrockId,
            secondaryBedrockId = body.secondaryBedrockId,
            dominantBedrockColor = body.dominantBedrockColor,
            secondaryBedrockColor = body.secondaryBedrockColor,
            iceColor = new Vector3(iceCol.r, iceCol.g, iceCol.b),

            eqInsolation = eqInsolation,
            poleInsolation = poleInsolation
        };
    }
}