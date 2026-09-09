using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class UIAtmosphereTab : UITabPanel
{
    [Header("Global Stats")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI pressureText;
    public TextMeshProUGUI capacityText;
    public TextMeshProUGUI greenhouseText;

    [Header("Global Controls")]
    public Button addPressureBtn;
    public Button removePressureBtn;

    [Header("Gas List")]
    public Transform gasListContainer;
    public GameObject gasRowPrefab;

    private Dictionary<byte, GameObject> gasRows = new Dictionary<byte, GameObject>();

    private void Start()
    {
        if (addPressureBtn != null)
            addPressureBtn.onClick.AddListener(() => ChangeGlobalPressure(1.1));

        if (removePressureBtn != null)
            removePressureBtn.onClick.AddListener(() => ChangeGlobalPressure(0.9));
    }

    public override void OnOpen()
    {
        base.OnOpen();
        BuildGasList();
    }

    protected override void Refresh()
    {
        if (currentBody == null || !gameObject.activeSelf) return;

        if (titleText != null) titleText.text = $"{currentBody.name} - Atmosphere";

        UpdateGlobalStats();
        UpdateGasRows();
    }

    private void UpdateGlobalStats()
    {
        if (currentBody == null) return;

        double baseCapacity = 5.15e18 * currentBody.massEarths;
        double magFactor = System.Math.Max(0.01, currentBody.magnetosphereStrength);
        double gravFactor = System.Math.Max(0.01, currentBody.surfaceGravity / 9.8);
        double capacityKg = baseCapacity * magFactor * gravFactor;

        double surfaceAreaM2 = 4.0 * System.Math.PI * System.Math.Pow(currentBody.radiusKm * 1000.0, 2);
        double capacityAtm = ((capacityKg * currentBody.surfaceGravity) / surfaceAreaM2) / 101325.0;

        if (pressureText != null) pressureText.text = $"<b>Current Pressure:</b> {currentBody.surfacePressureAtm:F3} atm";
        if (capacityText != null) capacityText.text = $"<b>Max Capacity:</b> {capacityAtm:F3} atm <size=70%>(Gravity * Magnetosphere)</size>";
        if (greenhouseText != null) greenhouseText.text = $"<b>Greenhouse Heat:</b> +{currentBody.greenhouseHeatContribution:F1} K";
    }

    private void BuildGasList()
    {
        if (gasListContainer == null || gasRowPrefab == null || DataLibrary.Instance == null) return;

        foreach (Transform child in gasListContainer) Destroy(child.gameObject);
        gasRows.Clear();

        foreach (var gas in DataLibrary.Instance.gases)
        {
            if (gas == null) continue;

            GameObject row = Instantiate(gasRowPrefab, gasListContainer);
            gasRows.Add(gas.gasId, row);

            Button[] btns = row.GetComponentsInChildren<Button>();
            if (btns.Length >= 4)
            {
                byte id = gas.gasId;
                btns[0].onClick.AddListener(() => ChangeGasMass(id, 1000000000000000));
                btns[1].onClick.AddListener(() => ChangeGasMass(id, -1000000000000000));
                btns[2].onClick.AddListener(() => ChangeGasPercentage(id, 0.05));
                btns[3].onClick.AddListener(() => ChangeGasPercentage(id, -0.05));
            }
        }
    }

    private void UpdateGasRows()
    {
        if (currentBody == null || DataLibrary.Instance == null) return;

        double totalMass = currentBody.GetTotalAtmosphereMassKg();

        var sortedGases = DataLibrary.Instance.gases
            .OrderByDescending(g => currentBody.atmosphericGasesKg.ContainsKey(g.gasId) ? currentBody.atmosphericGasesKg[g.gasId] : 0)
            .ToList();

        for (int i = 0; i < sortedGases.Count; i++)
        {
            GasTemplate gas = sortedGases[i];
            if (gas == null || !gasRows.ContainsKey(gas.gasId)) continue;

            GameObject row = gasRows[gas.gasId];

            row.transform.SetSiblingIndex(i);

            double massKg = currentBody.atmosphericGasesKg.ContainsKey(gas.gasId) ? currentBody.atmosphericGasesKg[gas.gasId] : 0;
            double volatileKg = currentBody.frozenVolatilesKg.ContainsKey(gas.gasId) ? currentBody.frozenVolatilesKg[gas.gasId] : 0;

            double percentage = totalMass > 0 ? (massKg / totalMass) * 100.0 : 0;
            double massMt = massKg / 1e9;
            double volMt = volatileKg / 1e9;

            TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 3)
            {
                texts[0].text = gas.gasName;
                texts[1].text = $"{percentage:F1}%";

                string massText = $"{massMt:N0} Mt";
                if (volMt > 0) massText += $" <color=#55AAFF>(+{volMt:N0} Mt Frozen)</color>";
                texts[2].text = massText;
            }
        }
    }

    private void ChangeGlobalPressure(double multiplier)
    {
        if (currentBody == null) return;

        List<byte> keys = new List<byte>(currentBody.atmosphericGasesKg.Keys);
        foreach (byte key in keys)
        {
            currentBody.atmosphericGasesKg[key] *= multiplier;
        }

        ApplyChanges();
    }

    private void ChangeGasMass(byte gasId, double amountKg)
    {
        if (currentBody == null) return;

        if (!currentBody.atmosphericGasesKg.ContainsKey(gasId))
            currentBody.atmosphericGasesKg[gasId] = 0;

        currentBody.atmosphericGasesKg[gasId] += amountKg;
        if (currentBody.atmosphericGasesKg[gasId] < 0) currentBody.atmosphericGasesKg[gasId] = 0;

        ApplyChanges();
    }

    private void ChangeGasPercentage(byte gasId, double pctChange)
    {
        if (currentBody == null) return;

        double totalMass = currentBody.GetTotalAtmosphereMassKg();
        if (totalMass <= 0) return;

        double currentMass = currentBody.atmosphericGasesKg.ContainsKey(gasId) ? currentBody.atmosphericGasesKg[gasId] : 0;
        double currentPct = currentMass / totalMass;

        double newPct = System.Math.Clamp(currentPct + pctChange, 0.0, 1.0);

        double otherMass = totalMass - currentMass;

        if (newPct >= 0.999)
        {
            currentBody.atmosphericGasesKg[gasId] = otherMass * 1000;
        }
        else
        {
            double newMass = (newPct * otherMass) / (1.0 - newPct);
            currentBody.atmosphericGasesKg[gasId] = newMass;
        }

        ApplyChanges();
    }

    private void ApplyChanges()
    {
        SystemDataGenerator.Instance.UpdateAtmosphericProperties(currentBody);
        Refresh();

        ClimateResolver.Instance.TickClimateEquilibrium(new List<CelestialBody> { currentBody });
    }
}