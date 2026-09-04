using System.Collections.Generic;
using UnityEngine;

public class GeopoliticsManager : MonoBehaviour
{
    public static GeopoliticsManager Instance { get; private set; }

    [Header("Starting Setup")]
    public NationTemplate[] startingNations;
    public Color unclaimedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    private Dictionary<int, Nation> activeNations = new Dictionary<int, Nation>();
    private int nextNationId = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeStartingNations();
    }

    private void InitializeStartingNations()
    {
        activeNations.Clear();
        nextNationId = 1;

        activeNations.Add(0, new Nation(0, "Unclaimed", unclaimedColor));

        if (startingNations != null)
        {
            foreach (var template in startingNations)
            {
                if (template != null)
                {
                    CreateNation(template.nationName, template.defaultColor);
                }
            }
        }
    }

    public int CreateNation(string name, Color color)
    {
        int newId = nextNationId;
        Nation newNation = new Nation(newId, name, color);
        activeNations.Add(newId, newNation);

        nextNationId++;
        return newId;
    }

    public Nation GetNation(int id)
    {
        if (activeNations.TryGetValue(id, out Nation nation))
        {
            return nation;
        }
        return activeNations[0];
    }

    public int GetTotalNations()
    {
        return activeNations.Count;
    }

    public Vector4[] GetNationColorsAsArray()
    {
        Vector4[] colors = new Vector4[nextNationId];
        for (int i = 0; i < nextNationId; i++)
        {
            if (activeNations.TryGetValue(i, out Nation n))
            {
                colors[i] = new Vector4(n.color.r, n.color.g, n.color.b, n.color.a);
            }
            else
            {
                colors[i] = new Vector4(unclaimedColor.r, unclaimedColor.g, unclaimedColor.b, unclaimedColor.a);
            }
        }
        return colors;
    }
}