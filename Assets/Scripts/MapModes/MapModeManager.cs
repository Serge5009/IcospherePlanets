using System;
using System.Collections.Generic;
using UnityEngine;

public class MapModeManager : MonoBehaviour
{
    public static MapModeManager Instance { get; private set; }

    [Header("Available Modes")]
    public List<MapModeTemplate> availableModes;

    [Header("Default Modes")]
    public MapModeTemplate politicalMode;

    public MapModeTemplate ActiveMode { get; private set; }
    public byte ActiveSubModeId { get; private set; }

    public bool ShowAtmosphere { get; private set; } = true;

    public event Action<MapModeTemplate, byte> OnModeChanged;
    public event Action<bool> OnAtmosphereToggled;

    private Dictionary<MapModeType, IMapModeProcessor> processors;
    private ClearMapMode clearProcessor;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        processors = new Dictionary<MapModeType, IMapModeProcessor>
        {
            { MapModeType.Gradient, new GradientMapModeProcessor() },
            { MapModeType.Political, new PoliticalMapModeProcessor() }
        };

        clearProcessor = new ClearMapMode();
    }

    private void Start()
    {
        SetMode(null);
    }

    public void RequestModeChange(MapModeTemplate requestedMode, byte subModeId = 0)
    {
        if (ActiveMode == requestedMode && ActiveSubModeId == subModeId)
        {
            if (ActiveMode == politicalMode) SetMode(null);
            else if (ActiveMode == null) SetMode(politicalMode);
            else SetMode(politicalMode);
        }
        else
        {
            SetMode(requestedMode, subModeId);
        }
    }

    public void SetMode(MapModeTemplate newMode, byte subModeId = 0)
    {
        ActiveMode = newMode;
        ActiveSubModeId = subModeId;

        string modeName = ActiveMode != null ? ActiveMode.modeName : "None";
        Debug.Log($"Map Mode Changed to: {modeName}");

        ApplyModeToAllPlanets();

        OnModeChanged?.Invoke(ActiveMode, ActiveSubModeId);
    }

    public void ToggleAtmosphere(bool show)
    {
        if (ShowAtmosphere == show) return;
        ShowAtmosphere = show;
        OnAtmosphereToggled?.Invoke(ShowAtmosphere);
    }

    public void ApplyModeToAllPlanets()
    {
        if (SystemDataGenerator.Instance == null || SystemDataGenerator.Instance.allBodies.Count == 0) return;

        bool isPolitical = ActiveMode != null && ActiveMode.modeType == MapModeType.Political;

        IMapModeProcessor processor = clearProcessor;
        if (ActiveMode != null && processors.ContainsKey(ActiveMode.modeType))
        {
            processor = processors[ActiveMode.modeType];
        }

        foreach (var body in SystemDataGenerator.Instance.allBodies)
        {
            if (body.bodyType == BodyType.Star || body.bodyType == BodyType.GasGiant) continue;

            if (body.localViewData != null) processor.ApplyMode(body.localViewData, ActiveMode, ActiveSubModeId);
            if (body.systemViewData != null) processor.ApplyMode(body.systemViewData, ActiveMode, ActiveSubModeId);

            if (isPolitical || ActiveMode == null)
            {
                if (body.localViewData != null) clearProcessor.ApplyMode(body.localViewData, null, 0);
                if (body.systemViewData != null) clearProcessor.ApplyMode(body.systemViewData, null, 0);
            }
        }

        foreach (Planet p in Planet.ActivePlanets)
        {
            if (p != null)
            {
                p.UpdateOverlayBuffer();
                p.UpdatePoliticalBuffer();

                bool hiddenByAtmos = p.bodyData.atmosphereVisualOpacity >= 0.99f && ShowAtmosphere;
                p.politicalRenderer.enabled = isPolitical && !hiddenByAtmos;
            }
        }
    }
}