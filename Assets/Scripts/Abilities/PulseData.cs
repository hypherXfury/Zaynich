using UnityEngine;

/// <summary>
/// Zidebactam Pulse configuration across all five levels.
/// Create via Assets > Create > Zaynich > Pulse Data.
/// Level stats are pre-populated — tweak in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "PulseData", menuName = "Zaynich/Pulse Data")]
public class PulseData : ScriptableObject
{
    [SerializeField]
    private PulseLevelConfig[] _levels = new PulseLevelConfig[]
    {
        // Lv 1 — baseline: breaks shields, no extra damage
        new PulseLevelConfig { radius = 4.0f, cooldown = 8f, bonusDamage =  0f, oneShots = false },
        // Lv 2 — wider reach, faster cooldown
        new PulseLevelConfig { radius = 5.5f, cooldown = 6f, bonusDamage =  0f, oneShots = false },
        // Lv 3 — shield-break + damage burst
        new PulseLevelConfig { radius = 7.0f, cooldown = 5f, bonusDamage = 20f, oneShots = false },
        // Lv 4 — harder hit, faster reset
        new PulseLevelConfig { radius = 8.5f, cooldown = 4f, bonusDamage = 35f, oneShots = false },
        // Lv 5 — max: one-shots any shielded enemy + large damage burst
        new PulseLevelConfig { radius = 10f,  cooldown = 3f, bonusDamage = 50f, oneShots = true  },
    };

    public int Length => _levels.Length;

    /// <summary>Returns the config for the given 1-based level, clamped to valid range.</summary>
    public PulseLevelConfig GetLevel(int level) =>
        _levels[Mathf.Clamp(level - 1, 0, _levels.Length - 1)];
}
