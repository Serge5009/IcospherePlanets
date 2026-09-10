using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GeneratorUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject menuPanel;
    public TMP_InputField seedInput;

    [Header("Cycle Sliders")]
    public Slider dryCyclesSlider;
    public TextMeshProUGUI dryCyclesText;

    public Slider wetCyclesSlider;
    public TextMeshProUGUI wetCyclesText;

    public Slider simCyclesSlider;
    public TextMeshProUGUI simCyclesText;

    [Header("Controls")]
    public Button generateButton;
    public TextMeshProUGUI statusText;

    private void Start()
    {
        ShowMenu();

        if (dryCyclesSlider != null) dryCyclesSlider.onValueChanged.AddListener(UpdateDryText);
        if (wetCyclesSlider != null) wetCyclesSlider.onValueChanged.AddListener(UpdateWetText);
        if (simCyclesSlider != null) simCyclesSlider.onValueChanged.AddListener(UpdateSimText);

        generateButton.onClick.AddListener(StartGeneration);

        if (dryCyclesSlider != null) UpdateDryText(dryCyclesSlider.value);
        if (wetCyclesSlider != null) UpdateWetText(wetCyclesSlider.value);
        if (simCyclesSlider != null) UpdateSimText(simCyclesSlider.value);
    }

    public void ShowMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        if (statusText != null)
        {
            statusText.gameObject.SetActive(true);
            statusText.text = "Ready to Generate.";
        }
    }

    private void UpdateDryText(float value) { if (dryCyclesText != null) dryCyclesText.text = $"Dry Cycles: {value:F0}"; }
    private void UpdateWetText(float value) { if (wetCyclesText != null) wetCyclesText.text = $"Wet Cycles: {value:F0}"; }
    private void UpdateSimText(float value) { if (simCyclesText != null) simCyclesText.text = $"Sim Cycles: {value:F0}"; }

    private async void StartGeneration()
    {
        menuPanel.SetActive(false);
        statusText.text = "Generating Accretion Disk...";

        int seed = string.IsNullOrEmpty(seedInput.text) ? Random.Range(0, 999999) : seedInput.text.GetHashCode();
        Random.InitState(seed);

        SystemDataGenerator.Instance.GenerateData();

        await SystemMeshGenerator.Instance.GenerateMeshesAsync(SystemDataGenerator.Instance.allBodies, msg => statusText.text = msg);

        int dry = dryCyclesSlider != null ? Mathf.RoundToInt(dryCyclesSlider.value) : 10;
        int wet = wetCyclesSlider != null ? Mathf.RoundToInt(wetCyclesSlider.value) : 10;
        int sim = simCyclesSlider != null ? Mathf.RoundToInt(simCyclesSlider.value) : 20;

        await ClimateResolver.Instance.GenerateInitialClimateAsync(SystemDataGenerator.Instance.allBodies, dry, wet, sim, msg => statusText.text = msg);

        statusText.text = "Simulation Complete.";

        Invoke(nameof(HideStatusText), 3f);
    }

    private void HideStatusText()
    {
        if (statusText != null) statusText.gameObject.SetActive(false);
    }
}