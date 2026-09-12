using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System;

public class UIOceanTab : UITabPanel, IPointerUpHandler
{
    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI statsText;
    public Slider waterLevelSlider;

    [Header("Controls")]
    public Button addWaterBtn;
    public Button removeWaterBtn;

    [Header("Atmosphere Graph")]
    public RawImage atmosphereImage;
    public int atmosGraphHeight = 64;
    private Texture2D atmosphereTexture;

    [Header("Moisture Info")]
    public TextMeshProUGUI moistureInfoText;

    [Header("Hypsometric Graph")]
    public RawImage graphImage;
    public int graphWidth = 512;
    public int graphHeight = 256;
    public float shallowDepthMeters = 500f;

    [Header("Graph Colors")]
    public Color graphBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    public Color landColor = new Color(0.4f, 0.3f, 0.2f, 1f);
    public Color curveLineColor = Color.white;

    private Texture2D graphTexture;
    private bool isUpdatingSlider = false;

    private void Start()
    {
        if (waterLevelSlider != null)
        {
            waterLevelSlider.onValueChanged.AddListener(OnSliderChanged);

            EventTrigger trigger = waterLevelSlider.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = waterLevelSlider.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerUp;
            entry.callback.AddListener((data) => { OnPointerUp((PointerEventData)data); });
            trigger.triggers.Add(entry);
        }

        if (addWaterBtn != null)
            addWaterBtn.onClick.AddListener(() => ChangeVolume(10000000));

        if (removeWaterBtn != null)
            removeWaterBtn.onClick.AddListener(() => ChangeVolume(-10000000));

        InitializeTextures();
    }

    private void InitializeTextures()
    {
        if (graphImage != null)
        {
            graphTexture = new Texture2D(graphWidth, graphHeight, TextureFormat.RGBA32, false);
            graphTexture.filterMode = FilterMode.Bilinear;
            graphImage.texture = graphTexture;
        }

        if (atmosphereImage != null)
        {
            atmosphereTexture = new Texture2D(graphWidth, atmosGraphHeight, TextureFormat.RGBA32, false);
            atmosphereTexture.filterMode = FilterMode.Bilinear;
            atmosphereImage.texture = atmosphereTexture;
        }
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

            float maxAlt = currentBody.hypsometricCurveSqKm.Length - 1;
            waterLevelSlider.minValue = 0;
            waterLevelSlider.maxValue = maxAlt * 1.1f;
            waterLevelSlider.value = currentBody.waterLevel > 0 ? currentBody.waterLevel : 0;

            isUpdatingSlider = false;
        }

        UpdateStatsText();
        UpdateMoistureInfo();
        DrawAtmosphereGraph();
        DrawHypsometricGraph();
    }

    private void OnSliderChanged(float value)
    {
        if (isUpdatingSlider || currentBody == null) return;

        currentBody.waterLevel = value;
        currentBody.oceanVolumeKm3 = HypsometricMath.GetVolumeFromLevel(currentBody, value);

        if (currentBody.oceanLiquid == null) AssignBestLiquid();

        UpdateStatsText();
        DrawHypsometricGraph();

        if (currentBody.visualObject != null)
        {
            Planet p = currentBody.visualObject.GetComponent<Planet>();
            if (p != null) p.FastUpdateWaterVisuals();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (currentBody == null) return;
        ClimateResolver.Instance.TickClimateEquilibrium(new List<CelestialBody> { currentBody });
    }

    private void ChangeVolume(double amountKm3)
    {
        if (currentBody == null || currentBody.hypsometricCurveSqKm == null) return;

        currentBody.oceanVolumeKm3 += amountKm3;
        if (currentBody.oceanVolumeKm3 < 0) currentBody.oceanVolumeKm3 = 0;

        currentBody.waterLevel = HypsometricMath.GetLevelFromVolume(currentBody, currentBody.oceanVolumeKm3);

        if (currentBody.oceanLiquid == null && currentBody.oceanVolumeKm3 > 0) AssignBestLiquid();

        if (waterLevelSlider != null)
        {
            isUpdatingSlider = true;
            waterLevelSlider.value = currentBody.waterLevel;
            isUpdatingSlider = false;
        }

        UpdateStatsText();
        DrawHypsometricGraph();

        ClimateResolver.Instance.TickClimateEquilibrium(new List<CelestialBody> { currentBody });
    }

    private void AssignBestLiquid()
    {
        if (DataLibrary.Instance == null || DataLibrary.Instance.liquids == null) return;

        foreach (var liquid in DataLibrary.Instance.liquids)
        {
            if (liquid == null) continue;

            if (currentBody.globalMaxTemperature > liquid.baseFreezingPointKelvin &&
                currentBody.globalMaxTemperature < liquid.baseBoilingPointKelvin)
            {
                currentBody.oceanLiquid = liquid;
                currentBody.oceanColor = liquid.shallowColor;
                return;
            }
        }

        currentBody.oceanLiquid = DataLibrary.Instance.liquids[0];
        currentBody.oceanColor = currentBody.oceanLiquid.shallowColor;
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

    private void UpdateMoistureInfo()
    {
        if (moistureInfoText == null || currentBody == null) return;

        StringBuilder sb = new StringBuilder();

        if (currentBody.oceanLiquid != null && currentBody.oceanLiquid.evaporatesInto != null)
        {
            byte vaporId = currentBody.oceanLiquid.evaporatesInto.gasId;
            double currentVapor = currentBody.atmosphericGasesKg.ContainsKey(vaporId) ? currentBody.atmosphericGasesKg[vaporId] : 0;

            string targetStr = currentBody.targetVaporMassKg >= double.MaxValue * 0.9 ? "<color=#FF5555>Infinite (Runaway)</color>" : $"{currentBody.targetVaporMassKg / 1e9:N0} Mt";

            sb.AppendLine($"<b>Current Vapor:</b> {currentVapor / 1e9:N0} Mt");
            sb.AppendLine($"<b>Target Vapor:</b> {targetStr}");

            string exchangeStr = "Equilibrium";
            if (currentBody.vaporExchangeRateKgPerMonth > 0) exchangeStr = $"<color=#FF5555>+{(currentBody.vaporExchangeRateKgPerMonth / 1e9):N0} Mt/mo (Evaporating)</color>";
            else if (currentBody.vaporExchangeRateKgPerMonth < 0) exchangeStr = $"<color=#55AAFF>-{(Math.Abs(currentBody.vaporExchangeRateKgPerMonth) / 1e9):N0} Mt/mo (Condensing)</color>";

            sb.AppendLine($"<b>Exchange Rate:</b> {exchangeStr}");
        }
        else
        {
            sb.AppendLine("<b>Current Vapor:</b> 0 Mt");
            sb.AppendLine("<b>Target Vapor:</b> 0 Mt");
            sb.AppendLine("<b>Exchange Rate:</b> None");
        }

        sb.AppendLine($"<b>Global Rain Factor:</b> {(currentBody.globalRainStrength * 100f):F1}%");

        moistureInfoText.text = sb.ToString();
    }

    private void DrawAtmosphereGraph()
    {
        if (atmosphereTexture == null || currentBody == null || DataLibrary.Instance == null) return;

        Color[] pixels = new Color[graphWidth * atmosGraphHeight];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = graphBackgroundColor;

        double totalMass = currentBody.GetTotalAtmosphereMassKg();
        if (totalMass <= 0)
        {
            atmosphereTexture.SetPixels(pixels);
            atmosphereTexture.Apply();
            return;
        }

        var sortedGases = currentBody.atmosphericGasesKg
            .Select(kvp => new { Gas = DataLibrary.Instance.GetGas(kvp.Key), Mass = kvp.Value })
            .Where(g => g.Gas != null && g.Mass > 0)
            .OrderByDescending(g => g.Gas.molarMass)
            .ToList();

        int currentY = 0;

        foreach (var g in sortedGases)
        {
            float fraction = (float)(g.Mass / totalMass);
            int layerHeight = Mathf.RoundToInt(fraction * atmosGraphHeight);

            if (layerHeight == 0) continue;

            int endY = Mathf.Min(currentY + layerHeight, atmosGraphHeight);

            for (int y = currentY; y < endY; y++)
            {
                float verticalGradient = (float)(y - currentY) / layerHeight;

                for (int x = 0; x < graphWidth; x++)
                {
                    Color pixelColor;

                    if (g.Gas.formsClouds)
                    {
                        float noise = Mathf.PerlinNoise(x * 0.05f, y * 0.1f);
                        pixelColor = Color.Lerp(g.Gas.skyColor, g.Gas.cloudColor, noise);
                    }
                    else
                    {
                        Color fadeColor = new Color(g.Gas.skyColor.r * 0.5f, g.Gas.skyColor.g * 0.5f, g.Gas.skyColor.b * 0.5f, 1f);
                        pixelColor = Color.Lerp(g.Gas.skyColor, fadeColor, verticalGradient);
                    }

                    pixels[y * graphWidth + x] = pixelColor;
                }
            }
            currentY = endY;
        }

        atmosphereTexture.SetPixels(pixels);
        atmosphereTexture.Apply();
    }

    private void DrawHypsometricGraph()
    {
        if (graphTexture == null || currentBody == null || currentBody.hypsometricCurveSqKm == null) return;

        Color[] pixels = new Color[graphWidth * graphHeight];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = graphBackgroundColor;

        int maxAlt = currentBody.hypsometricCurveSqKm.Length - 1;
        float graphMaxAlt = maxAlt * 1.1f;
        double totalArea = currentBody.totalSurfaceAreaSqKm;

        Color shallowCol = currentBody.oceanLiquid != null ? currentBody.oceanLiquid.shallowColor : new Color(0.2f, 0.6f, 1f);
        Color deepCol = currentBody.oceanLiquid != null ? currentBody.oceanLiquid.deepColor : new Color(0f, 0.1f, 0.4f);
        Color iceCol = currentBody.oceanLiquid != null ? currentBody.oceanLiquid.iceColor : Color.white;

        int[] curveYPositions = new int[graphWidth];
        for (int x = 0; x < graphWidth; x++)
        {
            double targetArea = (1.0 - ((double)x / (graphWidth - 1))) * totalArea;

            int altAtX = 0;
            for (int i = 0; i <= maxAlt; i++)
            {
                if (currentBody.hypsometricCurveSqKm[i] >= targetArea)
                {
                    altAtX = i;
                    break;
                }
                if (i == maxAlt) altAtX = maxAlt;
            }

            curveYPositions[x] = Mathf.FloorToInt(((float)altAtX / graphMaxAlt) * graphHeight);
        }

        int waterLevelY = Mathf.FloorToInt((currentBody.waterLevel / graphMaxAlt) * graphHeight);

        int iceStartX = graphWidth - Mathf.FloorToInt(currentBody.globalIceCoverage * graphWidth);

        for (int x = 0; x < graphWidth; x++)
        {
            int curveY = curveYPositions[x];

            for (int y = 0; y < graphHeight; y++)
            {
                int pixelIndex = y * graphWidth + x;

                if (y < curveY)
                {
                    pixels[pixelIndex] = landColor;
                }
                else if (y == curveY)
                {
                    pixels[pixelIndex] = curveLineColor;
                }
                else if (y <= waterLevelY)
                {
                    float currentAltMeters = ((float)y / graphHeight) * graphMaxAlt;
                    float depthMeters = currentBody.waterLevel - currentAltMeters;

                    float depthT = Mathf.Clamp01(depthMeters / shallowDepthMeters);
                    Color waterCol = Color.Lerp(shallowCol, deepCol, depthT);

                    pixels[pixelIndex] = Color.Lerp(graphBackgroundColor, waterCol, 0.8f);
                }

                if (x >= iceStartX && y >= waterLevelY && y <= waterLevelY + 2)
                {
                    pixels[pixelIndex] = iceCol;
                }
            }
        }

        graphTexture.SetPixels(pixels);
        graphTexture.Apply();
    }
}