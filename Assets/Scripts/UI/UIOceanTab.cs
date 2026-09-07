using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class UIOceanTab : UITabPanel
{
    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI statsText;
    public Slider waterLevelSlider;

    [Header("Controls")]
    public Button addWaterBtn;
    public Button removeWaterBtn;

    private bool isUpdatingSlider = false;

    private void Start()
    {
        if (waterLevelSlider != null)
            waterLevelSlider.onValueChanged.AddListener(OnSliderChanged);

        if (addWaterBtn != null)
            addWaterBtn.onClick.AddListener(() => ChangeVolume(10000000));

        if (removeWaterBtn != null)
            removeWaterBtn.onClick.AddListener(() => ChangeVolume(-10000000));
    }

    protected override void Refresh()
    {
        if (currentBody == null) return;

        if (titleText != null) titleText.text = $"{currentBody.name} - Hydrology";

        if (currentBody.hypsometricCurveSqKm == null || currentBody.hypsometricCurveSqKm.Length == 0)
        {
            if (statsText != null) statsText.text = "No topographic data available.";
            if (waterLevelSlider != null) waterLevelSlider.interactable = false;
            return;
        }

        if (waterLevelSlider != null)
        {
            waterLevelSlider.interactable = true;
            isUpdatingSlider = true;
            waterLevelSlider.minValue = 0;
            waterLevelSlider.maxValue = currentBody.hypsometricCurveSqKm.Length - 1;
            waterLevelSlider.value = currentBody.waterLevel > 0 ? currentBody.waterLevel : 0;
            isUpdatingSlider = false;
        }

        UpdateStatsText();
    }

    private void OnSliderChanged(float value)
    {
        if (isUpdatingSlider || currentBody == null) return;

        currentBody.waterLevel = value;
        currentBody.oceanVolumeKm3 = HypsometricMath.GetVolumeFromLevel(currentBody, value);

        if (currentBody.oceanLiquid == null && DataLibrary.Instance != null && DataLibrary.Instance.liquids.Length > 0)
        {
            currentBody.oceanLiquid = DataLibrary.Instance.liquids[0];
            currentBody.oceanColor = currentBody.oceanLiquid.shallowColor;
        }

        UpdateStatsText();
        UpdatePlanetVisuals();
    }

    private void ChangeVolume(double amountKm3)
    {
        if (currentBody == null || currentBody.hypsometricCurveSqKm == null) return;

        currentBody.oceanVolumeKm3 += amountKm3;
        if (currentBody.oceanVolumeKm3 < 0) currentBody.oceanVolumeKm3 = 0;

        currentBody.waterLevel = HypsometricMath.GetLevelFromVolume(currentBody, currentBody.oceanVolumeKm3);

        if (currentBody.oceanLiquid == null && currentBody.oceanVolumeKm3 > 0 && DataLibrary.Instance != null && DataLibrary.Instance.liquids.Length > 0)
        {
            currentBody.oceanLiquid = DataLibrary.Instance.liquids[0];
            currentBody.oceanColor = currentBody.oceanLiquid.shallowColor;
        }

        if (waterLevelSlider != null)
        {
            isUpdatingSlider = true;
            waterLevelSlider.value = currentBody.waterLevel;
            isUpdatingSlider = false;
        }

        UpdateStatsText();
        UpdatePlanetVisuals();
    }

    private void UpdateStatsText()
    {
        if (statsText == null) return;

        StringBuilder sb = new StringBuilder();

        string liquidName = currentBody.oceanLiquid != null ? currentBody.oceanLiquid.liquidName : "None";
        sb.AppendLine($"<b>Liquid Type:</b> {liquidName}");
        sb.AppendLine($"<b>Total Volume:</b> {currentBody.oceanVolumeKm3:N0} km³");

        if (currentBody.waterLevel > 0)
        {
            sb.AppendLine($"<b>Sea Level:</b> {currentBody.waterLevel:F0} m");
            sb.AppendLine($"<b>Max Depth:</b> {currentBody.waterLevel:F0} m");

            float maxAlt = currentBody.hypsometricCurveSqKm.Length - 1;
            float maxElev = maxAlt - currentBody.waterLevel;
            sb.AppendLine($"<b>Max Elevation:</b> {maxElev:F0} m");

            int levelInt = Mathf.Clamp(Mathf.FloorToInt(currentBody.waterLevel), 0, currentBody.hypsometricCurveSqKm.Length - 1);
            double oceanArea = currentBody.hypsometricCurveSqKm[levelInt];
            double oceanPct = (oceanArea / currentBody.totalSurfaceAreaSqKm) * 100.0;
            double landPct = 100.0 - oceanPct;

            sb.AppendLine($"<b>Surface Split:</b> {landPct:F1}% Land / {oceanPct:F1}% Ocean");
        }
        else
        {
            sb.AppendLine("<b>Sea Level:</b> Dry");
            sb.AppendLine("<b>Surface Split:</b> 100% Land / 0% Ocean");
        }

        statsText.text = sb.ToString();
    }

    private void UpdatePlanetVisuals()
    {
        if (currentBody.visualObject != null)
        {
            Planet p = currentBody.visualObject.GetComponent<Planet>();
            if (p != null) p.FastUpdateWaterVisuals();
        }
    }
}