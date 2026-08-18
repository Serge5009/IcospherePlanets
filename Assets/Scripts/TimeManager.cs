using System;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Time Settings")]
    [Tooltip("Base speed: How many in-game seconds pass per real-life second at 1x speed")]
    public float baseTimeScale = 86400f;

    [Tooltip("Current speed multiplier (0 = Paused, 1 = Normal, 10 = Fast)")]
    public float currentMultiplier = 1f;

    [Header("Current Time")]
    public double totalSeconds = 0;

    private double secondsSinceLastDailyTick = 0;
    private double secondsSinceLastMonthlyTick = 0;

    private const double SECONDS_IN_DAY = 86400;
    private const double SECONDS_IN_MONTH = 2592000;

    public event Action OnDailyTick;
    public event Action OnMonthlyTick;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (currentMultiplier <= 0f) return;

        double deltaSeconds = Time.deltaTime * baseTimeScale * currentMultiplier;
        totalSeconds += deltaSeconds;

        secondsSinceLastDailyTick += deltaSeconds;
        secondsSinceLastMonthlyTick += deltaSeconds;

        while (secondsSinceLastDailyTick >= SECONDS_IN_DAY)
        {
            secondsSinceLastDailyTick -= SECONDS_IN_DAY;
            OnDailyTick?.Invoke();
        }

        while (secondsSinceLastMonthlyTick >= SECONDS_IN_MONTH)
        {
            secondsSinceLastMonthlyTick -= SECONDS_IN_MONTH;
            OnMonthlyTick?.Invoke();
        }
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        currentMultiplier = multiplier;
    }

    public void Pause()
    {
        currentMultiplier = 0f;
    }
}