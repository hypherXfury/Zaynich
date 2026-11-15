using UnityEngine;

/// <summary>
/// Emergency Dose configuration across all five levels.
/// Create via Assets > Create > Zaynich > Emergency Dose Data.
/// </summary>
[CreateAssetMenu(fileName = "EmergencyDoseData", menuName = "Zaynich/Emergency Dose Data")]
public class EmergencyDoseData : ScriptableObject
{
    [SerializeField]
    private EmergencyDoseLevelConfig[] _levels = new EmergencyDoseLevelConfig[]
    {
        // Lv 1 — narrow blast, very long cooldown
        new EmergencyDoseLevelConfig { radius =  6f, damage = 120f, cooldown = 45f },
        // Lv 2 — wider reach, slightly shorter cooldown
        new EmergencyDoseLevelConfig { radius =  8f, damage = 160f, cooldown = 38f },
        // Lv 3 — screen-clearing potential begins
        new EmergencyDoseLevelConfig { radius = 10f, damage = 210f, cooldown = 32f },
        // Lv 4 — large blast, manageable cooldown
        new EmergencyDoseLevelConfig { radius = 13f, damage = 270f, cooldown = 26f },
        // Lv 5 — max: massive radius, very high damage, respectable cooldown
        new EmergencyDoseLevelConfig { radius = 16f, damage = 350f, cooldown = 20f },
    };

    public int Length => _levels.Length;

    /// <summary>Returns the config for the given 1-based level, clamped to valid range.</summary>
    public EmergencyDoseLevelConfig GetLevel(int level) =>
        _levels[Mathf.Clamp(level - 1, 0, _levels.Length - 1)];
}
