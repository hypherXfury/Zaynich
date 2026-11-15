using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central vitals system: Health, Oxygen, Infection.
/// Health is DERIVED — it is never set directly, only recalculated from
/// Oxygen and Infection using a weighted formula.
///
/// ── FORMULA ───────────────────────────────────────────────────────────────
///   Health = (OxygenWeight * Oxygen) + (InfectionWeight * (100 - Infection))
///   Default weights: 70% Oxygen, 30% inverse-Infection.
///
/// ── TRIGGERS ──────────────────────────────────────────────────────────────
///   Oxygen   ↓  when the player takes damage (call ReportPlayerDamaged)
///            ↓  when Infection rises from an enemy acting (automatic, see below)
///            ↑  when an oxygen pickup is collected (call CollectOxygen)
///   Infection ↑ when an enemy "acts" — e.g. spawns or attacks (call ReportEnemyAction)
///             ↓ when an enemy is killed (call ReportEnemyKilled), scaled by that
///               enemy's EnemyData.infectionContribution — tougher enemies swing more
///
/// ── SETUP ─────────────────────────────────────────────────────────────────
///   1. Add this component to a persistent GameObject (e.g. "GameManager").
///   2. Assign the three Slider references in the Inspector.
///   3. Call VitalsManager.Instance.ReportEnemyKilled(enemyData) from Enemy.Die()
///      or from the spawner's death callback.
///   4. Call VitalsManager.Instance.ReportPlayerDamaged(amount) from PlayerHealth
///      whenever the player takes damage.
///   5. Call VitalsManager.Instance.CollectOxygen(amount) from your oxygen pickup script.
/// </summary>
public class VitalsManager : MonoBehaviour
{
    public static VitalsManager Instance { get; private set; }

    // ── Inspector — Starting Values ──────────────────────────────────────────

    [Header("Starting Values (0–100)")]
    [SerializeField] [Range(0f, 100f)] private float _startingOxygen   = 12f;

    [Header("Health Formula Weights")]
    [Tooltip("Weight applied to Oxygen in the health formula. Should sum to 1 with InfectionWeight.")]
    [SerializeField] [Range(0f, 1f)] private float _oxygenWeight    = 0.7f;
    [Tooltip("Weight applied to (100 - Infection) in the health formula.")]
    [SerializeField] [Range(0f, 1f)] private float _infectionWeight = 0.3f;

    [Header("Oxygen Drain Amounts")]
    [Tooltip("Oxygen lost per point of damage the player takes (e.g. 0.2 means 10 damage drains 2 oxygen).")]
    [SerializeField] private float _oxygenLossPerDamagePoint = 0.15f;
    [Tooltip("Oxygen lost per point that Infection rises (enemies acting also strains oxygen).")]
    [SerializeField] private float _oxygenLossPerInfectionPoint = 0.3f;

    [Header("Infection — Per Enemy Action")]
    [Tooltip("Default infection gained when an enemy 'acts' (spawns/attacks) if its " +
             "EnemyData doesn't specify a custom value.")]
    [SerializeField] private float _defaultInfectionPerAction = 1.5f;

    [Header("UI — Fill Images (Image Type = Filled, 0-1)")]
    [SerializeField] private Image _healthFillBar;
    [SerializeField] private Image _oxygenFillBar;
    [SerializeField] private Image _infectionFillBar;

    [Header("UI — Value Texts")]
    [SerializeField] private TextMeshProUGUI _healthText;
    [SerializeField] private TextMeshProUGUI _oxygenText;
    [SerializeField] private TextMeshProUGUI _infectionText;

    [Tooltip("Separate label describing the current level's infection (set via SetInfectionDescription).")]
    [SerializeField] private TextMeshProUGUI _infectionDescriptionText;

    [Header("UI — Fill Bar Colors (optional)")]
    [SerializeField] private Gradient _healthGradient;   // red at low, green at high

    [Header("Game Over")]
    [Tooltip("If true, reaching 0 health triggers OnPlayerDeath automatically.")]
    [SerializeField] private bool _autoTriggerDeath = true;

    // ── Public read-only state ───────────────────────────────────────────────

    public float Health    { get; private set; }
    public float Oxygen    { get; private set; }
    public float Infection { get; private set; }

    public const float MaxValue = 100f;

    /// <summary>Raised whenever Health changes. Arg: new health value [0,100].</summary>
    public event Action<float> OnHealthChanged;
    /// <summary>Raised whenever Oxygen changes. Arg: new oxygen value [0,100].</summary>
    public event Action<float> OnOxygenChanged;
    /// <summary>Raised whenever Infection changes. Arg: new infection value [0,100].</summary>
    public event Action<float> OnInfectionChanged;
    /// <summary>Raised once when Health reaches 0.</summary>
    public event Action OnPlayerDeath;

    public PlayerHealth playerHealth;

    private bool _isDead;
    

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Oxygen    = Mathf.Clamp(_startingOxygen, 0f, MaxValue);

        // Infection is the persistent Bloodstream Infection stat carried across
        // scenes for the whole play session (see InfectionState) — not a
        // per-scene starting value — so scenes without a LevelProgression
        // component (e.g. the Boss scene) still pick up where the last level
        // left off instead of resetting to a hardcoded number.
        Infection = Mathf.Clamp(InfectionState.Value, 0f, MaxValue);
    }

    private void Start()
    {
        // Runs after every object's Awake (incl. PlayerHealth's), so AdjustHealth
        // below isn't clobbered by PlayerHealth.Awake resetting CurrentHealth.
        RecalculateHealth();
        PushAllToUI();
    }

    // ── UI helpers ─────────────────────────────────────────────────────────────

    private static void SetFill(Image bar, float value01)
    {
        if (bar != null) bar.fillAmount = Mathf.Clamp01(value01);
    }

    private static void SetText(TextMeshProUGUI label, float value)
    {
        if (label != null) label.SetText($"{value:0}%");
    }

    /// <summary>Sets the standalone infection description label (e.g. per-level flavor text).</summary>
    public void SetInfectionDescription(string description)
    {
        if (_infectionDescriptionText != null)
            _infectionDescriptionText.SetText(description);
    }

    // ── Public API — call these from gameplay events ─────────────────────────

    /// <summary>
    /// Call when the player takes damage (from PlayerHealth.TakeDamage).
    /// Drains Oxygen proportionally, which in turn affects Health.
    /// </summary>
    public void ReportPlayerDamaged(float damageAmount)
    {
        if (_isDead || damageAmount <= 0f) return;
        AdjustOxygen(-damageAmount * _oxygenLossPerDamagePoint);
    }

    /// <summary>
    /// Call when an enemy performs an action that should worsen infection
    /// (e.g. spawning, attacking, or simply existing on a timer).
    /// Optionally pass the enemy's EnemyData to use a custom infection value.
    /// </summary>
    public void ReportEnemyAction(EnemyData enemyData = null)
    {
        if (_isDead) return;

        float amount = enemyData != null && enemyData.infectionContribution > 0f
            ? enemyData.infectionContribution
            : _defaultInfectionPerAction;

        //AdjustInfection(amount);

        // Infection rising also strains oxygen
        AdjustOxygen(-amount * _oxygenLossPerInfectionPoint);
    }

    /// <summary>
    /// Call when an enemy is killed. Infection itself is no longer moved by
    /// individual kills — it's now the Bloodstream Infection stat, driven
    /// externally by LevelProgression/InfectionState via SetInfectionPercent()
    /// as whole levels are cleared. Kept for call-site compatibility (Enemy.Die())
    /// and future non-Infection on-kill effects.
    /// </summary>
    public void ReportEnemyKilled(EnemyData enemyData)
    {
        if (_isDead) return;
    }

    /// <summary>
    /// Directly sets Infection to an external percentage (0–100). Used by
    /// LevelProgression to sync the Bloodstream Infection stat into this slider
    /// and the derived Health formula whenever a level's nests are fully cleared.
    /// </summary>
    public void SetInfectionPercent(float percent)
    {
        if (_isDead) return;

        Infection = Mathf.Clamp(percent, 0f, MaxValue);
        OnInfectionChanged?.Invoke(Infection);
        SetFill(_infectionFillBar, Infection / MaxValue);
        SetText(_infectionText, Infection);

        RecalculateHealth();
    }

    /// <summary>
    /// Call when the player collects an oxygen pickup.
    /// </summary>
    public void CollectOxygen(float amount)
    {
        if (_isDead) return;
        AdjustOxygen(amount);
    }

    // ── Internal adjustment + recalculation ───────────────────────────────────

    private void AdjustOxygen(float delta)
    {
        Oxygen = Mathf.Clamp(Oxygen + delta, 5f, MaxValue);
        OnOxygenChanged?.Invoke(Oxygen);
        SetFill(_oxygenFillBar, Oxygen / MaxValue);
        SetText(_oxygenText, Oxygen);

        RecalculateHealth();
    }

    private void AdjustInfection(float delta)
    {
        Infection = Mathf.Clamp(Infection + delta, 0f, MaxValue);
        OnInfectionChanged?.Invoke(Infection);
        SetFill(_infectionFillBar, Infection / MaxValue);
        SetText(_infectionText, Infection);

        RecalculateHealth();
    }

    /// <summary>
    /// Health is always DERIVED from Oxygen and Infection — never set directly.
    /// Health = (oxygenWeight * Oxygen) + (infectionWeight * (100 - Infection))
    /// </summary>
    private void RecalculateHealth()
    {
        float previous = Health;

        Health = Mathf.Clamp(
            (_oxygenWeight * Oxygen) + (_infectionWeight * (MaxValue - Infection)),
            0f, MaxValue);

        if (!Mathf.Approximately(previous, Health))
            OnHealthChanged?.Invoke(Health);

        SetFill(_healthFillBar, Health / MaxValue);
        SetText(_healthText, Health);

        if (_healthFillBar != null && _healthGradient != null)
            _healthFillBar.color = _healthGradient.Evaluate(Health / MaxValue);

        if (_autoTriggerDeath && Health <= 0f && !_isDead)
        {
            _isDead = true;
            OnPlayerDeath?.Invoke();
        }

        if (playerHealth != null) playerHealth.AdjustHealth(Health);
    }

    private void PushAllToUI()
    {
        SetFill(_healthFillBar,    Health / MaxValue);
        SetFill(_oxygenFillBar,    Oxygen / MaxValue);
        SetFill(_infectionFillBar, Infection / MaxValue);

        SetText(_healthText,    Health);
        SetText(_oxygenText,    Oxygen);
        SetText(_infectionText, Infection);

        if (_healthFillBar != null && _healthGradient != null)
            _healthFillBar.color = _healthGradient.Evaluate(Health / MaxValue);
    }
}