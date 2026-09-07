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
            waterLevelSlider.onValueChanged.AddListener(OnSliderChanged);

        if (addWaterBtn != null)
            addWaterBtn.onClick.AddListener(() => ChangeVolume(10000000));

        if (removeWaterBtn != null)
            removeWaterBtn.onClick.AddListener(() => ChangeVolume(-10000000));

        InitializeGraphTexture();
    }

    private void InitializeGraphTexture()
    {
        if (graphImage == null) return;

        graphTexture = new Texture2D(graphWidth, graphHeight, TextureFormat.RGBA32, false);
        graphTexture.filterMode = FilterMode.Bilinear;
        graphImage.texture = graphTexture;
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
        DrawHypsometricGraph();
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
        DrawHypsometricGraph();
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
        DrawHypsometricGraph();
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

        int[] curveYPositions = new int[graphWidth];
        for (int x = 0; x < graphWidth; x++)
        {
            double targetArea = ((double)x / (graphWidth - 1)) * totalArea;

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
            }
        }

        graphTexture.SetPixels(pixels);
        graphTexture.Apply();
    }
}