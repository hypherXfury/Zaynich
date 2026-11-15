using UnityEngine;

/// <summary>
/// ScriptableObject that stores data for the enemy intro card.
/// Create via Assets > Create > Zaynich > Enemy Card Data.
/// Attach this to your existing enemy ScriptableObject or create separately.
/// </summary>
[CreateAssetMenu(fileName = "EnemyCardData", menuName = "Zaynich/Enemy Card Data")]
public class EnemyCardData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName        = "ESBL E. Coli";
    public string enemySubtitle    = "Extended-Spectrum Beta-Lactamase Producing";
    public string objective        = "Eliminate the pathogen and contain the infection.";

    [Header("Visuals")]
    public Sprite enemyImage;

    [Header("Threat Level")]
    [Tooltip("0 = Minimal, 1 = Low, 2 = Moderate, 3 = High, 4 = Extreme")]
    [Range(0, 4)]
    public int threatLevel = 2;

    public string ThreatLabelText => threatLevel switch
    {
        0 => "Minimal",
        1 => "Low",
        2 => "Moderate",
        3 => "High",
        4 => "Extreme",
        _ => "Unknown"
    };

    /// <summary>Unique key used to track whether this card has been shown before.</summary>
    public string SaveKey => $"EnemyCard_{name}";
}