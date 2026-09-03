using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIMapModeTab : UITabPanel
{
    [Header("UI Containers")]
    public Transform mainModeContainer;
    public Transform subModeContainer;

    [Header("Prefabs")]
    public GameObject buttonPrefab;

    private void Start()
    {
        if (MapModeManager.Instance != null)
        {
            MapModeManager.Instance.OnModeChanged += HandleModeChanged;
            GenerateMainButtons();
        }
    }

    private void OnDestroy()
    {
        if (MapModeManager.Instance != null)
        {
            MapModeManager.Instance.OnModeChanged -= HandleModeChanged;
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if (MapModeManager.Instance != null)
        {
            HandleModeChanged(MapModeManager.Instance.ActiveMode, MapModeManager.Instance.ActiveSubModeId);
        }
    }

    protected override void Refresh()
    {
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