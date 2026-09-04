using UnityEngine;

public class Nation
{
    public int id;
    public string name;
    public Color color;

    public Nation(int id, string name, Color color)
    {
        this.id = id;
        this.name = name;
        this.color = color;
    }
}