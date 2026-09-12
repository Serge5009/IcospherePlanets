using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class AtmosphereBuilder
{
    public static void CalculateAtmosphere(CelestialBody body, double distanceAU)
    {
        body.atmosphericGasesKg.Clear();
        body.frozenVolatilesKg.Clear();
        body.surfacePressureAtm = 0;
        body.greenhouseHeatContribution = 0;
        body.toxicityLevel = 0;

        if (body.bodyType == BodyType.Star || body.bodyType == BodyType.Asteroid || body.bodyType == BodyType.Comet) return;

        double baseCapacity = 5.15e18 * body.massEarths;
        double magFactor = Math.Max(0.01, body.magnetosphereStrength);
        double gravFactor = Math.Max(0.01, body.surfaceGravity / 9.8);
        double atmosphericCapacityKg = baseCapacity * magFactor * gravFactor;

        bool isGiant = body.bodyType == BodyType.GasGiant || body.bodyType == BodyType.IceGiant;
        double accretedMass = isGiant ? atmosphericCapacityKg * UnityEngine.Random.Range(0.85f, 0.95f) : baseCapacity * UnityEngine.Random.Range(0.5f, 2.0f);
        double finalMass = Math.Min(accretedMass, atmosphericCapacityKg);

        double starSolarMasses = SystemDataGenerator.Instance.star.massKg / AstroMath.SOLAR_MASS_KG;
        double starLuminosity = AstroMath.CalculateLuminosity(starSolarMasses);
        float initialTemp = AstroMath.CalculateBlackbodyTemperature(starLuminosity, distanceAU, 0.3f);

        byte primarySolventGasId = 255;
        if (!isGiant && SystemDataGenerator.Instance.primordialLiquids != null)
        {
            foreach (var wl in SystemDataGenerator.Instance.primordialLiquids)
            {
                if (wl.template != null && initialTemp > wl.template.baseFreezingPointKelvin && initialTemp < wl.template.baseBoilingPointKelvin)
                {
                    if (wl.template.evaporatesInto != null) primarySolventGasId = wl.template.evaporatesInto.gasId;
                    break;
                }
            }
        }

        if (finalMass > 0 && SystemDataGenerator.Instance.primordialGases != null && SystemDataGenerator.Instance.primordialGases.Count > 0)
        {
            float totalWeight = 0;
            foreach (var wg in SystemDataGenerator.Instance.primordialGases) totalWeight += wg.weight;

            foreach (var wg in SystemDataGenerator.Instance.primordialGases)
            {
                if (wg.template == null) continue;

                if (!isGiant && wg.template.gasId == primarySolventGasId) continue;

                double retention = 1.0;
                if (!isGiant)
                {
                    retention = Math.Clamp((body.surfaceGravity * wg.template.molarMass) / initialTemp, 0.0, 1.0);
                }

                double gasMass = finalMass * (wg.weight / totalWeight) * retention;
                gasMass *= UnityEngine.Random.Range(0.8f, 1.2f);

                if (gasMass > 0)
                {
                    if (!isGiant && initialTemp < wg.template.freezingPointKelvin && wg.template.gasId != primarySolventGasId)
                    {
                        if (!body.frozenVolatilesKg.ContainsKey(wg.template.gasId)) body.frozenVolatilesKg[wg.template.gasId] = 0;
                        body.frozenVolatilesKg[wg.template.gasId] += gasMass;
                    }
                    else
                    {
                        if (!body.atmosphericGasesKg.ContainsKey(wg.template.gasId)) body.atmosphericGasesKg[wg.template.gasId] = 0;
                        body.atmosphericGasesKg[wg.template.gasId] += gasMass;
                    }
                }
            }

            if (body.bodyType == BodyType.RockyPlanet)
            {
                GasTemplate co2 = SystemDataGenerator.Instance.primordialGases.FirstOrDefault(g => g.template != null && g.template.isCarbonCycleGas).template;
                if (co2 != null && co2.gasId != primarySolventGasId)
                {
                    double baselineCO2 = baseCapacity * 0.005;
                    if (initialTemp < co2.freezingPointKelvin)
                    {
                        if (!body.frozenVolatilesKg.ContainsKey(co2.gasId)) body.frozenVolatilesKg[co2.gasId] = 0;
                        body.frozenVolatilesKg[co2.gasId] += baselineCO2;
                    }
                    else
                    {
                        if (!body.atmosphericGasesKg.ContainsKey(co2.gasId)) body.atmosphericGasesKg[co2.gasId] = 0;
                        body.atmosphericGasesKg[co2.gasId] += baselineCO2;
                    }
                }
            }

            ResolveInitialChemistry(body);
            UpdateAtmosphericProperties(body);
        }

        if (body.surfacePressureAtm < 0.05)
        {
            if (body.archetype == PlanetArchetype.ActiveTerrestrial)
                body.archetype = PlanetArchetype.DeadTerrestrial;
        }

        if (isGiant)
            body.atmosphereCloudScale = Mathf.Pow(20f, UnityEngine.Random.value);
        else
            body.atmosphereCloudScale = UnityEngine.Random.Range(2.5f, 10f);
    }

    public static void ResolveInitialChemistry(CelestialBody body)
    {
        if (DataLibrary.Instance.reactions == null || DataLibrary.Instance.reactions.Length == 0) return;

        bool reactionOccurred = true;
        int safetyCounter = 0;

        while (reactionOccurred && safetyCounter < 100)
        {
            reactionOccurred = false;
            safetyCounter++;

            foreach (var reaction in DataLibrary.Instance.reactions)
            {
                if (reaction == null || reaction.inputs.Length == 0) continue;

                double limitingMoles = double.MaxValue;
                bool hasAllInputs = true;

                foreach (var input in reaction.inputs)
                {
                    if (input.gas == null || input.moles <= 0) continue;

                    if (!body.atmosphericGasesKg.TryGetValue(input.gas.gasId, out double currentMass) || currentMass <= 0)
                    {
                        hasAllInputs = false;
                        break;
                    }

                    double currentMoles = currentMass / input.gas.molarMass;
                    double maxReactionMoles = currentMoles / input.moles;

                    if (maxReactionMoles < limitingMoles) limitingMoles = maxReactionMoles;
                }

                if (!hasAllInputs || limitingMoles <= 0) continue;

                double actualReactingMoles = limitingMoles * 0.99;

                if (actualReactingMoles < 1000) continue;

                foreach (var input in reaction.inputs)
                {
                    if (input.gas == null) continue;
                    double massToRemove = actualReactingMoles * input.moles * input.gas.molarMass;
                    body.atmosphericGasesKg[input.gas.gasId] -= massToRemove;
                    if (body.atmosphericGasesKg[input.gas.gasId] < 0) body.atmosphericGasesKg[input.gas.gasId] = 0;
                }

                foreach (var output in reaction.outputs)
                {
                    if (output.gas == null || output.moles <= 0) continue;
                    double massToAdd = actualReactingMoles * output.moles * output.gas.molarMass;

                    if (!body.atmosphericGasesKg.ContainsKey(output.gas.gasId)) body.atmosphericGasesKg[output.gas.gasId] = 0;
                    body.atmosphericGasesKg[output.gas.gasId] += massToAdd;
                }

                reactionOccurred = true;
            }
        }
    }

    public static void UpdateAtmosphericProperties(CelestialBody body)
    {
        double totalMassKg = 0;
        double vaporMassKg = 0;
        float totalGreenhouse = 0;
        float totalToxicity = 0;

        Color blendedSky = Color.black;
        Color blendedCloud = Color.black;

        foreach (var kvp in body.atmosphericGasesKg)
        {
            totalMassKg += kvp.Value;
            GasTemplate gas = DataLibrary.Instance.GetGas(kvp.Key);
            if (gas != null && gas.formsClouds) vaporMassKg += kvp.Value;
        }

        foreach (var kvp in body.atmosphericGasesKg)
        {
            GasTemplate gas = DataLibrary.Instance.GetGas(kvp.Key);
            if (gas != null)
            {
                float fractionOfTotal = (float)(kvp.Value / Math.Max(1.0, totalMassKg));
                totalGreenhouse += gas.greenhouseMultiplier * fractionOfTotal;
                totalToxicity += gas.toxicity * fractionOfTotal;

                if (!gas.formsClouds) blendedSky += gas.skyColor * fractionOfTotal;

                if (gas.formsClouds && vaporMassKg > 0)
                {
                    float fractionOfVapor = (float)(kvp.Value / vaporMassKg);
                    blendedCloud += gas.cloudColor * fractionOfVapor;
                }
            }
        }

        double surfaceAreaM2 = 4.0 * Math.PI * Math.Pow(body.radiusKm * 1000.0, 2);
        double pressurePascals = (totalMassKg * body.surfaceGravity) / surfaceAreaM2;
        body.surfacePressureAtm = pressurePascals / 101325.0;

        double vaporPressureAtm = ((vaporMassKg * body.surfaceGravity) / surfaceAreaM2) / 101325.0;

        float effectiveGreenhousePressure = Mathf.Log10(1f + (float)body.surfacePressureAtm * 9f);
        body.greenhouseHeatContribution = totalGreenhouse * effectiveGreenhousePressure * 40f;

        body.toxicityLevel = totalToxicity;

        body.atmosphereSkyColor = new Color(Mathf.Clamp01(blendedSky.r), Mathf.Clamp01(blendedSky.g), Mathf.Clamp01(blendedSky.b), Mathf.Clamp01(blendedSky.a));
        body.atmosphereCloudColor = new Color(Mathf.Clamp01(blendedCloud.r), Mathf.Clamp01(blendedCloud.g), Mathf.Clamp01(blendedCloud.b), Mathf.Clamp01(blendedCloud.a));

        body.atmosphereVisualScale = 1.01f + Mathf.Log10(1f + (float)body.surfacePressureAtm) * 0.05f;
        body.atmosphereVisualScale = Mathf.Clamp(body.atmosphereVisualScale, 1.01f, 1.15f);

        float pressureFloat = (float)body.surfacePressureAtm;
        body.atmosphereVisualOpacity = Mathf.Clamp01(Mathf.Sqrt(pressureFloat / 5.0f));

        if (vaporPressureAtm > 0)
        {
            float rawCoverage = Mathf.Sqrt((float)vaporPressureAtm / 0.1f);
            body.atmosphereCloudCoverage = Mathf.Clamp(rawCoverage, 0.1f, 0.8f);
        }
        else
        {
            body.atmosphereCloudCoverage = 0f;
        }
    }
}