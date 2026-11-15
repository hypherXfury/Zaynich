using System;

/// <summary>Per-level stats for the Cefepime Bolt ability.</summary>
[Serializable]
public struct BoltLevelConfig
{
    /// <summary>Damage dealt per hit.</summary>
    public float damage;

    /// <summary>Bolts fired per second.</summary>
    public float fireRate;

    /// <summary>Number of additional enemies the bolt passes through before despawning (0 = no pierce).</summary>
    public int piercingCount;

    /// <summary>Whether the bolt spawns split projectiles when it is destroyed.</summary>
    public bool splits;

    /// <summary>How many split bolts to spawn on destruction.</summary>
    public int splitCount;

    /// <summary>Total spread angle in degrees between the split bolts.</summary>
    public float splitAngle;
    public float XP;
    public float maxXP;
}
