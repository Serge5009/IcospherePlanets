using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIInfoItem : MonoBehaviour
{
    public TextMeshProUGUI labelText;
    public TextMeshProUGUI valueText;

    public Image backgroundImage;

    public void Setup(string label, string value)
    {
        if (labelText != null) labelText.text = label;
        if (valueText != null) valueText.text = value;
        gameObject.SetActive(true);
    }

    public void SetBackgroundColor(Color color)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = color;
        }
    }
}