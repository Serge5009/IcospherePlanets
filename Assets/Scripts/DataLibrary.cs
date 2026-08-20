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
    private Dictionary<byte, GasTemplate> gasDict;
    private Dictionary<byte, ResourceTemplate> resourceDict;

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

        gasDict = new Dictionary<byte, GasTemplate>();
        foreach (var g in gases) if (g != null) gasDict[g.gasId] = g;

        resourceDict = new Dictionary<byte, ResourceTemplate>();
        foreach (var r in resources) if (r != null) resourceDict[r.resourceId] = r;
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

    public GasTemplate GetGas(byte id)
    {
        if (gasDict.TryGetValue(id, out var template)) return template;
        return gases.Length > 0 ? gases[0] : null;
    }

    public ResourceTemplate GetResource(byte id)
    {
        if (resourceDict.TryGetValue(id, out var template)) return template;
        return resources.Length > 0 ? resources[0] : null;
    }
}