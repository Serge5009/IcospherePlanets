using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimeManagerUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI dateText;
    public TextMeshProUGUI speedText;

    public Button pauseButton;
    public Button normalSpeedButton;
    public Button fastSpeedButton;
    public Button ultraSpeedButton;

    private void Start()
    {
        if (pauseButton != null) pauseButton.onClick.AddListener(() => SetSpeed(0f));
        if (normalSpeedButton != null) normalSpeedButton.onClick.AddListener(() => SetSpeed(1f));
        if (fastSpeedButton != null) fastSpeedButton.onClick.AddListener(() => SetSpeed(10f));
        if (ultraSpeedButton != null) ultraSpeedButton.onClick.AddListener(() => SetSpeed(30f));

        UpdateSpeedText();
    }

    private void Update()
    {
        if (TimeManager.Instance == null) return;

        double totalSecs = TimeManager.Instance.totalSeconds;

        int years = (int)(totalSecs / 31536000);
        double remainder = totalSecs % 31536000;

        int days = (int)(remainder / 86400);

        if (dateText != null)
        {
            dateText.text = $"Year {years}, Day {days}";
        }
    }

    private void SetSpeed(float multiplier)
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.SetSpeedMultiplier(multiplier);
            UpdateSpeedText();
        }
    }

    private void UpdateSpeedText()
    {
        if (speedText == null || TimeManager.Instance == null) return;

        float mult = TimeManager.Instance.currentMultiplier;

        if (mult == 0) speedText.text = "PAUSED";
        else if (mult == 1) speedText.text = "PLAY (1x)";
        else speedText.text = $"FAST ({mult}x)";
    }
}