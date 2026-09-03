using System;
using System.Collections.Generic;
using UnityEngine;

public class MapModeManager : MonoBehaviour
{
    public static MapModeManager Instance { get; private set; }

    [Header("Available Modes")]
    [Tooltip("Assign your MapModeTemplate ScriptableObjects here.")]
    public List<MapModeTemplate> availableModes;

    [Header("Default Modes")]
    [Tooltip("The mode to switch to when toggling off an active mode.")]
    public MapModeTemplate politicalMode;

    public MapModeTemplate ActiveMode { get; private set; }
    public byte ActiveSubModeId { get; private set; }

    public event Action<MapModeTemplate, byte> OnModeChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        SetMode(politicalMode);
    }

    public void RequestModeChange(MapModeTemplate requestedMode, byte subModeId = 0)
    {
        if (ActiveMode == requestedMode && ActiveSubModeId == subModeId)
        {
            if (ActiveMode == politicalMode)
            {
                SetMode(null);
            }
            else if (ActiveMode == null)
            {
                SetMode(politicalMode);
            }
            else
            {
                SetMode(politicalMode);
            }
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
        Debug.Log($"Map Mode Changed to: {modeName} (SubID: {ActiveSubModeId})");

        OnModeChanged?.Invoke(ActiveMode, ActiveSubModeId);
    }
}