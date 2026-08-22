using UnityEngine;
using TMPro;
using System.Text;

public class UIManager : MonoBehaviour
{
    [Header("Panel Animation")]
    public RectTransform leftPanel;
    public float hiddenX = -400f;
    public float visibleX = 0f;
    public float slideSpeed = 10f;
    private bool isPanelOpen = false;

    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI infoText;

    private void Start()
    {
        leftPanel.anchoredPosition = new Vector2(hiddenX, leftPanel.anchoredPosition.y);

        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnPlanetSelected += DisplayPlanetInfo;
            SelectionManager.Instance.OnCellSelected += DisplayCellInfo;
        }
    }

    private void Update()
    {
        float targetX = isPanelOpen ? visibleX : hiddenX;
        float currentX = leftPanel.anchoredPosition.x;

        if (Mathf.Abs(currentX - targetX) > 0.1f)
        {
            float newX = Mathf.Lerp(currentX, targetX, Time.deltaTime * slideSpeed);
            leftPanel.anchoredPosition = new Vector2(newX, leftPanel.anchoredPosition.y);
        }
    }

    private void DisplayPlanetInfo(CelestialBody body)
    {
        isPanelOpen = true;
        titleText.text = body.name.ToUpper();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<b>Archetype:</b> {body.archetype}");
        sb.AppendLine($"<b>Mass:</b> {body.massEarths:F3} Earths");
        sb.AppendLine($"<b>Radius:</b> {body.radiusKm:F0} km");
        sb.AppendLine($"<b>Gravity:</b> {body.surfaceGravity:F2} m/s²");
        sb.AppendLine($"<b>Distance:</b> {(body.orbit.semiMajorAxis / AstroMath.AU_TO_KM):F2} AU");

        if (body.bodyType != BodyType.Star && body.bodyType != BodyType.GasGiant)
        {
            sb.AppendLine("\n<b>--- GEOLOGY ---</b>");
            string rock = body.dominantBedrock != null ? body.dominantBedrock.bedrockName : "None";
            sb.AppendLine($"<b>Bedrock:</b> {rock}");
            sb.AppendLine($"<b>Core Temp:</b> {body.coreTemperatureKelvin:F0} K");
            sb.AppendLine($"<b>Mag-Shield:</b> {body.magnetosphereStrength:F2}");

            sb.AppendLine("\n<b>--- CLIMATE ---</b>");
            if (body.surfacePressureAtm > 0)
            {
                sb.AppendLine($"<b>Pressure:</b> {body.surfacePressureAtm:F3} atm");
                sb.AppendLine($"<b>Greenhouse:</b> +{body.greenhouseHeatContribution:F1} K");
            }
            else
            {
                sb.AppendLine("<b>Atmosphere:</b> Vacuum");
            }
        }

        infoText.text = sb.ToString();
    }

    private void DisplayCellInfo(CelestialBody body, int cellId)
    {
        isPanelOpen = true;
        titleText.text = $"{body.name} - Cell {cellId}";

        CellTopology topo = body.localViewData.topologies[cellId];
        CellClimate clim = body.localViewData.climates[cellId];
        CellEconomy econ = body.localViewData.economies[cellId];

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<b>--- TOPOLOGY ---</b>");
        sb.AppendLine($"<b>Altitude:</b> {topo.altitude:F0} m");
        sb.AppendLine($"<b>Insolation:</b> {(topo.baseInsolation * 100f):F0}%");
        sb.AppendLine($"<b>Rain Factor:</b> {(topo.rainFactor * 100f):F0}%");

        sb.AppendLine("\n<b>--- CLIMATE ---</b>");
        sb.AppendLine($"<b>Temperature:</b> {clim.localTemperature:F1} K ({(clim.localTemperature - 273.15f):F1} °C)");
        sb.AppendLine($"<b>Moisture:</b> {clim.moisture:F3}");

        if (clim.liquidDepth > 0) sb.AppendLine($"<b>Ocean Depth:</b> {clim.liquidDepth:F2}");
        if (clim.snowDepth > 0) sb.AppendLine($"<b>Snow/Ice Depth:</b> {clim.snowDepth:F2}");
        if (clim.biomass > 0) sb.AppendLine($"<b>Biomass:</b> {(clim.biomass * 100f):F1}%");

        sb.AppendLine("\n<b>--- ECONOMY ---</b>");
        sb.AppendLine($"<b>Population:</b> {econ.population}");
        sb.AppendLine($"<b>Usable Land:</b> {(econ.infrastructureCap * 100f):F0}%");

        infoText.text = sb.ToString();
    }

    public void ClosePanel()
    {
        isPanelOpen = false;
    }
}