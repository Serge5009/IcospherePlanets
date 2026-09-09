using System;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Time Settings")]
    [Tooltip("The starting date of the game.")]
    public int startYear = 2200;
    public int startMonth = 1;
    public int startDay = 1;

    [Tooltip("How many in-game seconds pass per real-life second at 1x speed.")]
    public float baseTimeScale = 86400f;

    [Header("Speed Gears")]
    public float[] speedGears = new float[] { 0f, 1f, 5f, 30f };
    public int currentGearIndex = 0;

    [Header("Current State (Read Only)")]
    public double totalGameSeconds = 0;
    public bool isPaused => speedGears[currentGearIndex] == 0f;

    private const double SECONDS_IN_HOUR = 3600;
    private const double SECONDS_IN_DAY = 86400;
    private const double SECONDS_IN_MONTH = 2592000;
    private const double SECONDS_IN_YEAR = 31104000;

    private double secondsSinceLastHour = 0;
    private double secondsSinceLastDay = 0;
    private double secondsSinceLastMonth = 0;
    private double secondsSinceLastYear = 0;

    public event Action OnHourTick;
    public event Action OnDayTick;
    public event Action OnMonthTick;
    public event Action OnYearTick;

    private DateTime startDate;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        startDate = new DateTime(startYear, startMonth, startDay);
    }

    private void Update()
    {
        float currentMultiplier = speedGears[currentGearIndex];
        if (currentMultiplier <= 0f) return;

        double deltaSeconds = Time.deltaTime * baseTimeScale * currentMultiplier;

        AdvanceTime(deltaSeconds);
    }

    private void AdvanceTime(double deltaSeconds)
    {
        totalGameSeconds += deltaSeconds;

        secondsSinceLastHour += deltaSeconds;
        secondsSinceLastDay += deltaSeconds;
        secondsSinceLastMonth += deltaSeconds;
        secondsSinceLastYear += deltaSeconds;

        while (secondsSinceLastHour >= SECONDS_IN_HOUR)
        {
            secondsSinceLastHour -= SECONDS_IN_HOUR;
            OnHourTick?.Invoke();
        }

        while (secondsSinceLastDay >= SECONDS_IN_DAY)
        {
            secondsSinceLastDay -= SECONDS_IN_DAY;
            OnDayTick?.Invoke();
        }

        while (secondsSinceLastMonth >= SECONDS_IN_MONTH)
        {
            secondsSinceLastMonth -= SECONDS_IN_MONTH;
            OnMonthTick?.Invoke();
        }

        while (secondsSinceLastYear >= SECONDS_IN_YEAR)
        {
            secondsSinceLastYear -= SECONDS_IN_YEAR;
            OnYearTick?.Invoke();
        }
    }

    public void SetSpeedGear(int gearIndex)
    {
        if (gearIndex >= 0 && gearIndex < speedGears.Length)
        {
            currentGearIndex = gearIndex;
        }
    }

    public void TogglePause()
    {
        if (currentGearIndex == 0)
        {
            currentGearIndex = 1;
        }
        else
        {
            currentGearIndex = 0;
        }
    }

    public void DebugStepHour()
    {
        AdvanceTime(SECONDS_IN_HOUR);
    }

    public void DebugStepDay()
    {
        AdvanceTime(SECONDS_IN_DAY);
    }

    public void DebugStepMonth()
    {
        AdvanceTime(SECONDS_IN_MONTH);
    }

    public DateTime GetCurrentDate()
    {
        return startDate.AddSeconds(totalGameSeconds);
    }
}