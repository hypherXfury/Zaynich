using UnityEngine;

/// <summary>
/// Shared configuration for a single enemy type.
/// Create via Assets > Create > Zaynich > Enemy Data.
/// </summary>
[CreateAssetMenu(fileName = "EnemyData", menuName = "Zaynich/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Stats")]
    public float maxHealth     = 20f;
    public float moveSpeed     = 3.5f;
    public float contactDamage = 10f;

    /// <summary>Seconds between consecutive damage ticks while overlapping the player.</summary>
    public float damageInterval = 1f;

    /// <summary>
    /// Scales the shove a direct Bolt/Lance hit applies (see Enemy.ApplyKnockback).
    /// 1 = the normal push, 0 = immune. Knockback is a flat velocity rather than a
    /// physics force, so it ignores mass and would otherwise fling a boss exactly
    /// as far as the smallest enemy — Boss_Data sets this to 0 so hits can't
    /// shunt the boss off its patrol line.
    /// </summary>
    [Min(0f)]
    public float knockbackMultiplier = 1f;

    [Header("Visuals")]
    public Color color = Color.green;

    /// <summary>Uniform world-space size of the enemy sprite.</summary>
    [Range(0.1f, 3f)]
    public float size = 0.28f;

    // ── Shield ──────────────────────────────────────────────────────────────
    [Header("Shield")]
    /// <summary>Whether this enemy spawns with an active shield.</summary>
    public bool  hasShield   = false;

    /// <summary>Color of the sprite while the shield is active (single-layer fallback).</summary>
    public Color shieldColor = Color.yellow;

    [Header("Shield – Advanced")]
    /// <summary>Number of sequential shield layers. Each layer must be removed by its own pulse hits.</summary>
    [Min(1)]
    public int shieldLayers = 1;

    /// <summary>
    /// Colors per layer, outermost first.
    /// Falls back to <see cref="shieldColor"/> for any missing entries or when the array is empty.
    /// </summary>
    public Color[] shieldLayerColors = new Color[0];

    /// <summary>Number of pulse hits required to remove one shield layer. Values greater than 1 create a gradually shrinking shield.</summary>
    [Min(1)]
    public int pulsesPerLayer = 1;

    /// <summary>Number of Cefepime Bolt hits required to remove one shield layer. Lets players strip shields with normal fire, not just the pulse.</summary>
    [Min(1)]
    public int boltHitsPerLayer = 7;

    /// <summary>Seconds after the shield is fully broken before it regenerates. 0 = no regeneration.</summary>
    [Min(0f)]
    public float shieldRegenerateTime = 0f;

    /// <summary>Probability [0–1] that this enemy spawns with its shield active. 1 = always.</summary>
    [Range(0f, 1f)]
    public float shieldChance = 1f;

    // ── Rewards ──────────────────────────────────────────────────────────────
    [Header("Rewards")]
    /// <summary>Extra pills granted when this enemy is killed. 0 = standard loot only.</summary>
    [Min(0)]
    public int extraPillsOnDeath = 0;

    /// <summary>XP awarded to the Efficacy Meter (PlayerProgression) when this enemy drops an XP pickup.</summary>
    [Min(0f)]
    public float xpReward = 5f;

    // ── Vitals ───────────────────────────────────────────────────────────────
    [Header("Vitals — Infection")]
    /// <summary>
    /// How much Infection this enemy contributes when it acts, and how much it
    /// reduces Infection by when killed. Tougher/more dangerous enemies should
    /// use a higher value. 0 = falls back to VitalsManager's default value.
    /// </summary>
    [Min(0f)]
    public float infectionContribution = 1.5f;

    // ── Biofilm ──────────────────────────────────────────────────────────────
    [Header("Biofilm")]
    /// <summary>When true, a biofilm puddle is spawned at this enemy's position on creation.</summary>
    public bool       spawnsBiofilm = false;

    /// <summary>Biofilm prefab to instantiate. Only used when <see cref="spawnsBiofilm"/> is true.</summary>
    public GameObject biofilmPrefab;
}