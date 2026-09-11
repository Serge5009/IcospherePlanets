using System;
using System.Collections.Generic;
using UnityEngine;

public class SimulationDirector : MonoBehaviour
{
    public static SimulationDirector Instance { get; private set; }

    [Header("Debug Toggles")]
    public bool simulateClimate = true;
    public bool simulateEconomy = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnHourTick += HandleHourTick;
            TimeManager.Instance.OnDayTick += HandleDayTick;
            TimeManager.Instance.OnMonthTick += HandleMonthTick;
            TimeManager.Instance.OnYearTick += HandleYearTick;
        }
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnHourTick -= HandleHourTick;
            TimeManager.Instance.OnDayTick -= HandleDayTick;
            TimeManager.Instance.OnMonthTick -= HandleMonthTick;
            TimeManager.Instance.OnYearTick -= HandleYearTick;
        }
    }

    private void HandleHourTick() { }

    private void HandleDayTick()
    {
        if (!simulateEconomy) return;
    }

    private void HandleMonthTick()
    {
        if (simulateClimate)
        {
            if (SystemDataGenerator.Instance == null || SystemDataGenerator.Instance.allBodies.Count == 0) return;

            List<CelestialBody> bodiesNeedingEquilibrium = new List<CelestialBody>();

            foreach (var body in SystemDataGenerator.Instance.allBodies)
            {
                bool chemChanged = SimulateChemistry(body);
                bool volChanged = ProcessVolatileExchange(body);

                if (chemChanged || volChanged)
                {
                    bodiesNeedingEquilibrium.Add(body);
                }
            }

            if (bodiesNeedingEquilibrium.Count > 0)
            {
                ClimateResolver.Instance.TickClimateEquilibrium(bodiesNeedingEquilibrium);
            }
        }

        if (simulateEconomy) { }
    }

    private void HandleYearTick() { }

    public bool SimulateChemistry(CelestialBody body)
    {
        if (body.bodyType == BodyType.Star) return false;
        if (DataLibrary.Instance.reactions == null || DataLibrary.Instance.reactions.Length == 0) return false;

        bool atmosphereChanged = false;
        double totalMass = body.GetTotalAtmosphereMassKg();
        if (totalMass <= 0) return false;

        foreach (var reaction in DataLibrary.Instance.reactions)
        {
            if (reaction == null || reaction.inputs.Length == 0) continue;

            double limitingMoles = double.MaxValue;
            double inputMassSum = 0;
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
                inputMassSum += currentMass;
            }

            if (!hasAllInputs || limitingMoles <= 0) continue;

            double concentration = inputMassSum / totalMass;
            double rateMultiplier = Math.Pow(concentration, 1.5);
            double tempMultiplier = Math.Max(0.1, body.globalMaxTemperature / 288.0);

            double actualReactingMoles = limitingMoles * reaction.baseReactionRate * rateMultiplier * tempMultiplier;

            if (actualReactingMoles < 1000) continue;

            foreach (var input in reaction.inputs)
            {
                if (input.gas == null) continue;
                double massToRemove = actualReactingMoles * input.moles * input.gas.molarMass;
                body.atmosphericGasesKg[input.gas.gasId] -= massToRemove;

                if (body.atmosphericGasesKg[input.gas.gasId] < 0)
                    body.atmosphericGasesKg[input.gas.gasId] = 0;
            }

            foreach (var output in reaction.outputs)
            {
                if (output.gas == null || output.moles <= 0) continue;
                double massToAdd = actualReactingMoles * output.moles * output.gas.molarMass;

                if (body.globalMinTemperature < output.gas.freezingPointKelvin)
                {
                    if (!body.frozenVolatilesKg.ContainsKey(output.gas.gasId)) body.frozenVolatilesKg[output.gas.gasId] = 0;
                    body.frozenVolatilesKg[output.gas.gasId] += massToAdd;
                }
                else
                {
                    if (!body.atmosphericGasesKg.ContainsKey(output.gas.gasId)) body.atmosphericGasesKg[output.gas.gasId] = 0;
                    body.atmosphericGasesKg[output.gas.gasId] += massToAdd;
                }
            }

            atmosphereChanged = true;
        }

        if (atmosphereChanged)
        {
            SystemDataGenerator.Instance.UpdateAtmosphericProperties(body);
        }

        return atmosphereChanged;
    }

    public bool ProcessVolatileExchange(CelestialBody body)
    {
        if (body.bodyType == BodyType.Star || body.bodyType == BodyType.GasGiant || body.bodyType == BodyType.IceGiant) return false;

        bool atmosphereChanged = false;
        bool oceanChanged = false;

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

        double totalMass = body.GetTotalAtmosphereMassKg();
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

        // --- 4. RESOLVE ---
        if (oceanChanged)
        {
            if (body.oceanVolumeKm3 <= 0)
            {
                body.waterLevel = -9999f;
            }
            else
            {
                body.waterLevel = HypsometricMath.GetLevelFromVolume(body, body.oceanVolumeKm3);
            }
        }

        if (atmosphereChanged)
        {
            SystemDataGenerator.Instance.UpdateAtmosphericProperties(body);
        }

        return atmosphereChanged || oceanChanged;
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
            SystemDataGenerator.Instance.UpdateAtmosphericProperties(body);
        }
    }
}