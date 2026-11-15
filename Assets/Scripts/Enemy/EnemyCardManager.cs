using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that queues and shows enemy intro cards.
/// Cards that have already been seen are saved to PlayerPrefs and skipped.
///
/// HOW TO TRIGGER:
///   EnemyCardManager.Instance.ShowCard(myEnemyCardData);
///
/// Call this when a new enemy type first appears — e.g. from EnemySpawner
/// when it spawns a type for the first time, or from EnemyNest on activation.
///
/// SETUP:
///   1. Add to a persistent GameManager GameObject.
///   2. Assign _cardUI (drag your EnemyCardPanel in).
///   3. Assign _levelCards for each level — the manager tracks which types
///      have been seen using PlayerPrefs.
/// </summary>
public class EnemyCardManager : MonoBehaviour
{
    public static EnemyCardManager Instance { get; private set; }

    [Header("UI Reference")]
    [SerializeField] private EnemyCardUI _cardUI;

    [Header("Cards Per Level (for auto-triggering on level load)")]
    [Tooltip("Cards to show automatically when each level starts, in order.\n" +
             "Level 1 = index 0, Level 2 = index 1, etc.")]
    [SerializeField] private LevelCardSet[] _levelCards;

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly Queue<EnemyCardData> _queue = new();
    private bool _showing;

    /// <summary>
    /// True while a card is on screen or still waiting in the queue. Lets a caller
    /// hold off on whatever comes next (Chamber waits on this before spawning its
    /// wave). Cards already seen never enter the queue, so this stays false for them.
    /// </summary>
    public bool IsBusy => _showing || _queue.Count > 0;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (_cardUI != null)
            _cardUI.OnDismissed += OnCardDismissed;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Queue an enemy card to show.
    /// If ignoreSeenState = false (default), skips cards already seen.
    /// If ignoreSeenState = true, always shows regardless of save state.
    /// </summary>
    public void ShowCard(EnemyCardData data, bool ignoreSeenState = false)
    {
        if (data == null) return;
        if (!ignoreSeenState && HasBeenSeen(data)) return;

        _queue.Enqueue(data);
        TryShowNext();
    }

    /// <summary>
    /// Show all cards for a given level index (0-based).
    /// Call this on level load. Cards already seen are automatically skipped.
    /// </summary>
    public void ShowCardsForLevel(int levelIndex)
    {
        if (_levelCards == null || levelIndex >= _levelCards.Length) return;

        foreach (var card in _levelCards[levelIndex].cards)
            ShowCard(card);
    }

    /// <summary>Reset all seen cards (useful for testing).</summary>
    public void ResetAllSeen()
    {
        if (_levelCards == null) return;
        foreach (var set in _levelCards)
            foreach (var card in set.cards)
                if (card != null) PlayerPrefs.DeleteKey(card.SaveKey);
        PlayerPrefs.Save();
    }

    // ── Queue ─────────────────────────────────────────────────────────────────

    private void TryShowNext()
    {
        if (_showing || _queue.Count == 0 || _cardUI == null) return;

        EnemyCardData next = _queue.Dequeue();
        _showing = true;
        MarkSeen(next);
        _cardUI.Show(next);
    }

    private void OnCardDismissed()
    {
        _showing = false;
        TryShowNext();   // show next in queue if any
    }

    // ── PlayerPrefs save ──────────────────────────────────────────────────────

    private static bool HasBeenSeen(EnemyCardData data)
        => PlayerPrefs.HasKey(data.SaveKey);

    private static void MarkSeen(EnemyCardData data)
    {
        PlayerPrefs.SetInt(data.SaveKey, 1);
        PlayerPrefs.Save();
    }
}

/// <summary>Groups enemy cards by level for the Inspector.</summary>
[System.Serializable]
public class LevelCardSet
{
    public string           levelName;
    public EnemyCardData[]  cards;
}