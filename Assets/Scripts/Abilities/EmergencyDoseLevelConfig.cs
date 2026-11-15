using System;

/// <summary>Per-level stats for the Emergency Dose ability.</summary>
[Serializable]
public struct EmergencyDoseLevelConfig
{
    /// <summary>World-space radius of the blast.</summary>
    public float radius;

    /// <summary>Damage dealt to every enemy caught in the blast, applied after force-breaking their shields.</summary>
    public float damage;

    /// <summary>Seconds between uses.</summary>
    public float cooldown;
}
