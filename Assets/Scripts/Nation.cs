using UnityEngine;

public class Nation
{
    public int id;
    public string name;
    public Color mapColor;

    public Nation(int id, string name, Color mapColor)
    {
        this.id = id;
        this.name = name;
        this.mapColor = mapColor;
    }
}