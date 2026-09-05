using UnityEngine;
using TMPro;
using System.Text;
using System.Linq;

public class UIInfoTab : UITabPanel
{
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI infoText;

    protected override void Refresh()
    {
        if (currentBody == null) return;

        if (currentCellId == -1)
        {
            DisplayPlanetInfo();
        }
        else
        {
            DisplayCellInfo();
        }
    }

    private void DisplayPlanetInfo()
    {
        titleText.text = currentBody.name.ToUpper();
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<color=#55AAFF><b>--- PHYSICS & ORBIT ---</b></color>");
        sb.AppendLine($"<b>Archetype:</b> {currentBody.archetype}");
        sb.AppendLine($"<b>Mass:</b> {currentBody.massEarths:F3} Earths");
        sb.AppendLine($"<b>Radius:</b> {currentBody.radiusKm:F0} km");
        sb.AppendLine($"<b>Gravity:</b> {currentBody.surfaceGravity:F2} m/s²");
        sb.AppendLine($"<b>Distance:</b> {(currentBody.orbit.semiMajorAxis / AstroMath.AU_TO_KM):F2} AU");
        sb.AppendLine($"<b>Axial Tilt:</b> {currentBody.axialTilt:F1}°");
        sb.AppendLine($"<b>Day Length:</b> {(currentBody.rotationPeriodSeconds / 3600f):F1} Hours");
        if (currentBody.isTidallyLocked) sb.AppendLine($"<color=#FF5555><i>Tidally Locked</i></color>");

        if (currentBody.bodyType != BodyType.Star && currentBody.bodyType != BodyType.GasGiant)
        {
            sb.AppendLine("\n<color=#FFAA55><b>--- GEOLOGY ---</b></color>");
            string domRock = currentBody.dominantBedrock != null ? currentBody.dominantBedrock.bedrockName : "None";
            string secRock = currentBody.secondaryBedrock != null ? currentBody.secondaryBedrock.bedrockName : "None";
            string soil = currentBody.surfaceSoil != null ? currentBody.surfaceSoil.soilName : "None";

            sb.AppendLine($"<b>Dominant Rock:</b> {domRock}");
            sb.AppendLine($"<b>Secondary Rock:</b> {secRock}");
            sb.AppendLine($"<b>Global Soil:</b> {soil}");
            sb.AppendLine($"<b>Core Mass:</b> {(currentBody.coreMassFraction * 100f):F1}%");
            sb.AppendLine($"<b>Core Temp:</b> {currentBody.coreTemperatureKelvin:F0} K ({(currentBody.isCoreActive ? "<color=#55FF55>Active</color>" : "<color=#FF5555>Dead</color>")})");
            sb.AppendLine($"<b>Mag-Shield:</b> {currentBody.magnetosphereStrength:F2}");

            sb.AppendLine("\n<color=#55FFFF><b>--- CLIMATE & HYDROLOGY ---</b></color>");

            float minC = currentBody.globalMinTemperature - 273.15f;
            float maxC = currentBody.globalMaxTemperature - 273.15f;
            sb.AppendLine($"<b>Min Temp:</b> {currentBody.globalMinTemperature:F1} K ({minC:F1} °C)");
            sb.AppendLine($"<b>Max Temp:</b> {currentBody.globalMaxTemperature:F1} K ({maxC:F1} °C)");

            string liquid = currentBody.oceanLiquid != null ? currentBody.oceanLiquid.liquidName : "None";
            sb.AppendLine($"<b>Ocean Liquid:</b> {liquid}");

            if (currentBody.waterLevel > -5000f)
            {
                sb.AppendLine($"<b>Sea Level (Alt):</b> {currentBody.waterLevel:F0} m");
                if (currentBody.oceanLiquid != null)
                {
                    sb.AppendLine($"<b>Freezing Pt:</b> {currentBody.oceanLiquid.baseFreezingPointKelvin:F1} K");
                    sb.AppendLine($"<b>Boiling Pt:</b> {currentBody.oceanLiquid.baseBoilingPointKelvin:F1} K");

                    if (currentBody.globalMaxTemperature < currentBody.oceanLiquid.baseFreezingPointKelvin)
                    {
                        sb.AppendLine("<color=#55AAFF><i>(Global Snowball)</i></color>");
                    }
                }
            }
            else if (currentBody.oceanLiquid != null)
            {
                float avgTemp = 0;
                if (currentBody.localViewData != null)
                {
                    foreach (var clim in currentBody.localViewData.climates) avgTemp += clim.localTemperature;
                    avgTemp /= currentBody.localViewData.climates.Length;
                }

                if (currentBody.surfacePressureAtm < 0.05)
                {
                    if (avgTemp < currentBody.oceanLiquid.baseFreezingPointKelvin) sb.AppendLine("<b>Sea Level:</b> <color=#AAAAAA>0m (Frozen Dry / Sublimated)</color>");
                    else sb.AppendLine("<b>Sea Level:</b> <color=#AAAAAA>0m (Boiled Dry into Vacuum)</color>");
                }
                else
                {
                    sb.AppendLine("<b>Sea Level:</b> <color=#FF5555>0m (Runaway Greenhouse)</color>");
                }
            }
            else
            {
                sb.AppendLine("<b>Sea Level:</b> <color=#AAAAAA>0m (No Liquid Accreted)</color>");
            }

            sb.AppendLine("\n<color=#DDDDDD><b>--- ATMOSPHERE ---</b></color>");
            if (currentBody.surfacePressureAtm > 0.001)
            {
                sb.AppendLine($"<b>Pressure:</b> {currentBody.surfacePressureAtm:F3} atm");
                sb.AppendLine($"<b>Greenhouse Heat:</b> +{currentBody.greenhouseHeatContribution:F1} K");
                sb.AppendLine($"<b>Toxicity:</b> {currentBody.toxicityLevel:F2}");
                sb.AppendLine($"<b>Cloud Coverage:</b> {(currentBody.atmosphereCloudCoverage * 100f):F0}%");

                sb.AppendLine("<b>Composition:</b>");
                double totalMass = currentBody.atmosphericGasesKg.Values.Sum();
                foreach (var kvp in currentBody.atmosphericGasesKg)
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

    private void DisplayCellInfo()
    {
        titleText.text = $"{currentBody.name} - Cell {currentCellId}";

        CellTopology topo = currentBody.localViewData.topologies[currentCellId];
        CellClimate clim = currentBody.localViewData.climates[currentCellId];
        CellEconomy econ = currentBody.localViewData.economies[currentCellId];

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<color=#FFAA55><b>--- TOPOLOGY ---</b></color>");
        BedrockTemplate rock = DataLibrary.Instance.GetBedrock(topo.bedrockId);
        string rockName = rock != null ? rock.bedrockName : "Unknown";

        sb.AppendLine($"<b>Bedrock:</b> {rockName}");
        sb.AppendLine($"<b>Absolute Alt:</b> {topo.altitude:F0} m");

        if (currentBody.waterLevel > -5000f)
        {
            float elevation = topo.altitude - currentBody.waterLevel;
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
        string soilName = currentBody.surfaceSoil != null ? currentBody.surfaceSoil.soilName : "None";
        sb.AppendLine($"<b>Soil Type:</b> {soilName}");

        float tAlt = (topo.altitude - 0f) / Mathf.Max(1f, 10000f);
        float baseThickness = 1.0f - tAlt;
        float curvePower = 1.0f + ((float)currentBody.surfacePressureAtm * 2.0f);
        float thickness = Mathf.Pow(Mathf.Max(0f, baseThickness), curvePower);
        thickness -= topo.rainFactor * tAlt * 0.5f;

        float rainStrength = (clim.liquidDepth > 0 ? 1f : 0f);
        float globalSoil = currentBody.soilBaseThickness * (1.0f + (float)currentBody.surfacePressureAtm) * (float)(currentBody.surfaceGravity / 9.8) * (1.0f + rainStrength);
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
}