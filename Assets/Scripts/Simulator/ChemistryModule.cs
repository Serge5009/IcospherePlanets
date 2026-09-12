using System;

public class ChemistryModule : ISimulationModule
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
            AtmosphereBuilder.UpdateAtmosphericProperties(body);
        }

        return atmosphereChanged;
    }

    public bool SimulateYear(CelestialBody body)
    {
        return false;
    }
}