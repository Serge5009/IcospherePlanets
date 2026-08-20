using UnityEngine;

[CreateAssetMenu(fileName = "New Resource", menuName = "Strategy/Templates/Resource")]
public class ResourceTemplate : ScriptableObject
{
    [Tooltip("Unique ID used to map struct data to this SO. Must be unique!")]
    public byte resourceId;

    public string resourceName;

    [Tooltip("Base value or extraction difficulty modifier")]
    public float baseValue = 1.0f;
}