using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIMapModeTab : UITabPanel
{
    [Header("UI Containers")]
    public Transform mainModeContainer;
    public Transform subModeContainer;

    [Header("Prefabs")]
    public GameObject buttonPrefab;

    private void Start()
    {
        if (buttonPrefab != null && mainModeContainer != null)
        {
            ClearContainer(mainModeContainer);
            ClearContainer(subModeContainer);

            CreateButton(mainModeContainer, "Political", () => Debug.Log("Political Mode Clicked"));
            CreateButton(mainModeContainer, "Temperature", () => { ClearContainer(subModeContainer); Debug.Log("Temperature Mode Clicked"); });
            CreateButton(mainModeContainer, "Resources", PopulateDummySubModes);
            CreateButton(mainModeContainer, "None", () => { ClearContainer(subModeContainer); Debug.Log("None Mode Clicked"); });
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
    }

    protected override void Refresh()
    {
    }

    public void CreateButton(Transform container, string text, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = Instantiate(buttonPrefab, container);

        TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null) btnText.text = text;

        Button btn = btnObj.GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(onClick);
    }

    public void ClearContainer(Transform container)
    {
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
    }

    private void PopulateDummySubModes()
    {
        ClearContainer(subModeContainer);
        CreateButton(subModeContainer, "Iron", () => Debug.Log("Iron Selected"));
        CreateButton(subModeContainer, "Organics", () => Debug.Log("Organics Selected"));
        CreateButton(subModeContainer, "Silicates", () => Debug.Log("Silicates Selected"));
    }
}