using UnityEngine;

/// <summary>
/// Tissue Penetration Lance configuration across all five levels.
/// Create via Assets > Create > Zaynich > Lance Data.
/// </summary>
[CreateAssetMenu(fileName = "LanceData", menuName = "Zaynich/Lance Data")]
public class LanceData : ScriptableObject
{
    [SerializeField]
    private LanceLevelConfig[] _levels = new LanceLevelConfig[]
    {
        // Lv 1 — single piercing lance, slow cadence
        new LanceLevelConfig { damage =  30f, fireRate = 0.7f, lanceCount = 1, lanceSpread =  0f },
        // Lv 2 — heavier damage, slightly faster
        new LanceLevelConfig { damage =  50f, fireRate = 0.9f, lanceCount = 1, lanceSpread =  0f },
        // Lv 3 — deeper tissue reach (more damage)
        new LanceLevelConfig { damage =  75f, fireRate = 1.1f, lanceCount = 1, lanceSpread =  0f },
        // Lv 4 — high damage, near-reliable cadence
        new LanceLevelConfig { damage = 100f, fireRate = 1.3f, lanceCount = 1, lanceSpread =  0f },
        // Lv 5 — max: trident volley — three lances in a 20-degree spread
        new LanceLevelConfig { damage = 130f, fireRate = 1.5f, lanceCount = 3, lanceSpread = 20f },
    };

    public int Length => _levels.Length;

    /// <summary>Returns the config for the given 1-based level, clamped to valid range.</summary>
    public LanceLevelConfig GetLevel(int level) =>
        _levels[Mathf.Clamp(level - 1, 0, _levels.Length - 1)];
}
