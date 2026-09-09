using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System;

public class UITimePanel : MonoBehaviour
{
    [Header("Readouts")]
    public TextMeshProUGUI dateText;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI speedText;

    [Header("Speed Controls")]
    public Button pauseBtn;
    public Button speed1Btn;
    public Button speed2Btn;
    public Button speed3Btn;

    [Header("Debug Controls")]
    public Button stepHourBtn;
    public Button stepDayBtn;
    public Button stepMonthBtn;

    [Header("Input Actions")]
    public InputActionReference togglePauseAction;
    public InputActionReference speedUpAction;
    public InputActionReference speedDownAction;

    [Header("UI Colors")]
    public Color activeColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    public Color inactiveColor = Color.white;

    private void Start()
    {
        if (TimeManager.Instance == null) return;

        if (pauseBtn != null) pauseBtn.onClick.AddListener(() => TimeManager.Instance.SetSpeedGear(0));
        if (speed1Btn != null) speed1Btn.onClick.AddListener(() => TimeManager.Instance.SetSpeedGear(1));
        if (speed2Btn != null) speed2Btn.onClick.AddListener(() => TimeManager.Instance.SetSpeedGear(2));
        if (speed3Btn != null) speed3Btn.onClick.AddListener(() => TimeManager.Instance.SetSpeedGear(3));

        if (stepHourBtn != null) stepHourBtn.onClick.AddListener(() => TimeManager.Instance.DebugStepHour());
        if (stepDayBtn != null) stepDayBtn.onClick.AddListener(() => TimeManager.Instance.DebugStepDay());
        if (stepMonthBtn != null) stepMonthBtn.onClick.AddListener(() => TimeManager.Instance.DebugStepMonth());
    }

    private void OnEnable()
    {
        if (togglePauseAction != null) togglePauseAction.action.Enable();
        if (speedUpAction != null) speedUpAction.action.Enable();
        if (speedDownAction != null) speedDownAction.action.Enable();
    }

    private void OnDisable()
    {
        if (togglePauseAction != null) togglePauseAction.action.Disable();
        if (speedUpAction != null) speedUpAction.action.Disable();
        if (speedDownAction != null) speedDownAction.action.Disable();
    }

    private void Update()
    {
        if (TimeManager.Instance == null) return;

        UpdateReadouts();
        UpdateButtons();
        HandleInput();
    }

    private void UpdateReadouts()
    {
        DateTime currentDate = TimeManager.Instance.GetCurrentDate();

        if (dateText != null) dateText.text = currentDate.ToString("dd MM yyyy");

        if (timeText != null) timeText.text = currentDate.ToString("HH:mm");

        if (speedText != null)
        {
            int gear = TimeManager.Instance.currentGearIndex;
            float multiplier = TimeManager.Instance.speedGears[gear];

            if (gear == 0) speedText.text = "0x";
            else speedText.text = $"{multiplier}x";
        }
    }

    private void UpdateButtons()
    {
        int gear = TimeManager.Instance.currentGearIndex;

        SetButtonColor(pauseBtn, gear == 0);
        SetButtonColor(speed1Btn, gear == 1);
        SetButtonColor(speed2Btn, gear == 2);
        SetButtonColor(speed3Btn, gear == 3);
    }

    private void SetButtonColor(Button btn, bool isActive)
    {
        if (btn == null) return;

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = isActive ? activeColor : inactiveColor;
        }
    }

    private void HandleInput()
    {
        if (togglePauseAction != null && togglePauseAction.action.WasPressedThisFrame())
        {
            TimeManager.Instance.TogglePause();
        }

        if (speedUpAction != null && speedUpAction.action.WasPressedThisFrame())
        {
            int nextGear = Mathf.Min(TimeManager.Instance.currentGearIndex + 1, TimeManager.Instance.speedGears.Length - 1);
            TimeManager.Instance.SetSpeedGear(nextGear);
        }

        if (speedDownAction != null && speedDownAction.action.WasPressedThisFrame())
        {
            int prevGear = Mathf.Max(TimeManager.Instance.currentGearIndex - 1, 0);
            TimeManager.Instance.SetSpeedGear(prevGear);
        }
    }
}