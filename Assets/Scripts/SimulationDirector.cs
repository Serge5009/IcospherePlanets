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

    private void HandleHourTick()
    {
    }

    private void HandleDayTick()
    {
        if (!simulateEconomy) return;
    }

    private void HandleMonthTick()
    {
        if (simulateClimate)
        {
            SimulateVolatileCycle();
        }

        if (simulateEconomy)
        {
        }
    }

    private void HandleYearTick()
    {
    }

    private void SimulateVolatileCycle()
    {
        if (SystemDataGenerator.Instance == null || SystemDataGenerator.Instance.allBodies.Count == 0) return;

        List<CelestialBody> bodiesNeedingEquilibrium = new List<CelestialBody>();

        foreach (var body in SystemDataGenerator.Instance.allBodies)
        {
            if (body.bodyType == BodyType.Star || body.bodyType == BodyType.GasGiant || body.bodyType == BodyType.IceGiant) continue;

            bool atmosphereChanged = false;
            bool oceanChanged = false;

            if (body.oceanLiquid != null && body.oceanLiquid.evaporatesInto != null)
            {
                byte vaporId = body.oceanLiquid.evaporatesInto.gasId;

                if (!body.atmosphericGasesKg.ContainsKey(vaporId)) body.atmosphericGasesKg[vaporId] = 0;

                int levelInt = Mathf.Clamp(Mathf.FloorToInt(body.waterLevel), 0, body.hypsometricCurveSqKm.Length - 1);
                double oceanAreaSqKm = body.hypsometricCurveSqKm[levelInt];
                double oceanFraction = oceanAreaSqKm / body.totalSurfaceAreaSqKm;

                float tempRange = body.oceanLiquid.baseBoilingPointKelvin - body.oceanLiquid.baseFreezingPointKelvin;
                float tempProgress = Mathf.Clamp01((body.globalMaxTemperature - body.oceanLiquid.baseFreezingPointKelvin) / tempRange);

                double baseCapacity = 5.15e18 * body.massEarths;
                double targetVaporMass = baseCapacity * 0.1 * oceanFraction * tempProgress;

                if (body.globalMaxTemperature > body.oceanLiquid.baseBoilingPointKelvin)
                {
                    targetVaporMass = double.MaxValue;
                }

                double currentVaporMass = body.atmosphericGasesKg[vaporId];
                double deltaVapor = targetVaporMass - currentVaporMass;

                double transferAmount = deltaVapor * 0.05;

                if (Math.Abs(transferAmount) > 1000000)
                {
                    if (transferAmount > 0)
                    {
                        double maxEvap = body.oceanVolumeKm3 * 1e9;
                        transferAmount = Math.Min(transferAmount, maxEvap);

                        body.oceanVolumeKm3 -= (transferAmount / 1e9);
                        body.atmosphericGasesKg[vaporId] += transferAmount;
                    }
                    else
                    {
                        transferAmount = Math.Max(transferAmount, -currentVaporMass);

                        body.oceanVolumeKm3 += (Math.Abs(transferAmount) / 1e9);
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
                double leakAmount = excessMass * 0.01;

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
                body.waterLevel = HypsometricMath.GetLevelFromVolume(body, body.oceanVolumeKm3);
            }

            if (atmosphereChanged)
            {
                SystemDataGenerator.Instance.UpdateAtmosphericProperties(body);
            }

            if (atmosphereChanged || oceanChanged)
            {
                bodiesNeedingEquilibrium.Add(body);
            }
        }

        if (bodiesNeedingEquilibrium.Count > 0)
        {
            ClimateResolver.Instance.TickClimateEquilibrium(bodiesNeedingEquilibrium);
        }
    }
}