using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIMinimapPanel : MonoBehaviour
{
    [Header("Minimap Placeholders")]
    public RawImage minimapImage;
    public TextMeshProUGUI minimapContextText;
    public Slider zoomSlider;

    [Header("Map Mode Containers")]
    public Transform mainModeContainer;
    public Transform subModeContainer;

    [Header("Prefabs")]
    public GameObject buttonPrefab;

    [Header("Toggles")]
    public Toggle atmosphereToggle;

    private void Start()
    {
        if (MapModeManager.Instance != null)
        {
            MapModeManager.Instance.OnModeChanged += HandleModeChanged;
            GenerateMainButtons();

            if (atmosphereToggle != null)
            {
                atmosphereToggle.isOn = MapModeManager.Instance.ShowAtmosphere;
                atmosphereToggle.onValueChanged.AddListener((isOn) =>
                {
                    MapModeManager.Instance.ToggleAtmosphere(isOn);
                });
            }
        }
    }

    private void Update()
    {
        UpdateMinimapContext();
    }

    private void OnDestroy()
    {
        if (MapModeManager.Instance != null)
        {
            MapModeManager.Instance.OnModeChanged -= HandleModeChanged;
        }
    }

    private void UpdateMinimapContext()
    {
        if (SpaceCameraController.Instance == null) return;

        CameraState state = SpaceCameraController.Instance.currentState;

        if (minimapContextText != null)
        {
            minimapContextText.text = state.ToString();
        }

        if (zoomSlider != null)
        {
            float zLevel = 1f - SpaceCameraController.Instance.currentZLevel;
            zoomSlider.SetValueWithoutNotify(zLevel);
        }
    }

    private void GenerateMainButtons()
    {
        ClearContainer(mainModeContainer);

        foreach (var mode in MapModeManager.Instance.availableModes)
        {
            if (mode == null) continue;

            MapModeTemplate capturedMode = mode;

            CreateButton(mainModeContainer, mode.modeName, () =>
            {
                MapModeManager.Instance.RequestModeChange(capturedMode);
            });
        }

        CreateButton(mainModeContainer, "None", () =>
        {
            MapModeManager.Instance.RequestModeChange(null);
        });
    }

    private void HandleModeChanged(MapModeTemplate activeMode, byte subModeId)
    {
        ClearContainer(subModeContainer);

        if (activeMode != null && activeMode.modeType == MapModeType.Resource)
        {
            if (DataLibrary.Instance != null && DataLibrary.Instance.resources != null)
            {
                foreach (var resource in DataLibrary.Instance.resources)
                {
                    if (resource == null) continue;

                    byte capturedId = resource.resourceId;
                    CreateButton(subModeContainer, resource.resourceName, () =>
                    {
                        MapModeManager.Instance.RequestModeChange(activeMode, capturedId);
                    });
                }
            }
        }

        HighlightActiveButton(activeMode);
    }

    private void HighlightActiveButton(MapModeTemplate activeMode)
    {
        string activeName = activeMode != null ? activeMode.modeName : "None";

        foreach (Transform child in mainModeContainer)
        {
            Button btn = child.GetComponent<Button>();
            TextMeshProUGUI txt = child.GetComponentInChildren<TextMeshProUGUI>();

            if (btn != null && txt != null)
            {
                ColorBlock cb = btn.colors;
                cb.normalColor = (txt.text == activeName) ? new Color(0.2f, 0.8f, 0.2f, 1f) : Color.white;
                btn.colors = cb;
            }
        }
    }

    private void CreateButton(Transform container, string text, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = Instantiate(buttonPrefab, container);

        TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null) btnText.text = text;

        Button btn = btnObj.GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(onClick);
    }

    private void ClearContainer(Transform container)
    {
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
    }
}