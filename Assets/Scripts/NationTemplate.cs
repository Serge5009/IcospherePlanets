using UnityEngine;

[CreateAssetMenu(fileName = "New Nation", menuName = "Strategy/Nation Template")]
public class NationTemplate : ScriptableObject
{
    public string nationName;
    public Color defaultColor = Color.white;
}