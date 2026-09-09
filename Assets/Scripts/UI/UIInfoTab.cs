using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class UIInfoTab : UITabPanel
{
    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public Button backToPlanetBtn;
    public Transform contentContainer;

    [Header("Prefabs")]
    public GameObject sectionPrefab;
    public GameObject itemPrefab;

    [Header("Zebra Striping Colors")]
    public Color rowColorDark = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color rowColorLight = new Color(0.4f, 0.4f, 0.4f, 1f);

    private List<UIInfoSection> activeSections = new List<UIInfoSection>();
    private List<UIInfoSection> sectionPool = new List<UIInfoSection>();

    private List<UIInfoItem> activeItems = new List<UIInfoItem>();
    private List<UIInfoItem> itemPool = new List<UIInfoItem>();

    private void Start()
    {
        if (backToPlanetBtn != null)
        {
            backToPlanetBtn.onClick.AddListener(OnBackToPlanetClicked);
        }
    }

    private void OnBackToPlanetClicked()
    {
        if (currentBody != null)
        {
            UIManager.Instance.SwitchTab(this);
            ReceiveData(currentBody, -1);

            if (currentBody.visualObject != null)
            {
                Planet p = currentBody.visualObject.GetComponent<Planet>();
                if (p != null) p.SetHoveredCell(-1);
            }
        }
    }

    protected override void Refresh()
    {
        if (currentBody == null) return;

        ClearUI();

        if (backToPlanetBtn != null)
        {
            backToPlanetBtn.gameObject.SetActive(currentCellId != -1);
        }

        if (currentCellId == -1)
        {
            DisplayPlanetInfo();
        }
        else
        {
            DisplayCellInfo();
        }

        foreach (var sec in activeSections)
        {
            sec.ApplyZebraStriping(rowColorDark, rowColorLight);

            if (sec.contentContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(sec.contentContainer.GetComponent<RectTransform>());
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(sec.GetComponent<RectTransform>());
        }

        if (contentContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer.GetComponent<RectTransform>());
        }
    }

    private void DisplayPlanetInfo()
    {
        titleText.text = currentBody.name.ToUpper();
        bool isGasGiant = currentBody.bodyType == BodyType.GasGiant || currentBody.bodyType == BodyType.IceGiant || currentBody.bodyType == BodyType.Star;

        UIInfoSection physSec = GetSection("PHYSICS & ORBIT");
        CreateItem(physSec, "Archetype", currentBody.archetype.ToString());
        CreateItem(physSec, "Mass", $"{currentBody.massEarths:F3} Earths <size=80%>({currentBody.massKg:E2} kg)</size>");

        string radiusLabel = currentBody.variantIndex > 0 ? "Mean Radius" : "Radius";
        double radiusMi = currentBody.radiusKm * 0.621371;
        CreateItem(physSec, radiusLabel, $"{currentBody.radiusKm:N0} km <size=80%>({radiusMi:N0} mi)</size>");

        double areaMi2 = currentBody.totalSurfaceAreaSqKm * 0.386102;
        CreateItem(physSec, "Total Area", $"{currentBody.totalSurfaceAreaSqKm:N0} km² <size=80%>({areaMi2:N0} mi²)</size>");

        CreateItem(physSec, "Gravity", $"{currentBody.surfaceGravity:F2} m/s²");

        if (currentBody.parent != null)
        {
            double distAU = currentBody.orbit.semiMajorAxis / AstroMath.AU_TO_KM;
            double distMkm = currentBody.orbit.semiMajorAxis / 1000000.0;
            CreateItem(physSec, "Distance", $"{distAU:F2} AU <size=80%>({distMkm:F1}M km)</size>");

            double periodSec = KeplerMath.GetOrbitalPeriod(currentBody.orbit);
            double periodDays = periodSec / 86400.0;
            string periodStr = periodDays > 365 ? $"{(periodDays / 365.25):F2} Years" : $"{periodDays:F1} Days";
            CreateItem(physSec, "Orbital Period", periodStr);
        }

        CreateItem(physSec, "Axial Tilt", $"{currentBody.axialTilt:F1}°");

        if (currentBody.isTidallyLocked) CreateItem(physSec, "Day Length", "<color=#FF5555>Tidally Locked</color>");
        else CreateItem(physSec, "Day Length", $"{(currentBody.rotationPeriodSeconds / 3600f):F1} Hours");

        int moonCount = currentBody.orbitingBodies.Count(b => b.bodyType == BodyType.Moon);
        CreateItem(physSec, "Moons", moonCount.ToString());

        if (!isGasGiant)
        {
            UIInfoSection geoSec = GetSection("GEOLOGY");
            string domRock = currentBody.dominantBedrock != null ? currentBody.dominantBedrock.bedrockName : "None";
            string secRock = currentBody.secondaryBedrock != null ? currentBody.secondaryBedrock.bedrockName : "None";
            string soil = currentBody.surfaceSoil != null ? currentBody.surfaceSoil.soilName : "None";

            CreateItem(geoSec, "Dominant Rock", domRock);
            CreateItem(geoSec, "Secondary Rock", secRock);
            CreateItem(geoSec, "Global Soil", soil);
            CreateItem(geoSec, "Core Mass", $"{(currentBody.coreMassFraction * 100f):F1}%");

            string coreState = currentBody.isCoreActive ? "<color=#55FF55>Active</color>" : "<color=#FF5555>Dead</color>";
            CreateItem(geoSec, "Core Temp", $"{currentBody.coreTemperatureKelvin:F0} K ({coreState})");
            CreateItem(geoSec, "Mag-Shield", $"{currentBody.magnetosphereStrength:F2}");
        }

        if (!isGasGiant)
        {
            UIInfoSection climSec = GetSection("CLIMATE & HYDROLOGY");

            float minC = currentBody.globalMinTemperature - 273.15f;
            float maxC = currentBody.globalMaxTemperature - 273.15f;
            CreateItem(climSec, "Min Temp", $"{currentBody.globalMinTemperature:F1} K <size=80%>({minC:F1} °C)</size>");
            CreateItem(climSec, "Max Temp", $"{currentBody.globalMaxTemperature:F1} K <size=80%>({maxC:F1} °C)</size>");

            string liquid = currentBody.oceanLiquid != null ? currentBody.oceanLiquid.liquidName : "None";
            CreateItem(climSec, "Ocean Liquid", liquid);

            if (currentBody.waterLevel > -5000f)
            {
                CreateItem(climSec, "Sea Level (Alt)", $"{currentBody.waterLevel:F0} m");
                if (currentBody.oceanLiquid != null)
                {
                    if (currentBody.globalMaxTemperature < currentBody.oceanLiquid.baseFreezingPointKelvin)
                        CreateItem(climSec, "State", "<color=#55AAFF>Global Snowball</color>");
                }
            }
            else if (currentBody.oceanLiquid != null)
            {
                if (currentBody.surfacePressureAtm < 0.05)
                    CreateItem(climSec, "Sea Level", "<color=#AAAAAA>0m (Frozen Dry / Sublimated)</color>");
                else
                    CreateItem(climSec, "Sea Level", "<color=#FF5555>0m (Runaway Greenhouse)</color>");
            }
            else
            {
                CreateItem(climSec, "Sea Level", "<color=#AAAAAA>0m (No Liquid Accreted)</color>");
            }

            UIInfoSection coverSec = GetSection("GLOBAL SURFACE COVER");
            CreateItem(coverSec, "Liquid Ocean", $"{(currentBody.globalOceanCoverage * 100f):F1}%");
            CreateItem(coverSec, "Frozen Ocean", $"{(currentBody.globalIceCoverage * 100f):F1}%");
            CreateItem(coverSec, "Snow on Land", $"{(currentBody.globalSnowCoverage * 100f):F1}%");
            CreateItem(coverSec, "Forest/Biomass", $"{(currentBody.globalBiomassCoverage * 100f):F1}%");
            CreateItem(coverSec, "Barren/Desert", $"{(currentBody.globalDesertCoverage * 100f):F1}%");
        }

        UIInfoSection atmSec = GetSection("ATMOSPHERE");
        if (currentBody.surfacePressureAtm > 0.001)
        {
            CreateItem(atmSec, "Pressure", $"{currentBody.surfacePressureAtm:F3} atm");
            CreateItem(atmSec, "Greenhouse Heat", $"+{currentBody.greenhouseHeatContribution:F1} K");
            CreateItem(atmSec, "Toxicity", $"{currentBody.toxicityLevel:F2}");
            CreateItem(atmSec, "Cloud Coverage", $"{(currentBody.atmosphereCloudCoverage * 100f):F0}%");

            double totalMass = currentBody.atmosphericGasesKg.Values.Sum();
            foreach (var kvp in currentBody.atmosphericGasesKg)
            {
                GasTemplate gas = DataLibrary.Instance.GetGas(kvp.Key);
                string gasName = gas != null ? gas.gasName : $"ID {kvp.Key}";
                double percentage = (kvp.Value / totalMass) * 100.0;
                double massMt = kvp.Value / 1e9;

                double volMt = 0;
                if (currentBody.frozenVolatilesKg.ContainsKey(kvp.Key)) volMt = currentBody.frozenVolatilesKg[kvp.Key] / 1e9;

                string volText = volMt > 0 ? $" <color=#55AAFF>(+{volMt:F0} Mt Frozen)</color>" : "";
                CreateItem(atmSec, $" • {gasName}", $"{percentage:F1}% <size=80%>({massMt:F0} Mt){volText}</size>");
            }
        }
        else
        {
            CreateItem(atmSec, "Status", "<color=#FF5555>Vacuum (No Atmosphere)</color>");
            if (currentBody.frozenVolatilesKg.Count > 0)
            {
                CreateItem(atmSec, "<b>Frozen Volatiles</b>", "");
                foreach (var kvp in currentBody.frozenVolatilesKg)
                {
                    GasTemplate gas = DataLibrary.Instance.GetGas(kvp.Key);
                    string gasName = gas != null ? gas.gasName : $"ID {kvp.Key}";
                    double volMt = kvp.Value / 1e9;
                    CreateItem(atmSec, $" • {gasName}", $"<color=#55AAFF>{volMt:F0} Mt</color>");
                }
            }
        }

        UIInfoSection socSec = GetSection("ECOLOGY & SOCIETY");
        CreateItem(socSec, "Has Life", currentBody.globalBiomassCoverage > 0 ? "<color=#55FF55>Yes</color>" : "No");
        CreateItem(socSec, "Has Civilization", currentBody.globalPopulation > 0 ? "<color=#55FF55>Yes</color>" : "No");
        if (currentBody.globalPopulation > 0)
        {
            CreateItem(socSec, "Global Population", $"{currentBody.globalPopulation:N0}");
        }
    }

    private void DisplayCellInfo()
    {
        titleText.text = $"{currentBody.name} - Cell {currentCellId}";

        CellTopology topo = currentBody.localViewData.topologies[currentCellId];
        CellClimate clim = currentBody.localViewData.climates[currentCellId];
        CellEconomy econ = currentBody.localViewData.economies[currentCellId];

        UIInfoSection topoSec = GetSection("TOPOLOGY");
        double cellAreaKm2 = currentBody.GetCellAreaSqKm(currentCellId);
        double cellAreaMi2 = cellAreaKm2 * 0.386102;
        CreateItem(topoSec, "Cell Area", $"{cellAreaKm2:N0} km² <size=80%>({cellAreaMi2:N0} mi²)</size>");

        BedrockTemplate rock = DataLibrary.Instance.GetBedrock(topo.bedrockId);
        CreateItem(topoSec, "Bedrock", rock != null ? rock.bedrockName : "Unknown");
        CreateItem(topoSec, "Absolute Alt", $"{topo.altitude:F0} m");

        if (currentBody.waterLevel > -5000f)
        {
            float elevation = topo.altitude - currentBody.waterLevel;
            if (elevation > 0) CreateItem(topoSec, "Elevation", $"<color=#55FF55>+{elevation:F0} m</color> (Land)");
            else CreateItem(topoSec, "Elevation", $"<color=#5555FF>{elevation:F0} m</color> (Underwater)");
        }
        else
        {
            CreateItem(topoSec, "Elevation", $"{topo.altitude:F0} m (Dry Planet)");
        }

        CreateItem(topoSec, "Base Insolation", $"{(topo.baseInsolation * 100f):F0}%");

        UIInfoSection coverSec = GetSection("SURFACE COVER");
        string soilName = currentBody.surfaceSoil != null ? currentBody.surfaceSoil.soilName : "None";
        CreateItem(coverSec, "Soil Type", soilName);

        float tAlt = (topo.altitude - 0f) / Mathf.Max(1f, 10000f);
        float baseThickness = 1.0f - tAlt;
        float curvePower = 1.0f + ((float)currentBody.surfacePressureAtm * 2.0f);
        float thickness = Mathf.Pow(Mathf.Max(0f, baseThickness), curvePower);
        thickness -= topo.rainFactor * tAlt * 0.5f;

        float rainStrength = (clim.liquidDepth > 0 ? 1f : 0f);
        float globalSoil = currentBody.soilBaseThickness * (1.0f + (float)currentBody.surfacePressureAtm) * (float)(currentBody.surfaceGravity / 9.8) * (1.0f + rainStrength);
        thickness = Mathf.Clamp01(thickness * Mathf.Clamp(globalSoil, 0.1f, 3.0f));

        CreateItem(coverSec, "Soil Thickness", $"{(thickness * 100f):F0}%");
        CreateItem(coverSec, "Soil Wetness", $"{(clim.moisture * 100f):F0}%");

        UIInfoSection climSec = GetSection("CLIMATE");
        float tempC = clim.localTemperature - 273.15f;
        string tempColor = tempC < 0 ? "#55AAFF" : (tempC > 40 ? "#FF5555" : "#55FF55");
        CreateItem(climSec, "Temperature", $"<color={tempColor}>{clim.localTemperature:F1} K ({tempC:F1} °C)</color>");

        CreateItem(climSec, "Moisture", $"{(clim.moisture * 100f):F1}%");
        CreateItem(climSec, "Rain Factor", $"{(topo.rainFactor * 100f):F0}%");

        if (clim.liquidDepth > 0) CreateItem(climSec, "Liquid Depth", $"<color=#5555FF>{(clim.liquidDepth * 1000f):F0} m</color>");
        if (clim.snowDepth > 0) CreateItem(climSec, "Snow Depth", $"<color=#FFFFFF>{clim.snowDepth:F2} m</color>");
        if (clim.iceCover > 0) CreateItem(climSec, "Ice Coverage", $"{(clim.iceCover * 100f):F0}%");

        UIInfoSection ecoSec = GetSection("ECOLOGY & ECONOMY");
        if (clim.biomass > 0) CreateItem(ecoSec, "Biomass", $"{(clim.biomass * 100f):F1}%");
        else CreateItem(ecoSec, "Biomass", "<color=#AAAAAA>0% (Sterile)</color>");

        CreateItem(ecoSec, "Population", $"{econ.population:N0}");
        CreateItem(ecoSec, "Usable Land", $"{(econ.infrastructureCap * 100f):F0}%");
        CreateItem(ecoSec, "Urbanization", $"{(econ.developmentCap * 100f):F0}%");
    }

    private void ClearUI()
    {
        foreach (var item in activeItems)
        {
            item.gameObject.SetActive(false);
            itemPool.Add(item);
        }
        activeItems.Clear();

        foreach (var sec in activeSections)
        {
            sec.gameObject.SetActive(false);
            sectionPool.Add(sec);
        }
        activeSections.Clear();
    }

    private UIInfoSection GetSection(string title)
    {
        UIInfoSection sec;
        if (sectionPool.Count > 0)
        {
            sec = sectionPool[sectionPool.Count - 1];
            sectionPool.RemoveAt(sectionPool.Count - 1);
        }
        else
        {
            GameObject obj = Instantiate(sectionPrefab, contentContainer);
            sec = obj.GetComponent<UIInfoSection>();
        }

        sec.transform.SetAsLastSibling();
        sec.Setup(title, true);
        activeSections.Add(sec);
        return sec;
    }

    private void CreateItem(UIInfoSection section, string label, string value)
    {
        UIInfoItem item;
        if (itemPool.Count > 0)
        {
            item = itemPool[itemPool.Count - 1];
            itemPool.RemoveAt(itemPool.Count - 1);
        }
        else
        {
            GameObject obj = Instantiate(itemPrefab);
            item = obj.GetComponent<UIInfoItem>();
        }

        item.transform.SetParent(section.contentContainer, false);
        item.transform.SetAsLastSibling();
        item.Setup(label, value);
        activeItems.Add(item);
    }
}