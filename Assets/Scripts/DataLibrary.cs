using UnityEngine;
using System.Collections.Generic;

public class DataLibrary : MonoBehaviour
{
    public static DataLibrary Instance { get; private set; }

    [Header("Registries")]
    public BedrockTemplate[] bedrocks;
    public LiquidTemplate[] liquids;
    public GasTemplate[] gases;
    public ResourceTemplate[] resources;

    private Dictionary<byte, BedrockTemplate> bedrockDict;
    private Dictionary<byte, LiquidTemplate> liquidDict;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeDictionaries();
    }

    private void InitializeDictionaries()
    {
        bedrockDict = new Dictionary<byte, BedrockTemplate>();
        foreach (var b in bedrocks) if (b != null) bedrockDict[b.bedrockId] = b;

        liquidDict = new Dictionary<byte, LiquidTemplate>();
        foreach (var l in liquids) if (l != null) liquidDict[l.liquidId] = l;
    }

    public BedrockTemplate GetBedrock(byte id)
    {
        if (bedrockDict.TryGetValue(id, out var template)) return template;
        Debug.LogWarning($"Bedrock ID {id} not found in DataLibrary!");
        return bedrocks.Length > 0 ? bedrocks[0] : null;
    }

    public LiquidTemplate GetLiquid(byte id)
    {
        if (liquidDict.TryGetValue(id, out var template)) return template;
        return liquids.Length > 0 ? liquids[0] : null;
    }
}