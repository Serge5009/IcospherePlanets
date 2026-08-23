using UnityEngine;
using TMPro;
using System.Text;
using System.Linq;

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
        if (leftPanel != null)
            leftPanel.anchoredPosition = new Vector2(hiddenX, leftPanel.anchoredPosition.y);

        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnPlanetSelected += DisplayPlanetInfo;
            SelectionManager.Instance.OnCellSelected += DisplayCellInfo;
        }
    }

    private void Update()
    {
        if (leftPanel == null) return;

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

        sb.AppendLine("<color=#55AAFF><b>--- PHYSICS & ORBIT ---</b></color>");
        sb.AppendLine($"<b>Archetype:</b> {body.archetype}");
        sb.AppendLine($"<b>Mass:</b> {body.massEarths:F3} Earths");
        sb.AppendLine($"<b>Radius:</b> {body.radiusKm:F0} km");
        sb.AppendLine($"<b>Gravity:</b> {body.surfaceGravity:F2} m/s²");
        sb.AppendLine($"<b>Distance:</b> {(body.orbit.semiMajorAxis / AstroMath.AU_TO_KM):F2} AU");
        sb.AppendLine($"<b>Axial Tilt:</b> {body.axialTilt:F1}°");
        sb.AppendLine($"<b>Day Length:</b> {(body.rotationPeriodSeconds / 3600f):F1} Hours");
        if (body.isTidallyLocked) sb.AppendLine($"<color=#FF5555><i>Tidally Locked</i></color>");

        if (body.bodyType != BodyType.Star && body.bodyType != BodyType.GasGiant)
        {
            sb.AppendLine("\n<color=#FFAA55><b>--- GEOLOGY ---</b></color>");
            string domRock = body.dominantBedrock != null ? body.dominantBedrock.bedrockName : "None";
            string secRock = body.secondaryBedrock != null ? body.secondaryBedrock.bedrockName : "None";
            string soil = body.surfaceSoil != null ? body.surfaceSoil.soilName : "None";

            sb.AppendLine($"<b>Dominant Rock:</b> {domRock}");
            sb.AppendLine($"<b>Secondary Rock:</b> {secRock}");
            sb.AppendLine($"<b>Global Soil:</b> {soil}");
            sb.AppendLine($"<b>Core Mass:</b> {(body.coreMassFraction * 100f):F1}%");
            sb.AppendLine($"<b>Core Temp:</b> {body.coreTemperatureKelvin:F0} K ({(body.isCoreActive ? "<color=#55FF55>Active</color>" : "<color=#FF5555>Dead</color>")})");
            sb.AppendLine($"<b>Mag-Shield:</b> {body.magnetosphereStrength:F2}");

            sb.AppendLine("\n<color=#55FFFF><b>--- HYDROLOGY ---</b></color>");
            string liquid = body.oceanLiquid != null ? body.oceanLiquid.liquidName : "None";
            sb.AppendLine($"<b>Ocean Liquid:</b> {liquid}");

            if (body.waterLevel > -5000f)
            {
                sb.AppendLine($"<b>Sea Level (Alt):</b> {body.waterLevel:F0} m");
                if (body.oceanLiquid != null)
                {
                    sb.AppendLine($"<b>Freezing Pt:</b> {body.oceanLiquid.baseFreezingPointKelvin:F1} K");
                    sb.AppendLine($"<b>Boiling Pt:</b> {body.oceanLiquid.baseBoilingPointKelvin:F1} K");
                }
            }
            else if (body.oceanLiquid != null)
            {
                if (body.surfacePressureAtm < 0.05) sb.AppendLine("<b>Sea Level:</b> <color=#AAAAAA>Vacuum (No Liquid)</color>");
                else sb.AppendLine("<b>Sea Level:</b> <color=#FF5555>Boiled Dry (Runaway Greenhouse)</color>");
            }
            else
            {
                sb.AppendLine("<b>Sea Level:</b> <color=#AAAAAA>0m (No Liquid Accreted)</color>");
            }

            sb.AppendLine("\n<color=#DDDDDD><b>--- ATMOSPHERE ---</b></color>");
            if (body.surfacePressureAtm > 0.001)
            {
                sb.AppendLine($"<b>Pressure:</b> {body.surfacePressureAtm:F3} atm");
                sb.AppendLine($"<b>Greenhouse Heat:</b> +{body.greenhouseHeatContribution:F1} K");
                sb.AppendLine($"<b>Toxicity:</b> {body.toxicityLevel:F2}");
                sb.AppendLine($"<b>Cloud Coverage:</b> {(body.atmosphereCloudCoverage * 100f):F0}%");

                sb.AppendLine("<b>Composition:</b>");
                double totalMass = body.atmosphericGasesKg.Values.Sum();
                foreach (var kvp in body.atmosphericGasesKg)
                {
                    GasTemplate gas = DataLibrary.Instance.GetGas(kvp.Key);
                    string gasName = gas != null ? gas.gasName : $"ID {kvp.Key}";
                    double percentage = (kvp.Value / totalMass) * 100.0;
                    double massMt = kvp.Value / 1e9;
                    sb.AppendLine($"  • {gasName}: {percentage:F1}% <size=80%>({massMt:F0} Mt)</size>");
                }
            }
            else
            {
                sb.AppendLine("<color=#FF5555>Vacuum (No Atmosphere)</color>");
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

        sb.AppendLine("<color=#FFAA55><b>--- TOPOLOGY ---</b></color>");
        BedrockTemplate rock = DataLibrary.Instance.GetBedrock(topo.bedrockId);
        string rockName = rock != null ? rock.bedrockName : "Unknown";

        sb.AppendLine($"<b>Bedrock:</b> {rockName}");
        sb.AppendLine($"<b>Absolute Alt:</b> {topo.altitude:F0} m");

        if (body.waterLevel > -5000f)
        {
            float elevation = topo.altitude - body.waterLevel;
            if (elevation > 0) sb.AppendLine($"<b>Elevation:</b> <color=#55FF55>+{elevation:F0} m</color> (Land)");
            else sb.AppendLine($"<b>Elevation:</b> <color=#5555FF>{elevation:F0} m</color> (Underwater)");
        }
        else
        {
            sb.AppendLine($"<b>Elevation:</b> {topo.altitude:F0} m (Dry Planet)");
        }

        sb.AppendLine($"<b>Base Insolation:</b> {(topo.baseInsolation * 100f):F0}%");
        sb.AppendLine($"<b>Wind Neighbor ID:</b> {(topo.windNeighborId == -1 ? "None" : topo.windNeighborId.ToString())}");

        sb.AppendLine("\n<color=#DDDD88><b>--- SURFACE COVER ---</b></color>");
        string soilName = body.surfaceSoil != null ? body.surfaceSoil.soilName : "None";
        sb.AppendLine($"<b>Soil Type:</b> {soilName}");

        float tAlt = (topo.altitude - 0f) / Mathf.Max(1f, 10000f);
        float baseThickness = 1.0f - tAlt;
        float curvePower = 1.0f + ((float)body.surfacePressureAtm * 2.0f);
        float thickness = Mathf.Pow(Mathf.Max(0f, baseThickness), curvePower);
        thickness -= topo.rainFactor * tAlt * 0.5f;

        float rainStrength = (clim.liquidDepth > 0 ? 1f : 0f);
        float globalSoil = body.soilBaseThickness * (1.0f + (float)body.surfacePressureAtm) * (float)(body.surfaceGravity / 9.8) * (1.0f + rainStrength);
        thickness = Mathf.Clamp01(thickness * Mathf.Clamp(globalSoil, 0.1f, 3.0f));

        sb.AppendLine($"<b>Soil Thickness:</b> {(thickness * 100f):F0}%");
        sb.AppendLine($"<b>Soil Wetness:</b> {(clim.moisture * 100f):F0}%");

        sb.AppendLine("\n<color=#55FFFF><b>--- CLIMATE ---</b></color>");
        float tempC = clim.localTemperature - 273.15f;
        string tempColor = tempC < 0 ? "#55AAFF" : (tempC > 40 ? "#FF5555" : "#55FF55");
        sb.AppendLine($"<b>Temperature:</b> <color={tempColor}>{clim.localTemperature:F1} K ({tempC:F1} °C)</color>");

        sb.AppendLine($"<b>Moisture:</b> {(clim.moisture * 100f):F1}%");
        sb.AppendLine($"<b>Rain Factor:</b> {(topo.rainFactor * 100f):F0}%");

        if (clim.liquidDepth > 0)
            sb.AppendLine($"<b>Liquid Depth:</b> <color=#5555FF>{(clim.liquidDepth * 1000f):F0} m</color>");

        if (clim.snowDepth > 0)
            sb.AppendLine($"<b>Snow Depth:</b> <color=#FFFFFF>{clim.snowDepth:F2} m</color>");

        if (clim.iceCover > 0)
            sb.AppendLine($"<b>Ice Coverage:</b> {(clim.iceCover * 100f):F0}%");

        sb.AppendLine("\n<color=#55FF55><b>--- ECOLOGY ---</b></color>");
        if (clim.biomass > 0)
            sb.AppendLine($"<b>Biomass:</b> {(clim.biomass * 100f):F1}%");
        else
            sb.AppendLine("<b>Biomass:</b> <color=#AAAAAA>0% (Sterile)</color>");

        sb.AppendLine("\n<color=#FFDD55><b>--- ECONOMY ---</b></color>");
        sb.AppendLine($"<b>Population:</b> {econ.population}");
        sb.AppendLine($"<b>Usable Land:</b> {(econ.infrastructureCap * 100f):F0}%");
        sb.AppendLine($"<b>Urbanization:</b> {(econ.developmentCap * 100f):F0}%");

        infoText.text = sb.ToString();
    }

    public void ClosePanel()
    {
        isPanelOpen = false;
    }
}