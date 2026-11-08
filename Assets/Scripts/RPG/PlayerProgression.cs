using UnityEngine;

/// <summary>
/// Player experience and level-ups. No hard level cap.
/// </summary>
public class PlayerProgression : MonoBehaviour
{
    public static PlayerProgression Instance { get; private set; }

    [System.NonSerialized] public int level = 1;
    [System.NonSerialized] public int xp;
    [System.NonSerialized] public int xpToNext = 40;

    public string lastLevelUpMessage = "";
    float _msgUntil;

    void Awake()
    {
        Instance = this;
        if (level < 1) level = 1;
        xpToNext = ProgressionFormulas.XpToNextLevel(level);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public float XpPercent => xpToNext <= 0 ? 0f : (float)xp / xpToNext;

    public void ApplySave(int savedLevel, int savedXp)
    {
        level = Mathf.Max(1, savedLevel);
        xpToNext = ProgressionFormulas.XpToNextLevel(level);
        xp = Mathf.Clamp(savedXp, 0, Mathf.Max(0, xpToNext - 1));
    }

    public void GainXp(int amount)
    {
        if (amount <= 0) return;

        var gained = ProgressionRules.ApplyXp(level, xp, amount);
        level = gained.Level;
        xp = gained.Xp;
        xpToNext = ProgressionRules.XpToNextLevel(level);
        bool leveled = gained.LevelsGained > 0;

        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.ShowLoot(leveled
                ? $"Level Up! Lv {level}   (+{amount} EXP)"
                : $"+{amount} EXP");
            PlayerInventory.Instance.ApplyEquipmentStats(leveled);
        }

        if (leveled)
        {
            lastLevelUpMessage = $"LEVEL UP → Lv {level}";
            _msgUntil = Time.time + 3f;
        }

        if (AccountManager.Instance != null && AccountManager.Instance.IsReady)
            AccountManager.Instance.SaveActive(silent: true);
    }

    public bool HasLevelUpMessage => Time.time < _msgUntil;

    void OnGUI()
    {
        if (!HasLevelUpMessage) return;
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel) return;
        RPGUI.Ensure();
        float w = Mathf.Min(Screen.width - 40f, 640f);
        Rect tip = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.28f, w, RPGUI.BannerH);
        RPGUI.DrawPanel(tip, new Color(0.22f, 0.16f, 0.04f, 0.94f));
        RPGUI.DrawText(tip, lastLevelUpMessage, RPGUI.Banner);
    }
}
