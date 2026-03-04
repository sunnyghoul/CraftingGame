using UnityEngine;
using System;

public class GameCalendar : MonoBehaviour
{
    [Header("Calendar")]
    [Min(1)] public int daysPerSeason = 28;
    [Min(1)] public int seasonsPerYear = 4;
    public string[] seasonNames = new[] { "Spring", "Summer", "Autumn", "Winter" };
    public Color[] seasonColors = new[]
    {
        new Color(0.5f,1f,0.5f),
        new Color(1f,0.9f,0.4f),
        new Color(1f,0.6f,0.3f),
        new Color(0.8f,0.9f,1f)
    };
    
    // optional artwork for each season, used by UI if assigned
    public Sprite[] seasonIcons = new Sprite[0];

    [Header("Start State")]
    public int startYear = 1;
    public int startSeasonIndex = 0;
    [Range(1, 999)] public int startDayInSeason = 1;

    [Header("Time Flow")]
    public bool autoAdvance = true;
    [Min(0.1f)] public float realSecondsPerGameDay = 20f;
    public float timeScale = 1f;
    [Range(0f, 1f)] public float dayProgress01;

    public int Year { get; private set; }
    public int SeasonIndex { get; private set; }
    public int DayInSeason { get; private set; }

    public event Action OnDayChanged;
    public event Action OnSeasonChanged;
    public event Action OnYearChanged;

    float accum;

    void Awake()
    {
        Year = Mathf.Max(1, startYear);
        SeasonIndex = Mathf.Clamp(startSeasonIndex, 0, Mathf.Max(0, seasonsPerYear - 1));
        DayInSeason = Mathf.Clamp(startDayInSeason, 1, daysPerSeason);
        dayProgress01 = 0f;
    }

    void OnValidate()
    {
        // keep the seasonIcons array in sync with the number of seasons
        if (seasonIcons == null)
            seasonIcons = new Sprite[seasonsPerYear];
        else if (seasonIcons.Length != seasonsPerYear)
        {
            Array.Resize(ref seasonIcons, seasonsPerYear);
        }
    }

    void Update()
    {
        if (!autoAdvance || realSecondsPerGameDay <= 0f || seasonsPerYear <= 0) return;

        accum += Time.unscaledDeltaTime * timeScale;
        dayProgress01 = Mathf.Clamp01(accum / realSecondsPerGameDay);

        if (dayProgress01 >= 1f)
        {
            AdvanceOneDay();
            accum = 0f;
            dayProgress01 = 0f;
        }
    }

    public void AdvanceOneDay()
    {
        DayInSeason++;
        if (DayInSeason > daysPerSeason)
        {
            DayInSeason = 1;
            SeasonIndex++;
            OnDayChanged?.Invoke();
            if (SeasonIndex >= seasonsPerYear)
            {
                SeasonIndex = 0;
                Year++;
                OnSeasonChanged?.Invoke();
                OnYearChanged?.Invoke();
                return;
            }
            OnSeasonChanged?.Invoke();
            return;
        }
        OnDayChanged?.Invoke();
    }

    public string CurrentSeasonName =>
        (seasonNames != null && seasonNames.Length > 0)
            ? seasonNames[Mathf.Clamp(SeasonIndex, 0, seasonNames.Length - 1)]
            : $"Season {SeasonIndex + 1}";

    public Color CurrentSeasonColor =>
        (seasonColors != null && seasonColors.Length > 0)
            ? seasonColors[Mathf.Clamp(SeasonIndex, 0, seasonColors.Length - 1)]
            : Color.white;

    /// <summary>
    /// Returns the sprite assigned for the current season index, or null if none
    /// </summary>
    public Sprite CurrentSeasonIcon
    {
        get
        {
            if (seasonIcons != null && seasonIcons.Length > 0)
            {
                int idx = Mathf.Clamp(SeasonIndex, 0, seasonIcons.Length - 1);
                return seasonIcons[idx];
            }
            return null;
        }
    }
}
