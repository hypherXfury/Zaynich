using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zidebactam Pulse — player-triggered radial wave that shatters enemy shields.
/// Attach to the Player GameObject alongside other ability scripts.
/// Wire <see cref="_data"/> and <see cref="_waveEffect"/> in the Inspector.
/// Trigger via a UI Button's onClick → <see cref="Activate"/>, or press Space in the editor.
/// </summary>
public class ZidebactamPulse : MonoBehaviour
{
    /// <summary>Golden-yellow wave tint matching the yellow shield colour.</summary>
    private static readonly Color WaveColor = new Color(1f, 0.88f, 0.15f, 0.85f);

    private const float WaveDuration = 0.45f;

    [SerializeField] private PulseData  _data;
    [SerializeField] private PulseWave  _waveEffect;

    private int   _level = 1;
    private float _cooldownRemaining;

    public int   Level    => _level;
    public int   MaxLevel => _data != null ? _data.Length : 5;

    /// <summary>Fraction of cooldown elapsed [0–1]. Useful for driving a cooldown indicator.</summary>
    public float CooldownProgress =>
        _data == null ? 1f : 1f - Mathf.Clamp01(_cooldownRemaining / _data.GetLevel(_level).cooldown);

    /// <summary>True when the ability is off cooldown and can be fired.</summary>
    public bool IsReady => _cooldownRemaining <= 0f;

    /// <summary>
    /// Raised each time the pulse actually fires (not on a cooldown-blocked tap).
    /// The tutorial uses the first one to dismiss its "use the pulse" card the
    /// moment the player presses the button, rather than making them wait until
    /// the shielded enemy is dead.
    /// </summary>
    public event System.Action OnPulseFired;
    public Image fillBar;
    private float timer;
    private bool isCoolingDown;
    private float coolDownFillBar;

    void Start()
    {
        fillBar.fillAmount = 1;
    }

    /// <summary>Advances the ability by one level, up to the maximum.</summary>
    public void LevelUp()
    {
        if (_data == null || _level >= MaxLevel) return;
        _level++;
        Debug.Log($"[ZidebactamPulse] Level {_level}");
    }

    /// <summary>
    /// Triggers the pulse if off cooldown.
    /// Safe to call directly from a UI Button onClick event.
    /// </summary>
    public void Activate()
    {
        if (_data == null || _cooldownRemaining > 0f) return;

        PulseLevelConfig cfg = _data.GetLevel(_level);
        FirePulse(cfg);
        _cooldownRemaining = cfg.cooldown;
        coolDownFillBar = _cooldownRemaining;
        isCoolingDown = true;

        OnPulseFired?.Invoke();
    }

    private void Update()
    {
        if (_cooldownRemaining > 0f)
            _cooldownRemaining -= Time.deltaTime;

#if UNITY_EDITOR
        // Editor shortcut for quick testing — Space key activates the pulse.
        if (Input.GetKeyDown(KeyCode.Space))
            Activate();
#endif

        if(fillBar != null)
        {
            if (isCoolingDown)
        {
            timer += Time.deltaTime;

            fillBar.fillAmount = timer / coolDownFillBar;

            if (timer >= coolDownFillBar)
            {
                fillBar.fillAmount = 1f;
                isCoolingDown = false;
                timer = 0f;
            }
        }
        }
    }

    private void FirePulse(PulseLevelConfig cfg)
    {
        SpawnWave(cfg.radius);
        AffectEnemiesInRange(cfg);
    }

    private void SpawnWave(float radius)
    {
        if (_waveEffect == null) return;
        PulseWave wave = Instantiate(_waveEffect, transform.position, Quaternion.identity);
        wave.Initialize(radius, WaveDuration, WaveColor);
    }

    private void AffectEnemiesInRange(PulseLevelConfig cfg)
    {
        float   radiusSq = cfg.radius * cfg.radius;
        Vector3 origin   = transform.position;

        // Snapshot the list so OnDisable/OnDeath callbacks don't mutate it mid-loop
        var snapshot = new List<Enemy>(Enemy.All);

        foreach (Enemy enemy in snapshot)
        {
            if (enemy == null) continue;
            if ((enemy.transform.position - origin).sqrMagnitude > radiusSq) continue;

            bool wasShielded = enemy.HasShield;

            // BreakShield now returns true only when ALL layers have been removed
            bool shieldFullyBroke = wasShielded && enemy.BreakShield();

            if (cfg.oneShots && shieldFullyBroke)
            {
                // Level 5: one-shot kills any enemy whose last shield layer the pulse just stripped
                enemy.TakeDamage(float.MaxValue);
            }
            else if (cfg.bonusDamage > 0f)
            {
                enemy.TakeDamage(cfg.bonusDamage);
            }
        }
    }
}
