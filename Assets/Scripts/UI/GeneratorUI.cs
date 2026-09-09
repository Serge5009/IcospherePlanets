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
        ShowMenu();

        cyclesSlider.onValueChanged.AddListener(UpdateCyclesText);
        generateButton.onClick.AddListener(StartGeneration);

        UpdateCyclesText(cyclesSlider.value);
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

        await ClimateResolver.Instance.GenerateInitialClimateAsync(SystemDataGenerator.Instance.allBodies, cycles, msg => statusText.text = msg);

        statusText.text = "Simulation Complete.";

        Invoke(nameof(HideStatusText), 3f);
    }

    private void HideStatusText()
    {
        if (statusText != null) statusText.gameObject.SetActive(false);
    }
}