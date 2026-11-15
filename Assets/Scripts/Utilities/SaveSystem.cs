using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Persisted state for a single upgradeable ability (see PlayerProgression).</summary>
[Serializable]
public class AbilitySaveEntry
{
    public string id;
    public int level;
    public bool unlocked;
}

/// <summary>
/// Everything written to disk. Deliberately does NOT include in-progress level
/// state (which nests/chambers are cleared, current HP, etc.) — only a level
/// fully cleared advances <see cref="highestClearedLevel"/>, so quitting
/// mid-level always restarts that level from its beginning. Ability upgrades,
/// by contrast, are saved unconditionally as soon as they're picked.
/// </summary>
[Serializable]
public class SaveData
{
    /// <summary>0 = no level cleared yet, 1 = Level 1 cleared, 2 = Level 2 cleared, 3 = Boss cleared.</summary>
    public int highestClearedLevel;
    public List<AbilitySaveEntry> abilities = new List<AbilitySaveEntry>();

    /// <summary>Player XP level (levelText in PlayerProgression) and progress toward the next one.</summary>
    public int playerLevel = 1;
    public float currentXP;

    /// <summary>
    /// Whole-run time in seconds (see <see cref="GameTimer"/>). Written when a
    /// level is cleared, so Continue resumes the clock instead of restarting it.
    /// </summary>
    public float elapsedSeconds;

    public AbilitySaveEntry FindAbility(string id) => abilities.Find(a => a.id == id);
}

/// <summary>
/// Disk-backed save game (PlayerPrefs-based — no separate file I/O needed).
/// Holds the single always-current save slot: ability upgrade levels/unlocks
/// (saved the instant an upgrade is picked, in PlayerProgression) and the
/// highest level fully cleared (saved by LevelProgression/Boss on completion),
/// which MainMenu uses to enable its Continue button.
/// </summary>
public static class SaveSystem
{
    private const string PrefsKey = "ZaynichSaveData";

    /// <summary>Scene names in play order — index+1 is the "cleared" value that scene's completion writes.</summary>
    public static readonly string[] SceneOrder = { "Level 1", "Level 2", "Boss" };

    private static SaveData _data;

    public static SaveData Data => _data ??= Load();

    private static SaveData Load()
    {
        if (!PlayerPrefs.HasKey(PrefsKey)) return new SaveData();

        try
        {
            return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(PrefsKey)) ?? new SaveData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] Failed to parse save data, starting fresh. {e.Message}");
            return new SaveData();
        }
    }

    public static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }

    /// <summary>Wipes ability upgrades and the level checkpoint — called when Start (New Game) is pressed.</summary>
    public static void ResetForNewGame()
    {
        _data = new SaveData();
        Save();
    }

    /// <summary>True once at least one level is cleared and the game isn't already fully complete.</summary>
    public static bool HasContinue => Data.highestClearedLevel > 0 && Data.highestClearedLevel < SceneOrder.Length;

    /// <summary>The scene Continue should load — the level right after the highest one cleared.</summary>
    public static string GetContinueSceneName()
    {
        int index = Mathf.Clamp(Data.highestClearedLevel, 0, SceneOrder.Length - 1);
        return SceneOrder[index];
    }

    /// <summary>Called when <paramref name="sceneName"/> is fully cleared. No-ops if it isn't past the current checkpoint.</summary>
    public static void MarkLevelCleared(string sceneName)
    {
        int index = Array.IndexOf(SceneOrder, sceneName);
        if (index < 0) return;

        int clearedValue = index + 1;
        if (clearedValue <= Data.highestClearedLevel) return;

        Data.highestClearedLevel = clearedValue;
        Save();
    }

    /// <summary>Records an ability's level/unlock state and saves immediately.</summary>
    public static void SetAbilityState(string id, int level, bool unlocked)
    {
        AbilitySaveEntry entry = Data.FindAbility(id);
        if (entry == null)
        {
            entry = new AbilitySaveEntry { id = id };
            Data.abilities.Add(entry);
        }

        entry.level = level;
        entry.unlocked = unlocked;
        Save();
    }

    /// <summary>
    /// Updates in-memory XP progress only — called on every kill/pickup, so it
    /// deliberately skips the PlayerPrefs disk write (that would mean a flush
    /// per kill). The in-memory value still carries correctly into the next
    /// scene within the same session; only the last-flushed value survives a
    /// full quit, matching how in-progress level state is never saved either.
    /// </summary>
    public static void UpdateXPProgress(float currentXP) => Data.currentXP = currentXP;

    /// <summary>Records a player level-up and flushes to disk immediately (same cadence as ability picks).</summary>
    public static void SetPlayerLevel(int level, float currentXP)
    {
        Data.playerLevel = level;
        Data.currentXP = currentXP;
        Save();
    }

    /// <summary>
    /// Commits the whole-run clock. Called on level completion rather than every
    /// frame — matching how in-progress level state is never saved, a quit
    /// mid-level rewinds the clock to the start of that level along with it.
    /// </summary>
    public static void SetElapsedTime(float seconds)
    {
        Data.elapsedSeconds = seconds;
        Save();
    }
}
