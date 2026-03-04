using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class TopBarUI : MonoBehaviour
{
    [System.Serializable] public class ResourceSlot
    {
        public ResourceDefinition def;       // drag the SO here
        public Image icon;
        public TextMeshProUGUI valueText;
        public string format = "n0";         // e.g., "n0" or custom
    }

    [Header("Refs")]
    public ResourceStore store;
    public GameCalendar calendar;
    public PlayerProgress playerProgress;

    [Header("Resource Slots (3 items or more)")]
    public List<ResourceSlot> resourceSlots = new(); // size 3, assign defs/icons/labels

    [Header("Calendar UI")]
    public Image seasonIcon;             // optional icon that should change per season
    public TextMeshProUGUI seasonLabel;  // "Spring"
    public TextMeshProUGUI dayLabel;     // "Day 7 / 28 (Y1)"
    public Image dayProgressFill;        // Image with FillMethod=Horizontal
    public Slider dayProgressSlider;     // alternative if you prefer Slider

    [Header("Text Formats")]
    public string dayFormat = "Day {0} / {1}";
    public string yearSuffix = " (Y{0})";
    public string seasonUppercase = "none"; // "upper","lower","none"

    [Header("Progress UI")]
    public Image xpFill;
    public TextMeshProUGUI levelLabel;
    public string levelFormat = "Lv {0}";

    [Header("Auto Build (optional)")]
    public bool autoBuildRows = true;
    public Transform resourcesGroupParent;
    public ResourceEntryUI resourceRowPrefab;
    public bool buildFromStoreList = false;

    [Header("Producer Sockets (optional)")]
    public bool autoBuildProducerSockets = true;
    public Transform producerSocketsParent;
    public ItemSlotUI producerSlotPrefab;
    public int producerSocketCount = 3;
    public TextMeshProUGUI producerTotalLabel;
    public ProducerSocketsManager producerManager;
    // Note: sockets are generated at runtime by instantiating `producerSlotPrefab`.

    readonly Dictionary<ResourceDefinition, ResourceEntryUI> rows = new Dictionary<ResourceDefinition, ResourceEntryUI>();

    System.Action dayChangedHandler;
    System.Action seasonChangedHandler;
    System.Action yearChangedHandler;
    System.Action progressChangedHandler;

    void OnEnable()
    {
        if (autoBuildRows) BuildRows();
        if (store != null) store.OnChanged += OnResourceChanged;
        RefreshAllResources();
        HookCalendar(true);
        HookProgress(true);
        RefreshCalendarUI(forceAll:true);
        RefreshXPUI();

        // warn if seasonIcon reference is missing but calendar has icons configured
        if (seasonIcon == null && calendar != null && calendar.seasonIcons != null && calendar.seasonIcons.Length > 0)
        {
            Debug.LogWarning("TopBarUI: seasonIcon Image field is not assigned. Season icon updates will be skipped.", this);
        }
    }

    void OnDisable()
    {
        if (store != null) store.OnChanged -= OnResourceChanged;
        HookCalendar(false);
        HookProgress(false);
    }

    void Update()
    {
        if (calendar != null)
        {
            float p = Mathf.Clamp01(calendar.dayProgress01);
            if (dayProgressFill)  dayProgressFill.fillAmount = p;
            if (dayProgressSlider) dayProgressSlider.value   = p;
        }
    }

    void HookProgress(bool on)
    {
        if (playerProgress == null) return;
        if (on)
        {
            progressChangedHandler ??= RefreshXPUI;
            playerProgress.OnChanged += progressChangedHandler;
        }
        else
        {
            if (progressChangedHandler != null)
                playerProgress.OnChanged -= progressChangedHandler;
        }
    }

    void HookCalendar(bool on)
    {
        if (calendar == null) return;
        if (on)
        {
            dayChangedHandler ??= () => RefreshCalendarUI(false, true, false);
            seasonChangedHandler ??= () => RefreshCalendarUI(true, true, false);
            yearChangedHandler ??= () => RefreshCalendarUI(false, false, true);

            calendar.OnDayChanged += dayChangedHandler;
            calendar.OnSeasonChanged += seasonChangedHandler;
            calendar.OnYearChanged += yearChangedHandler;
        }
        else
        {
            if (dayChangedHandler != null) calendar.OnDayChanged -= dayChangedHandler;
            if (seasonChangedHandler != null) calendar.OnSeasonChanged -= seasonChangedHandler;
            if (yearChangedHandler != null) calendar.OnYearChanged -= yearChangedHandler;
        }
    }

    public void BuildRows()
    {
        rows.Clear();
        if (!resourcesGroupParent || !resourceRowPrefab) return;

        for (int i = resourcesGroupParent.childCount - 1; i >= 0; i--)
            Destroy(resourcesGroupParent.GetChild(i).gameObject);

        var list = new List<ResourceDefinition>();
        if (buildFromStoreList && store != null)
        {
            foreach (var e in store.resources)
            {
                if (e.def) list.Add(e.def);
            }
        }
        else
        {
            foreach (var slot in resourceSlots)
            {
                if (slot.def) list.Add(slot.def);
            }
        }

        foreach (var def in list)
        {
            var row = Instantiate(resourceRowPrefab, resourcesGroupParent);
            int v = store ? store.Get(def) : 0;
            row.Bind(def, v.ToString("n0"));
            rows[def] = row;
        }

        // Make sure layout groups update immediately so rows are spaced correctly on first frame
        ForceResourceLayout();

        // Build producer sockets if configured
        if (autoBuildProducerSockets) BuildProducerSockets();
    }

    void BuildProducerSockets()
    {
        if (!producerSocketsParent || !producerSlotPrefab) return;

        // Clear existing children and instantiate prefabs
        for (int i = producerSocketsParent.childCount - 1; i >= 0; i--)
            DestroyImmediate(producerSocketsParent.GetChild(i).gameObject);

        var socketUIs = new ItemSlotUI[producerSocketCount];
        for (int i = 0; i < producerSocketCount; i++)
        {
            var go = Instantiate(producerSlotPrefab.gameObject, producerSocketsParent);
            go.name = "ProducerSocket_" + i;
            var ui = go.GetComponent<ItemSlotUI>();
            if (ui == null) { Debug.LogWarning("Producer slot prefab missing ItemSlotUI", go); Destroy(go); continue; }
            socketUIs[i] = ui;
        }

        if (producerManager == null)
        {
            // attach a manager if none assigned
            producerManager = producerSocketsParent.GetComponent<ProducerSocketsManager>();
            if (producerManager == null) producerManager = producerSocketsParent.gameObject.AddComponent<ProducerSocketsManager>();
        }

        // Wire store and spirit resource if this TopBar has them in resourceSlots (try find def with id 'spirit')
        if (producerManager.store == null) producerManager.store = store;
        if (producerManager.spiritResource == null)
        {
            var spirit = resourceSlots.Find(s => s.def && s.def.id == "spirit")?.def;
            producerManager.spiritResource = spirit;
        }

        producerManager.totalSpiritLabel = producerTotalLabel;
        producerManager.AssignSockets(socketUIs, producerSocketCount);
    }

    void OnResourceChanged(ResourceDefinition def, int newValue)
    {
        if (autoBuildRows && rows.TryGetValue(def, out var row))
        {
            row.SetValue(newValue);
            return;
        }

        foreach (var slot in resourceSlots)
        {
            if (slot.def == def && slot.valueText)
                slot.valueText.text = newValue.ToString(slot.format);
        }
    }

    public void RefreshAllResources()
    {
        if (store == null) return;

        if (autoBuildRows && rows.Count > 0)
        {
            foreach (var kv in rows)
                kv.Value.SetValue(store.Get(kv.Key));
            return;
        }

        foreach (var slot in resourceSlots)
        {
            if (!slot.def) continue;
            if (slot.icon) slot.icon.sprite = slot.def.icon;
            if (slot.valueText) slot.valueText.text = store.Get(slot.def).ToString(slot.format);
        }
    }

    public void RefreshCalendarUI(bool seasonChanged = true, bool dayChanged = true, bool yearChanged = true, bool forceAll=false)
    {
        if (calendar == null) return;

        if (seasonChanged || forceAll)
        {
            string sName = calendar.CurrentSeasonName;
            if (seasonUppercase == "upper") sName = sName.ToUpperInvariant();
            else if (seasonUppercase == "lower") sName = sName.ToLowerInvariant();

            if (seasonLabel) seasonLabel.text = sName;
            // seasonDot removed; coloring handled by the seasonIcon sprite
            if (seasonIcon)
            {
                // if the calendar has an icon for the current season, apply it
                // otherwise retain whatever sprite was already set on the image
                var icon = calendar.CurrentSeasonIcon;
                // debug log helps diagnose why the sprite isn't changing
                Debug.Log($"TopBarUI.RefreshCalendarUI: season index={calendar.SeasonIndex}, icon={(icon!=null?icon.name:"<null>")}", seasonIcon);
                if (icon != null)
                {
                    seasonIcon.sprite = icon;
                }
                // if you want the slot to clear when no icon is assigned, uncomment:
                // else seasonIcon.sprite = null;
                // optionally toggle visibility based on whether we have a sprite:
                // seasonIcon.enabled = seasonIcon.sprite != null;
            }
        }

        if (dayChanged || yearChanged || forceAll)
        {
            string day = string.Format(dayFormat, calendar.DayInSeason, calendar.daysPerSeason);
            string year = string.Format(yearSuffix, calendar.Year);
            if (dayLabel) dayLabel.text = day + year;
        }

        // progress handled per-frame in Update() for smoothness
    }

    void RefreshXPUI()
    {
        if (playerProgress == null) return;
        int prevReq = (playerProgress.Level <= 1) ? 0 : playerProgress.RequiredXPForLevel(playerProgress.Level);
        int nextReq = playerProgress.RequiredXPForLevel(playerProgress.Level + 1);
        float span = Mathf.Max(1f, nextReq - prevReq);
        float p = Mathf.Clamp01((playerProgress.XP - prevReq) / span);

        if (xpFill) xpFill.fillAmount = p;
        if (levelLabel) levelLabel.text = string.Format(levelFormat, playerProgress.Level);
    }

    void ForceResourceLayout()
    {
        if (!resourcesGroupParent) return;
        var rt = resourcesGroupParent as RectTransform;
        if (!rt) rt = resourcesGroupParent.GetComponent<RectTransform>();
        if (!rt) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    // Simple debug actions (hook to buttons if you want)
    public void AddGold(int n)    => TryAdd("gold", n);
    public void AddSpirit(int n)  => TryAdd("spirit", n);
    public void AddPopulation(int n) => TryAdd("population", n);

    void TryAdd(string id, int n)
    {
        if (store == null) return;
        var def = resourceSlots.Find(r => r.def && r.def.id == id)?.def;
        if (def) store.Add(def, n);
    }
}
