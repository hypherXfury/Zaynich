using UnityEngine;

/// <summary>
/// Cefepime Bolt configuration across all five levels.
/// Create via Assets > Create > Zaynich > Bolt Data.
/// Level stats are pre-populated — tweak in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "BoltData", menuName = "Zaynich/Bolt Data")]
public class BoltData : ScriptableObject
{
    [SerializeField]
    private BoltLevelConfig[] _levels = new BoltLevelConfig[]
    {
        // Lv 1 — one clean shot
        new BoltLevelConfig { damage = 15f, fireRate = 1.0f, piercingCount = 0, splits = false, splitCount = 0, splitAngle = 0f },
        // Lv 2 — faster, one-shots E. coli
        new BoltLevelConfig { damage = 22f, fireRate = 1.5f, piercingCount = 0, splits = false, splitCount = 0, splitAngle = 0f },
        // Lv 3 — first pierce (punches through one extra enemy)
        new BoltLevelConfig { damage = 30f, fireRate = 2.0f, piercingCount = 1, splits = false, splitCount = 0, splitAngle = 0f },
        // Lv 4 — pierces two extra enemies
        new BoltLevelConfig { damage = 40f, fireRate = 2.5f, piercingCount = 2, splits = false, splitCount = 0, splitAngle = 0f },
        // Lv 5 — max: three pierce + splits into two bolts on death
        new BoltLevelConfig { damage = 55f, fireRate = 3.0f, piercingCount = 3, splits = true,  splitCount = 2, splitAngle = 60f },
    };

    public int Length => _levels.Length;

    /// <summary>Returns the config for the given 1-based level, clamped to valid range.</summary>
    public BoltLevelConfig GetLevel(int level) =>
        _levels[Mathf.Clamp(level - 1, 0, _levels.Length - 1)];
}
