using System.Collections.Generic;
using UnityEngine;

public class GeopoliticsManager : MonoBehaviour
{
    public static GeopoliticsManager Instance { get; private set; }

    [Header("Starting Setup")]
    public NationTemplate[] startingNations;
    public Color unclaimedColor = new Color(0.2f, 0.2f, 0.2f, 1f);

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
        activeNations.Add(0, new Nation(0, "Unclaimed", unclaimedColor));

        foreach (var template in startingNations)
        {
            CreateNation(template.nationName, template.defaultColor);
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
}