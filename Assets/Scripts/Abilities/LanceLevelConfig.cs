using System;

/// <summary>Per-level stats for the Tissue Penetration Lance ability.</summary>
[Serializable]
public struct LanceLevelConfig
{
    /// <summary>Damage dealt to each enemy the lance passes through.</summary>
    public float damage;

    /// <summary>Lances fired per second.</summary>
    public float fireRate;

    /// <summary>Number of parallel lances fired per shot (1 = single, 3 = trident at level 5).</summary>
    public int lanceCount;

    /// <summary>Total spread angle in degrees between parallel lances. Only relevant when <see cref="lanceCount"/> is greater than 1.</summary>
    public float lanceSpread;
}
