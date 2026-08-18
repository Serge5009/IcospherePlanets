using System;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Time Settings")]
    [Tooltip("How many in-game seconds pass per real-life second")]
    public float timeScale = 86400f;

    [Header("Current Time")]
    [Tooltip("Total continuous seconds since the start of the simulation")]
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
        double deltaSeconds = Time.deltaTime * timeScale;
        totalSeconds += deltaSeconds;

        secondsSinceLastDailyTick += deltaSeconds;
        secondsSinceLastMonthlyTick += deltaSeconds;

        if (secondsSinceLastDailyTick >= SECONDS_IN_DAY)
        {
            secondsSinceLastDailyTick -= SECONDS_IN_DAY;
            OnDailyTick?.Invoke();
        }

        if (secondsSinceLastMonthlyTick >= SECONDS_IN_MONTH)
        {
            secondsSinceLastMonthlyTick -= SECONDS_IN_MONTH;
            OnMonthlyTick?.Invoke();
        }
    }

    public void SetTimeScale(float newScale)
    {
        timeScale = newScale;
    }
}