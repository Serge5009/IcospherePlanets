using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GeneratorUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject menuPanel;
    public TMP_InputField seedInput;
    public Slider cyclesSlider;
    public TextMeshProUGUI cyclesText;
    public Button generateButton;
    public TextMeshProUGUI statusText;

    private void Start()
    {
        menuPanel.SetActive(true);
        statusText.text = "Ready to Generate.";

        cyclesSlider.onValueChanged.AddListener(UpdateCyclesText);
        generateButton.onClick.AddListener(StartGeneration);

        UpdateCyclesText(cyclesSlider.value);
    }

    private void UpdateCyclesText(float value)
    {
        cyclesText.text = $"Simulation Cycles: {value:F0}";
    }

    private async void StartGeneration()
    {
        menuPanel.SetActive(false);
        statusText.text = "Generating Accretion Disk...";

        int seed = string.IsNullOrEmpty(seedInput.text) ? Random.Range(0, 999999) : seedInput.text.GetHashCode();
        Random.InitState(seed);

        SystemDataGenerator.Instance.GenerateData();

        await SystemMeshGenerator.Instance.GenerateMeshesAsync(SystemDataGenerator.Instance.allBodies, msg => statusText.text = msg);

        int cycles = Mathf.RoundToInt(cyclesSlider.value);
        int visualUpdateFrequency = Mathf.Max(1, cycles / 100);

        await ClimateResolver.Instance.ResolveEquilibriumAsync(SystemDataGenerator.Instance.allBodies, cycles, msg => statusText.text = msg);

        statusText.text = "Simulation Complete.";

        Destroy(statusText.gameObject, 3f);
    }
}