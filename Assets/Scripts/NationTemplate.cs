using UnityEngine;

[CreateAssetMenu(fileName = "New Nation", menuName = "Strategy/Templates/Nation")]
public class NationTemplate : ScriptableObject
{
    public string nationName;
    public Color defaultColor = Color.white;
    public Color secondaryColor = Color.gray;
}