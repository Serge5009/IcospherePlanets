using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIInfoSection : MonoBehaviour
{
    public Button headerButton;
    public TextMeshProUGUI headerText;
    public Transform contentContainer;
    public GameObject contentPanel;

    private void Start()
    {
        if (headerButton != null)
        {
            headerButton.onClick.AddListener(ToggleSection);
        }
    }

    public void Setup(string title, bool startOpen = true)
    {
        if (headerText != null) headerText.text = title;
        if (contentPanel != null) contentPanel.SetActive(startOpen);
        gameObject.SetActive(true);
    }

    private void ToggleSection()
    {
        if (contentPanel != null)
        {
            contentPanel.SetActive(!contentPanel.activeSelf);

            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());

            if (transform.parent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent.GetComponent<RectTransform>());
            }
        }
    }

    public void ApplyZebraStriping(Color darkColor, Color lightColor)
    {
        if (contentContainer == null) return;

        int activeIndex = 0;
        foreach (Transform child in contentContainer)
        {
            if (!child.gameObject.activeSelf) continue;

            UIInfoItem item = child.GetComponent<UIInfoItem>();
            if (item != null)
            {
                bool isEven = (activeIndex % 2 == 0);
                item.SetBackgroundColor(isEven ? darkColor : lightColor);
                activeIndex++;
            }
        }
    }
}