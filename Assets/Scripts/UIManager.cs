using UnityEngine;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panel Animation")]
    public RectTransform leftPanel;
    public float hiddenX = -400f;
    public float visibleX = 0f;
    public float slideSpeed = 10f;
    private bool isPanelOpen = false;

    [Header("Tabs")]
    public List<UITabPanel> tabs;
    private UITabPanel activeTab;

    private CelestialBody currentBody;
    private int currentCellId = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        if (leftPanel != null)
            leftPanel.anchoredPosition = new Vector2(hiddenX, leftPanel.anchoredPosition.y);

        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnPlanetSelected += HandlePlanetSelected;
            SelectionManager.Instance.OnCellSelected += HandleCellSelected;
        }

        if (tabs.Count > 0)
        {
            SwitchTab(tabs[0]);
        }
    }

    private void Update()
    {
        if (leftPanel == null) return;

        float targetX = isPanelOpen ? visibleX : hiddenX;
        float currentX = leftPanel.anchoredPosition.x;

        if (Mathf.Abs(currentX - targetX) > 0.1f)
        {
            float newX = Mathf.Lerp(currentX, targetX, Time.deltaTime * slideSpeed);
            leftPanel.anchoredPosition = new Vector2(newX, leftPanel.anchoredPosition.y);
        }
    }

    public void SwitchTab(UITabPanel newTab)
    {
        if (activeTab != null) activeTab.OnClose();

        activeTab = newTab;

        if (activeTab != null)
        {
            activeTab.OnOpen();
            if (currentBody != null)
            {
                activeTab.ReceiveData(currentBody, currentCellId);
            }
        }
    }

    public void SwitchTabByIndex(int index)
    {
        if (index >= 0 && index < tabs.Count)
        {
            SwitchTab(tabs[index]);
        }
    }

    private void HandlePlanetSelected(CelestialBody body)
    {
        isPanelOpen = true;
        currentBody = body;
        currentCellId = -1;

        foreach (var tab in tabs)
        {
            tab.ReceiveData(currentBody, currentCellId);
        }
    }

    private void HandleCellSelected(CelestialBody body, int cellId)
    {
        isPanelOpen = true;
        currentBody = body;
        currentCellId = cellId;

        foreach (var tab in tabs)
        {
            tab.ReceiveData(currentBody, currentCellId);
        }
    }

    public void ClosePanel()
    {
        isPanelOpen = false;
    }
}