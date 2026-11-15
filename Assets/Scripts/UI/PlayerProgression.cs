using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tracks player XP (gained from enemy kills / XP pickups) and levels up the
/// player's abilities — currently Cefepime Bolt and Zidebactam Pulse, sibling
/// components on this same GameObject. Crossing an XP threshold opens the
/// upgrade panel, whose "Current1"/"Current2" slots offer the not-yet-maxed
/// abilities to level up. A slot with nothing eligible to offer is hidden
/// rather than left blank.
///
/// The run deliberately starts and stays with just those two: Emergency Dose,
/// Tissue Lance and Synergy Field exist as components on the player but are
/// disabled and not registered here, so nothing can offer them. The panel's
/// third card ("Unlock", which used to bring a locked ability online) is
/// disabled in the scene to match — restoring that feature means re-registering
/// those abilities below AND re-adding the unlock slot here.
///
/// XP/level progress is intentionally hidden from the player (no advance
/// warning of the next upgrade) — progressFill/progressText are driven by
/// SetInfectionDisplay() instead, called from LevelProgression to show this
/// level's remaining infection percentage. levelText is hidden outright.
/// </summary>
public class PlayerProgression : MonoBehaviour
{
    [Tooltip("Image with Image Type = Filled. Repurposed: shows this level's remaining infection (0-1), pushed via SetInfectionDisplay.")]
    [SerializeField] private Image progressFill;
    [Tooltip("Repurposed: shows the same infection value as a percentage, pushed via SetInfectionDisplay.")]
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private GameObject upgradeParent;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("XP / Leveling")]
    [SerializeField] private float _baseXPToLevel = 50f;
    [SerializeField] private float _xpGrowthPerLevel = 50f;

    private class AbilityUpgrade
    {
        public string Id;
        public string Name;
        public string Description;
        public Behaviour Behaviour;
        public Func<int> GetLevel;
        public Func<int> GetMaxLevel;
        public Action DoLevelUp;
    }

    private bool _pausedForUpgrade;
    private bool _upgradeSuppressed;
    private float _currentXP;
    private int _currentLevel = 1;

    private List<AbilityUpgrade> _abilities;
    private Button _current1Button, _current2Button;
    private TextMeshProUGUI _current1Title, _current1Description;
    private TextMeshProUGUI _current2Title, _current2Description;

    void Start()
    {
        // Player level/XP is a fresh field on every scene load otherwise — read
        // it back from SaveSystem so it carries across scenes (and relaunches)
        // the same way ability upgrades do.
        _currentLevel = SaveSystem.Data.playerLevel;
        _currentXP = SaveSystem.Data.currentXP;
        if (levelText != null) levelText.SetText("Level " + _currentLevel);

        //if (levelText != null) levelText.gameObject.SetActive(false);

        BuildAbilityList();
        ApplySavedAbilities();

        // The upgrade cards live at UpgradeParent/GameObject/Current1 and
        // .../Current2 (a HorizontalLayoutGroup holds them), each with its own
        // Button + "Text (TMP)" title + "Text (TMP) (1)" description. The
        // sibling "Unlock" card is disabled in the scene and deliberately not
        // wired up here — Transform.Find would still return a disabled object,
        // so touching it would just re-activate the card.
        //
        // Current1/Current2 used to ship with a hardcoded persistent onClick
        // straight to a fixed ability's LevelUp() (bypassing XP/saving
        // entirely) — RemoveAllListeners() only clears runtime listeners, not
        // that Inspector-configured persistent call, so it had to be stripped
        // from the scene directly.
        SetupChoiceSlot("GameObject/Current1", out _current1Button, out _current1Title, out _current1Description);
        SetupChoiceSlot("GameObject/Current2", out _current2Button, out _current2Title, out _current2Description);
    }

    /// <summary>
    /// Only the two abilities the run starts with are registered. Emergency
    /// Dose ("EmergencyDose"), Tissue Lance ("Lance") and Synergy Field
    /// ("Synergy") are left out on purpose: registering them would let a stale
    /// save with unlocked:true re-enable them in ApplySavedAbilities() and put
    /// them straight into the Current1/Current2 slots.
    /// </summary>
    private void BuildAbilityList()
    {
        _abilities = new List<AbilityUpgrade>();

        AddAbility(GetComponent<CefepiimeBolt>(), "Bolt", "Cefepime Bolt",
            "Auto-fires piercing bolts at the nearest enemy",
            b => b.Level, b => b.MaxLevel, b => b.LevelUp());
        AddAbility(GetComponent<ZidebactamPulse>(), "Pulse", "Zidebactam Pulse",
            "Radial wave that shatters enemy shields",
            p => p.Level, p => p.MaxLevel, p => p.LevelUp());
    }

    private void AddAbility<T>(T ability, string id, string name, string description,
        Func<T, int> getLevel, Func<T, int> getMaxLevel, Action<T> levelUp) where T : Behaviour
    {
        if (ability == null) return;

        _abilities.Add(new AbilityUpgrade
        {
            Id = id,
            Name = name,
            Description = description,
            Behaviour = ability,
            GetLevel = () => getLevel(ability),
            GetMaxLevel = () => getMaxLevel(ability),
            DoLevelUp = () => levelUp(ability),
        });
    }

    /// <summary>
    /// Restores ability levels/unlocks from SaveSystem so upgrades carry over
    /// both across scene loads within a session and across game relaunches.
    /// Abilities without a save entry yet (never picked) keep their
    /// scene-authored defaults untouched.
    /// </summary>
    private void ApplySavedAbilities()
    {
        foreach (AbilityUpgrade ability in _abilities)
        {
            AbilitySaveEntry entry = SaveSystem.Data.FindAbility(ability.Id);
            if (entry == null)
            {
                Debug.Log($"[PlayerProgression] No save entry for '{ability.Id}' — leaving at scene-authored default (Lv {ability.GetLevel()}).");
                continue;
            }

            while (ability.GetLevel() < entry.level && ability.GetLevel() < ability.GetMaxLevel())
                ability.DoLevelUp();

            if (entry.unlocked) ability.Behaviour.enabled = true;

            Debug.Log($"[PlayerProgression] Restored '{ability.Id}' to Lv {ability.GetLevel()} (saved Lv {entry.level}), enabled={ability.Behaviour.enabled}.");
        }
    }

    private void SetupChoiceSlot(string path, out Button button, out TextMeshProUGUI title, out TextMeshProUGUI description)
    {
        button = null;
        title = null;
        description = null;
        if (upgradeParent == null) return;

        Transform slot = upgradeParent.transform.Find(path);
        if (slot == null)
        {
            Debug.LogWarning($"[PlayerProgression] Upgrade card '{path}' not found under {upgradeParent.name} — that choice slot will stay empty.");
            return;
        }

        button = slot.GetComponent<Button>();
        if (button == null) button = slot.gameObject.AddComponent<Button>();
        if (button.targetGraphic == null) button.targetGraphic = slot.GetComponent<Image>();

        // The card ships with "Text (TMP)" as its title and "Text (TMP) (1)" as
        // its description (see Current1/Current2/Unlock in the UpgradeParent
        // hierarchy). Fall back to the first two TMP texts found, in case those
        // exact names ever change.
        title = slot.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>();
        description = slot.Find("Text (TMP) (1)")?.GetComponent<TextMeshProUGUI>();

        if (title == null || description == null)
        {
            TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (title == null && texts.Length > 0) title = texts[0];
            if (description == null && texts.Length > 1) description = texts[1];
        }
    }

    /// <summary>
    /// XP needed, while at <paramref name="level"/>, to reach the next one.
    /// Closed form of the old "_xpToNextLevel += growth * newLevel" accumulation,
    /// so the threshold can be recomputed from the level alone instead of
    /// needing its own persisted field.
    /// </summary>
    private float XpThresholdForLevel(int level) =>
        _baseXPToLevel + _xpGrowthPerLevel * (level * (level + 1) / 2f - 1f);

    /// <summary>Called on every enemy kill / XP pickup.</summary>
    public void AddXP(float xpAmount)
    {
        _currentXP += xpAmount;

        bool leveledUp = false;
        while (_currentXP >= XpThresholdForLevel(_currentLevel))
        {
            _currentXP -= XpThresholdForLevel(_currentLevel);
            _currentLevel++;
            leveledUp = true;

            if (levelText != null) levelText.SetText("Level " + _currentLevel);
            ShowUpgrade();
        }

        // Level-ups flush to disk immediately (same cadence as ability picks);
        // in-between XP just updates the in-memory value so it still carries
        // into the next scene without a PlayerPrefs write on every kill.
        if (leveledUp) SaveSystem.SetPlayerLevel(_currentLevel, _currentXP);
        else SaveSystem.UpdateXPProgress(_currentXP);
    }

    /// <summary>
    /// Called by LevelProgression whenever this level's infection percentage
    /// changes. Drives the (now-repurposed) XP fill bar and its text instead.
    /// </summary>
    public void SetInfectionDisplay(float infectionPercent)
    {
        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01(infectionPercent / 100f);
        if (progressText != null)
            progressText.SetText($"{infectionPercent:0}%");
    }

    private void ShowUpgrade()
    {
        // If the level just completed (the last enemy's death both leveled the
        // player up AND cleared the final nest), suppress the upgrade panel so
        // only the level-complete black screen is shown.
        if (_upgradeSuppressed ||
            (LevelProgression.Instance != null && LevelProgression.Instance.IsCompleted))
            return;

        List<AbilityUpgrade> choices = PickRandom(
            _abilities.FindAll(a => a.Behaviour.enabled && a.GetLevel() < a.GetMaxLevel()), 2);

        if (choices.Count == 0)
            return;   // both abilities are maxed — nothing left to offer

        // Only one ability left to offer (the other is maxed) — Current2 gets a
        // null choice, which hides that card rather than leaving it blank.
        PopulateChoice(_current1Button, _current1Title, _current1Description, choices[0]);
        PopulateChoice(_current2Button, _current2Title, _current2Description,
            choices.Count > 1 ? choices[1] : null);

        upgradeParent.SetActive(true);
        _pausedForUpgrade = true;

        Time.timeScale = 0f;   // pause until the player picks an upgrade
    }

    private static List<AbilityUpgrade> PickRandom(List<AbilityUpgrade> pool, int count)
    {
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        if (pool.Count > count) pool.RemoveRange(count, pool.Count - count);
        return pool;
    }

    private void PopulateChoice(Button button, TextMeshProUGUI title, TextMeshProUGUI description,
        AbilityUpgrade choice)
    {
        if (button == null) return;

        button.gameObject.SetActive(choice != null);
        if (choice == null) return;

        if (title != null) title.SetText($"{choice.Name} (Lv {choice.GetLevel()} -> {choice.GetLevel() + 1})");
        if (description != null) description.SetText(choice.Description);

        // RemoveAllListeners() only clears runtime (AddListener) listeners, not
        // any persistent call configured in the Inspector — Current1/Current2
        // used to ship with one hardcoded straight to a fixed ability's
        // LevelUp(), which had to be deleted from the scene, not just shadowed
        // here, since both would otherwise fire on click.
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ChooseUpgrade(choice));
    }

    private void ChooseUpgrade(AbilityUpgrade choice)
    {
        choice.DoLevelUp();
        upgradeParent.SetActive(false);

        SaveSystem.SetAbilityState(choice.Id, choice.GetLevel(), true);
        Debug.Log($"[PlayerProgression] Picked '{choice.Id}' -> saved Lv {choice.GetLevel()}.");
    }

    /// <summary>
    /// Called by LevelProgression when the level completes. Hides the upgrade
    /// panel if it was opened this same frame and blocks it from opening again,
    /// so the level-complete black screen is the only thing on screen.
    /// </summary>
    public void SuppressUpgrade()
    {
        _upgradeSuppressed = true;

        if (upgradeParent != null && upgradeParent.activeSelf)
            upgradeParent.SetActive(false);

        _pausedForUpgrade = false;
    }

    void Update()
    {
        // Resume once the upgrade panel is closed (however it's dismissed),
        // but only if WE were the ones who paused — don't fight the pause menu.
        if (_pausedForUpgrade && upgradeParent != null && !upgradeParent.activeSelf)
        {
            _pausedForUpgrade = false;
            Time.timeScale = 1f;
        }
    }
}
