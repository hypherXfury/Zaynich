using System;

/// <summary>Per-level stats for the Zidebactam Pulse ability.</summary>
[Serializable]
public struct PulseLevelConfig
{
    /// <summary>World-space radius of the pulse wave.</summary>
    public float radius;

    /// <summary>Seconds between uses.</summary>
    public float cooldown;

    /// <summary>
    /// Damage applied after breaking a shield, and to any unshielded enemy caught in the wave.
    /// 0 means the pulse only breaks shields with no additional damage.
    /// </summary>
    public float bonusDamage;

    /// <summary>
    /// When true (level 5), the pulse instantly kills any enemy that had a shield active at the moment of impact.
    /// </summary>
    public bool oneShots;
    public int maxXP;
}
