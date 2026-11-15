using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Emergency Dose — a periodic screen-clearing AOE blast on a long cooldown.
/// Represents a last-resort crisis intervention. Force-breaks all shields (bypassing layer/pulse
/// requirements) then deals heavy damage to every enemy caught in the blast radius.
/// Reuses the <see cref="PulseWave"/> prefab for the visual.
/// Attach to the Player GameObject. Wire <see cref="_data"/> and <see cref="_waveEffect"/> in the Inspector.
/// Trigger via a UI Button's onClick → <see cref="Activate"/>, or press E in the editor.
/// Call <see cref="LevelUp"/> to advance through the five levels.
/// </summary>
public class EmergencyDose : MonoBehaviour
{
    private static readonly Color BlastColor = new Color(1f, 0.3f, 0.15f, 0.9f);
    private const float BlastWaveDuration = 0.65f;

    [SerializeField] private EmergencyDoseData _data;

    /// <summary>Reuses the existing PulseWave prefab for the blast visual.</summary>
    [SerializeField] private PulseWave _waveEffect;

    private int   _level = 1;
    private float _cooldownRemaining;

    public int Level    => _level;
    public int MaxLevel => _data != null ? _data.Length : 5;

    /// <summary>Fraction of cooldown elapsed [0–1]. Useful for driving a cooldown indicator.</summary>
    public float CooldownProgress =>
        _data == null ? 1f : 1f - Mathf.Clamp01(_cooldownRemaining / _data.GetLevel(_level).cooldown);

    /// <summary>True when the ability is off cooldown and can be activated.</summary>
    public bool IsReady => _cooldownRemaining <= 0f;

    /// <summary>Advances the ability by one level, up to the maximum.</summary>
    public void LevelUp()
    {
        if (_data == null || _level >= MaxLevel) return;
        _level++;
        Debug.Log($"[EmergencyDose] Level {_level}");
    }

    /// <summary>
    /// Triggers the blast if off cooldown.
    /// Safe to call directly from a UI Button onClick event.
    /// </summary>
    public void Activate()
    {
        if (_data == null || _cooldownRemaining > 0f) return;

        EmergencyDoseLevelConfig cfg = _data.GetLevel(_level);
        FireBlast(cfg);
        _cooldownRemaining = cfg.cooldown;
    }

    private void Update()
    {
        if (_cooldownRemaining > 0f)
            _cooldownRemaining -= Time.deltaTime;

#if UNITY_EDITOR
        // Editor shortcut: E key triggers the blast during testing.
        if (Input.GetKeyDown(KeyCode.E))
            Activate();
#endif
    }

    private void FireBlast(EmergencyDoseLevelConfig cfg)
    {
        // Visual wave
        if (_waveEffect != null)
        {
            PulseWave wave = Instantiate(_waveEffect, transform.position, Quaternion.identity);
            wave.Initialize(cfg.radius, BlastWaveDuration, BlastColor);
        }

        float   radiusSq = cfg.radius * cfg.radius;
        Vector3 origin   = transform.position;

        // Snapshot to avoid mutation during iteration
        var snapshot = new List<Enemy>(Enemy.All);
        foreach (Enemy enemy in snapshot)
        {
            if (enemy == null) continue;
            if ((enemy.transform.position - origin).sqrMagnitude > radiusSq) continue;

            // Force-break ALL shield layers regardless of pulse requirements, then damage
            enemy.ForceBreakShield();
            enemy.TakeDamage(cfg.damage);
        }
    }
}
