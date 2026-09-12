using System;
using System.Collections.Generic;
using UnityEngine;

public class VolatileModule : ISimulationModule
{
    public void Initialize()
    {
    }

    public bool SimulateDay(CelestialBody body)
    {
        return false;
    }

    public bool SimulateMonth(CelestialBody body)
    {
        if (body.bodyType == BodyType.Star || body.bodyType == BodyType.GasGiant || body.bodyType == BodyType.IceGiant) return false;

        bool atmosphereChanged = false;
        bool oceanChanged = false;

        double totalMass = body.GetTotalAtmosphereMassKg();

        if (body.oceanLiquid == null && totalMass > 0)
        {
            foreach (var kvp in body.atmosphericGasesKg)
            {
                double concentration = kvp.Value / totalMass;
                if (concentration > 0.01)
                {
                    LiquidTemplate potentialLiquid = GetLiquidForVapor(kvp.Key);
                    if (potentialLiquid != null)
                    {
                        if (body.globalMaxTemperature > potentialLiquid.baseFreezingPointKelvin &&
                            body.globalMaxTemperature < potentialLiquid.baseBoilingPointKelvin)
                        {
                            body.oceanLiquid = potentialLiquid;
                            body.oceanColor = potentialLiquid.shallowColor;
                            oceanChanged = true;
                            Debug.Log($"[Volatiles] {body.name} formed a new {potentialLiquid.liquidName} ocean from atmospheric vapor!");
                            break;
                        }
                    }
                }
            }
        }
        else if (body.oceanLiquid != null && body.oceanVolumeKm3 <= 0)
        {
            byte vaporId = body.oceanLiquid.evaporatesInto != null ? body.oceanLiquid.evaporatesInto.gasId : (byte)255;
            double vaporMass = body.atmosphericGasesKg.ContainsKey(vaporId) ? body.atmosphericGasesKg[vaporId] : 0;
            double concentration = totalMass > 0 ? vaporMass / totalMass : 0;

            if (concentration < 0.001)
            {
                Debug.Log($"[Volatiles] {body.name}'s {body.oceanLiquid.liquidName} ocean has completely dried up.");
                body.oceanLiquid = null;
                body.waterLevel = -9999f;
                oceanChanged = true;
            }
        }

        if (body.oceanLiquid != null && body.oceanLiquid.evaporatesInto != null)
        {
            byte vaporId = body.oceanLiquid.evaporatesInto.gasId;
            if (!body.atmosphericGasesKg.ContainsKey(vaporId)) body.atmosphericGasesKg[vaporId] = 0;

            double currentVaporMass = body.atmosphericGasesKg[vaporId];
            double transferAmount = 0;

            if (body.globalMaxTemperature > body.oceanLiquid.baseBoilingPointKelvin && body.oceanVolumeKm3 > 0)
            {
                double maxEvapKg = body.oceanVolumeKm3 * body.oceanLiquid.densityKgPerKm3;
                transferAmount = maxEvapKg * 0.10;
                body.targetVaporMassKg = double.MaxValue;
            }
            else
            {
                int levelInt = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, body.waterLevel)), 0, body.hypsometricCurveSqKm.Length - 1);
                double oceanAreaSqKm = body.hypsometricCurveSqKm[levelInt];
                double oceanFraction = oceanAreaSqKm / body.totalSurfaceAreaSqKm;

                float tempRange = body.oceanLiquid.baseBoilingPointKelvin - body.oceanLiquid.baseFreezingPointKelvin;
                float tempProgress = Mathf.Clamp01((body.globalMaxTemperature - body.oceanLiquid.baseFreezingPointKelvin) / tempRange);

                double baseCapacity = 5.15e18 * body.massEarths;
                double targetVaporMass = baseCapacity * 0.02 * oceanFraction * tempProgress;

                double deltaVapor = targetVaporMass - currentVaporMass;
                transferAmount = deltaVapor * 0.05;

                body.targetVaporMassKg = targetVaporMass;
            }

            body.vaporExchangeRateKgPerMonth = transferAmount;

            if (Math.Abs(transferAmount) > 1000000)
            {
                double density = body.oceanLiquid.densityKgPerKm3;

                if (transferAmount > 0)
                {
                    double maxEvapKg = body.oceanVolumeKm3 * density;
                    transferAmount = Math.Min(transferAmount, maxEvapKg);

                    body.oceanVolumeKm3 -= (transferAmount / density);
                    body.atmosphericGasesKg[vaporId] += transferAmount;

                    body.vaporExchangeRateKgPerMonth = transferAmount;
                }
                else
                {
                    transferAmount = Math.Max(transferAmount, -currentVaporMass);

                    body.oceanVolumeKm3 += (Math.Abs(transferAmount) / density);
                    body.atmosphericGasesKg[vaporId] += transferAmount;

                    body.vaporExchangeRateKgPerMonth = transferAmount;
                }

                atmosphereChanged = true;
                oceanChanged = true;
            }
            else
            {
                body.vaporExchangeRateKgPerMonth = 0;
            }
        }
        else
        {
            body.targetVaporMassKg = 0;
            body.vaporExchangeRateKgPerMonth = 0;
        }

        List<byte> keys = new List<byte>(body.atmosphericGasesKg.Keys);
        foreach (byte gasId in keys)
        {
            GasTemplate gas = DataLibrary.Instance.GetGas(gasId);
            if (gas == null) continue;

            if (body.oceanLiquid != null && body.oceanLiquid.evaporatesInto != null && gasId == body.oceanLiquid.evaporatesInto.gasId) continue;

            if (!body.frozenVolatilesKg.ContainsKey(gasId)) body.frozenVolatilesKg[gasId] = 0;

            double currentGasMass = body.atmosphericGasesKg[gasId];
            double currentFrozenMass = body.frozenVolatilesKg[gasId];

            if (body.globalMinTemperature < gas.freezingPointKelvin && currentGasMass > 0)
            {
                double freezeAmount = currentGasMass * 0.10;
                if (freezeAmount > 1000000)
                {
                    body.atmosphericGasesKg[gasId] -= freezeAmount;
                    body.frozenVolatilesKg[gasId] += freezeAmount;
                    atmosphereChanged = true;
                }
            }
            else if (body.globalMaxTemperature > gas.freezingPointKelvin && currentFrozenMass > 0)
            {
                double thawAmount = currentFrozenMass * 0.10;
                if (thawAmount > 1000000)
                {
                    body.frozenVolatilesKg[gasId] -= thawAmount;
                    body.atmosphericGasesKg[gasId] += thawAmount;
                    atmosphereChanged = true;
                }
            }
        }

        totalMass = body.GetTotalAtmosphereMassKg();
        double magFactor = Math.Max(0.01, body.magnetosphereStrength);
        double gravFactor = Math.Max(0.01, body.surfaceGravity / 9.8);
        double capacityKg = (5.15e18 * body.massEarths) * magFactor * gravFactor;

        if (totalMass > capacityKg)
        {
            double excessMass = totalMass - capacityKg;
            double overPressureRatio = excessMass / capacityKg;
            double leakPercentage = Math.Clamp(overPressureRatio * 0.10, 0.01, 0.90);

            double leakAmount = excessMass * leakPercentage;

            if (leakAmount > 1000000)
            {
                List<byte> leakKeys = new List<byte>(body.atmosphericGasesKg.Keys);
                foreach (byte gasId in leakKeys)
                {
                    double fraction = body.atmosphericGasesKg[gasId] / totalMass;
                    body.atmosphericGasesKg[gasId] -= (leakAmount * fraction);
                }
                atmosphereChanged = true;
            }
        }

        if (oceanChanged)
        {
            if (body.oceanVolumeKm3 <= 0) body.waterLevel = -9999f;
            else body.waterLevel = HypsometricMath.GetLevelFromVolume(body, body.oceanVolumeKm3);
        }

        if (atmosphereChanged)
        {
            AtmosphereBuilder.UpdateAtmosphericProperties(body);
        }

        return atmosphereChanged || oceanChanged;
    }

    public bool SimulateYear(CelestialBody body)
    {
        return false;
    }

    public void ForceInstantVolatileEquilibrium(CelestialBody body)
    {
        if (body.bodyType == BodyType.Star || body.bodyType == BodyType.GasGiant || body.bodyType == BodyType.IceGiant) return;

        bool atmosphereChanged = false;
        bool oceanChanged = false;

        if (body.oceanLiquid != null && body.oceanLiquid.evaporatesInto != null)
        {
            byte vaporId = body.oceanLiquid.evaporatesInto.gasId;
            if (!body.atmosphericGasesKg.ContainsKey(vaporId)) body.atmosphericGasesKg[vaporId] = 0;

            int levelInt = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, body.waterLevel)), 0, body.hypsometricCurveSqKm.Length - 1);
            double oceanAreaSqKm = body.hypsometricCurveSqKm[levelInt];
            double oceanFraction = oceanAreaSqKm / body.totalSurfaceAreaSqKm;

            float tempRange = body.oceanLiquid.baseBoilingPointKelvin - body.oceanLiquid.baseFreezingPointKelvin;
            float tempProgress = Mathf.Clamp01((body.globalMaxTemperature - body.oceanLiquid.baseFreezingPointKelvin) / tempRange);

            double baseCapacity = 5.15e18 * body.massEarths;
            double targetVaporMass = baseCapacity * 0.02 * oceanFraction * tempProgress;

            if (body.globalMaxTemperature > body.oceanLiquid.baseBoilingPointKelvin)
            {
                targetVaporMass = double.MaxValue;
            }

            double currentVaporMass = body.atmosphericGasesKg[vaporId];
            double deltaVapor = targetVaporMass - currentVaporMass;

            if (Math.Abs(deltaVapor) > 1000000)
            {
                double density = body.oceanLiquid.densityKgPerKm3;

                if (deltaVapor > 0)
                {
                    double maxEvapKg = body.oceanVolumeKm3 * density;
                    double transferAmount = Math.Min(deltaVapor, maxEvapKg);

                    body.oceanVolumeKm3 -= (transferAmount / density);
                    body.atmosphericGasesKg[vaporId] += transferAmount;
                }
                else
                {
                    double transferAmount = Math.Max(deltaVapor, -currentVaporMass);

                    body.oceanVolumeKm3 += (Math.Abs(transferAmount) / density);
                    body.atmosphericGasesKg[vaporId] += transferAmount;
                }

                atmosphereChanged = true;
                oceanChanged = true;
            }
        }

        List<byte> keys = new List<byte>(body.atmosphericGasesKg.Keys);
        foreach (byte gasId in keys)
        {
            GasTemplate gas = DataLibrary.Instance.GetGas(gasId);
            if (gas == null) continue;

            if (body.oceanLiquid != null && body.oceanLiquid.evaporatesInto != null && gasId == body.oceanLiquid.evaporatesInto.gasId) continue;

            if (!body.frozenVolatilesKg.ContainsKey(gasId)) body.frozenVolatilesKg[gasId] = 0;

            double currentGasMass = body.atmosphericGasesKg[gasId];
            double currentFrozenMass = body.frozenVolatilesKg[gasId];

            if (body.globalMinTemperature < gas.freezingPointKelvin && currentGasMass > 0)
            {
                body.atmosphericGasesKg[gasId] -= currentGasMass;
                body.frozenVolatilesKg[gasId] += currentGasMass;
                atmosphereChanged = true;
            }
            else if (body.globalMaxTemperature > gas.freezingPointKelvin && currentFrozenMass > 0)
            {
                body.frozenVolatilesKg[gasId] -= currentFrozenMass;
                body.atmosphericGasesKg[gasId] += currentFrozenMass;
                atmosphereChanged = true;
            }
        }

        if (oceanChanged)
        {
            if (body.oceanVolumeKm3 <= 0) body.waterLevel = -9999f;
            else body.waterLevel = HypsometricMath.GetLevelFromVolume(body, body.oceanVolumeKm3);
        }

        if (atmosphereChanged)
        {
            AtmosphereBuilder.UpdateAtmosphericProperties(body);
        }
    }

    private LiquidTemplate GetLiquidForVapor(byte gasId)
    {
        if (DataLibrary.Instance == null || DataLibrary.Instance.liquids == null) return null;

        foreach (var liquid in DataLibrary.Instance.liquids)
        {
            if (liquid != null && liquid.evaporatesInto != null && liquid.evaporatesInto.gasId == gasId)
            {
                return liquid;
            }
        }
        return null;
    }
}