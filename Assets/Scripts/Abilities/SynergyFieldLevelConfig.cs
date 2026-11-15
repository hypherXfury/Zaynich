using System;

/// <summary>Per-level stats for the Synergy Field ability.</summary>
[Serializable]
public struct SynergyFieldLevelConfig
{
    /// <summary>World-space radius of the damaging aura.</summary>
    public float radius;

    /// <summary>Damage dealt to enemies per second while inside the field.</summary>
    public float damagePerSecond;

    /// <summary>Seconds between damage ticks. Smaller values = smoother DPS but more frequent checks.</summary>
    public float tickInterval;
}
