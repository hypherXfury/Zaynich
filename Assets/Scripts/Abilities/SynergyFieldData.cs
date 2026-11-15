using UnityEngine;

/// <summary>
/// Synergy Field configuration across all five levels.
/// Create via Assets > Create > Zaynich > Synergy Field Data.
/// </summary>
[CreateAssetMenu(fileName = "SynergyFieldData", menuName = "Zaynich/Synergy Field Data")]
public class SynergyFieldData : ScriptableObject
{
    [SerializeField]
    private SynergyFieldLevelConfig[] _levels = new SynergyFieldLevelConfig[]
    {
        // Lv 1 — small, gentle aura
        new SynergyFieldLevelConfig { radius = 2.0f, damagePerSecond =  8f, tickInterval = 0.5f },
        // Lv 2 — wider reach
        new SynergyFieldLevelConfig { radius = 2.8f, damagePerSecond = 14f, tickInterval = 0.5f },
        // Lv 3 — meaningful damage output
        new SynergyFieldLevelConfig { radius = 3.5f, damagePerSecond = 22f, tickInterval = 0.5f },
        // Lv 4 — strong pressure field
        new SynergyFieldLevelConfig { radius = 4.2f, damagePerSecond = 32f, tickInterval = 0.5f },
        // Lv 5 — max: large area, high DPS, faster ticks
        new SynergyFieldLevelConfig { radius = 5.0f, damagePerSecond = 45f, tickInterval = 0.3f },
    };

    public int Length => _levels.Length;

    /// <summary>Returns the config for the given 1-based level, clamped to valid range.</summary>
    public SynergyFieldLevelConfig GetLevel(int level) =>
        _levels[Mathf.Clamp(level - 1, 0, _levels.Length - 1)];
}
