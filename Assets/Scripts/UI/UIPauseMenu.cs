using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class UIPauseMenu : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject pausePanel;

    [Header("Buttons")]
    public Button continueBtn;
    public Button settingsBtn;
    public Button restartBtn;
    public Button exitBtn;

    [Header("Input Actions")]
    public InputActionReference toggleMenuAction;

    private int previousGearIndex = 1;
    private bool isMenuOpen = false;

    private void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);

        if (continueBtn != null) continueBtn.onClick.AddListener(CloseMenu);
        if (settingsBtn != null) settingsBtn.onClick.AddListener(OpenSettings);
        if (restartBtn != null) restartBtn.onClick.AddListener(RestartGame);
        if (exitBtn != null) exitBtn.onClick.AddListener(ExitGame);
    }

    private void OnEnable()
    {
        if (toggleMenuAction != null) toggleMenuAction.action.Enable();
    }

    private void OnDisable()
    {
        if (toggleMenuAction != null) toggleMenuAction.action.Disable();
    }

    private void Update()
    {
        if (toggleMenuAction != null && toggleMenuAction.action.WasPressedThisFrame())
        {
            if (isMenuOpen) CloseMenu();
            else OpenMenu();
        }
    }

    private void OpenMenu()
    {
        isMenuOpen = true;
        if (pausePanel != null) pausePanel.SetActive(true);

        if (TimeManager.Instance != null)
        {
            previousGearIndex = TimeManager.Instance.currentGearIndex;
            TimeManager.Instance.SetSpeedGear(0);
        }
    }

    private void CloseMenu()
    {
        isMenuOpen = false;
        if (pausePanel != null) pausePanel.SetActive(false);

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.SetSpeedGear(previousGearIndex);
        }
    }

    private void OpenSettings()
    {
        Debug.Log("Settings Menu Placeholder Clicked.");
    }

    private void RestartGame()
    {
        Debug.Log("Restarting Universe...");

        CloseMenu();

        List<Planet> planetsToDestroy = new List<Planet>(Planet.ActivePlanets);
        foreach (var p in planetsToDestroy)
        {
            if (p != null) Destroy(p.gameObject);
        }
        Planet.ActivePlanets.Clear();

        if (SystemDataGenerator.Instance != null)
        {
            SystemDataGenerator.Instance.allBodies.Clear();
        }

        if (SpaceCameraController.Instance != null)
        {
            SpaceCameraController.Instance.ResetToDefault();
        }

        GeneratorUI genUI = FindFirstObjectByType<GeneratorUI>();
        if (genUI != null)
        {
            genUI.ShowMenu();
        }
    }

    private void ExitGame()
    {
        Debug.Log("Exiting Game...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}