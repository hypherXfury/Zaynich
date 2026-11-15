using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One card in the pathogen codex: which card object it drives, which pathogen
/// it reveals, and how far the player has to get before it's revealed.
/// </summary>
[System.Serializable]
public class PathogenCardEntry
{
    [Tooltip("The 'Card N' object under PathogenCards. Leave empty to use the card at this entry's " +
             "index instead, so cards only need wiring when the order differs from the hierarchy.")]
    public GameObject card;

    [Tooltip("Where the name and portrait come from — the same EnemyCardData the in-game intro card " +
             "uses, so the codex can't drift from it. Leave empty and fill the overrides below for a " +
             "pathogen that has no card data (the boss).")]
    public EnemyCardData pathogen;

    [Tooltip("Optional. Name to show instead of the EnemyCardData's.")]
    public string nameOverride;

    [Tooltip("Optional. Portrait to show instead of the EnemyCardData's.")]
    public Sprite spriteOverride;

    [Tooltip("Levels the player must have cleared for this card to unlock: 1 = after Level 1, " +
             "2 = after Level 2, 3 = after the Boss. Matches SaveData.highestClearedLevel.")]
    [Range(1, 3)] public int unlockedAfterLevel = 1;
}

/// <summary>
/// Fills in the main menu's pathogen codex (PathogenMenuUI > PathogenCards) from
/// the save file: every card the player has earned shows the unlocked background,
/// the pathogen's portrait and its name, with the padlock switched off. Cards
/// they haven't reached yet stay exactly as they're authored in the scene — the
/// locked plate, the silhouette, the padlock and the "Enemy Locked" caption.
///
/// Unlocking is driven by SaveData.highestClearedLevel, the same value that
/// drives the menu's Continue button, so clearing a level is all it takes; there
/// is no separate codex save. Every card carries its own unlockedAfterLevel, so
/// the split between levels is inspector data, not code.
///
/// SETUP:
///   1. This component sits on PathogenMenuUI. Refreshing happens in OnEnable,
///      which is exactly when the library button switches the panel on.
///   2. Assign _cardsParent (PathogenCards) and _unlockedBackground
///      (Sprites/UI_Sprites/PATHOGEN/unlocked.png).
///   3. Add one entry per card, in hierarchy order, with its EnemyCardData and
///      the level that reveals it. The boss has no EnemyCardData — give that
///      entry a nameOverride and spriteOverride instead.
///
/// Each card is expected to be laid out like the ones already in the scene:
/// children named "BG", "Locked Enemy", "LockIcon" and "Text". Anything missing
/// is skipped with a warning rather than throwing.
/// </summary>
public class PathogenCodex : MonoBehaviour
{
    [Header("Cards")]
    [Tooltip("PathogenCards — the layout group holding the Card objects.")]
    [SerializeField] private Transform _cardsParent;
    [SerializeField] private List<PathogenCardEntry> _entries = new();

    [Header("Unlocked Look")]
    [Tooltip("Background swapped in once a card is unlocked (unlocked.png). Empty = keep the locked plate.")]
    [SerializeField] private Sprite _unlockedBackground;
    [Tooltip("Tint applied to the portrait once unlocked — white shows the sprite's own colours, " +
             "which the darkened locked silhouette would otherwise keep hiding.")]
    [SerializeField] private Color _unlockedPortraitTint = Color.white;
    [Tooltip("Letterbox the portrait inside the silhouette's slot instead of stretching it to fill.")]
    [SerializeField] private bool _preservePortraitAspect = true;

    private const string BackgroundChild = "BG";
    private const string PortraitChild   = "Locked Enemy";
    private const string LockIconChild   = "LockIcon";
    private const string LabelChild      = "Text";

    /// <summary>
    /// A card's resolved widgets plus the locked look it was authored with.
    /// Snapshotting the locked state is what lets Refresh() run in either
    /// direction: New Game wipes highestClearedLevel back to 0, and the cards
    /// have to go back to padlocks rather than keep whatever the previous save
    /// revealed.
    /// </summary>
    private class CardView
    {
        public GameObject       Root;
        public Image            Background;
        public Image            Portrait;
        public GameObject       LockIcon;
        public TextMeshProUGUI  Label;

        public Sprite LockedBackground;
        public Sprite LockedPortrait;
        public Color  LockedPortraitColor;
        public bool   LockedPreserveAspect;
        public string LockedLabel;
    }

    private readonly List<CardView> _views = new();
    private bool _built;

    private void Awake() => Build();

    private void OnEnable() => Refresh();

    private void Build()
    {
        if (_built) return;
        _built = true;

        for (int i = 0; i < _entries.Count; i++)
            _views.Add(BuildView(_entries[i], i));
    }

    private CardView BuildView(PathogenCardEntry entry, int index)
    {
        GameObject card = entry.card;
        if (card == null && _cardsParent != null && index < _cardsParent.childCount)
            card = _cardsParent.GetChild(index).gameObject;

        if (card == null)
        {
            Debug.LogWarning($"[PathogenCodex] Entry {index} has no card and none could be found at " +
                             "that index under the cards parent — it will be skipped.");
            return null;
        }

        var view = new CardView
        {
            Root       = card,
            Background = FindImage(card.transform, BackgroundChild),
            Portrait   = FindImage(card.transform, PortraitChild),
            LockIcon   = card.transform.Find(LockIconChild)?.gameObject,
            Label      = card.transform.Find(LabelChild)?.GetComponent<TextMeshProUGUI>(),
        };

        // The caption is the one child likely to be renamed as the cards get
        // styled, so fall back to whatever text the card does have.
        if (view.Label == null) view.Label = card.GetComponentInChildren<TextMeshProUGUI>(true);

        if (view.Portrait == null || view.Background == null || view.LockIcon == null)
            Debug.LogWarning($"[PathogenCodex] '{card.name}' is missing one of its expected children " +
                             $"('{BackgroundChild}', '{PortraitChild}', '{LockIconChild}') — " +
                             "that part of the card won't update.");

        if (view.Background != null) view.LockedBackground = view.Background.sprite;
        if (view.Portrait != null)
        {
            view.LockedPortrait       = view.Portrait.sprite;
            view.LockedPortraitColor  = view.Portrait.color;
            view.LockedPreserveAspect = view.Portrait.preserveAspect;
        }
        if (view.Label != null) view.LockedLabel = view.Label.text;

        return view;
    }

    private static Image FindImage(Transform card, string childName)
    {
        Transform child = card.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    /// <summary>
    /// Re-reads the save and repaints every card. Public so anything that changes
    /// progress while the menu is up can push an update; otherwise OnEnable
    /// covers it, since the panel is switched off between viewings.
    /// </summary>
    public void Refresh()
    {
        Build();

        int cleared = SaveSystem.Data.highestClearedLevel;

        for (int i = 0; i < _views.Count; i++)
        {
            CardView view = _views[i];
            if (view == null || view.Root == null) continue;

            if (cleared >= _entries[i].unlockedAfterLevel) ShowUnlocked(view, _entries[i]);
            else                                           ShowLocked(view);
        }
    }

    private void ShowUnlocked(CardView view, PathogenCardEntry entry)
    {
        if (view.Background != null && _unlockedBackground != null)
            view.Background.sprite = _unlockedBackground;

        if (view.LockIcon != null) view.LockIcon.SetActive(false);

        if (view.Portrait != null)
        {
            Sprite portrait = entry.spriteOverride != null
                ? entry.spriteOverride
                : entry.pathogen != null ? entry.pathogen.enemyImage : null;

            if (portrait != null)
            {
                view.Portrait.sprite         = portrait;
                view.Portrait.color          = _unlockedPortraitTint;
                view.Portrait.preserveAspect = _preservePortraitAspect;
            }
        }

        if (view.Label != null)
        {
            string pathogenName = !string.IsNullOrWhiteSpace(entry.nameOverride)
                ? entry.nameOverride
                : entry.pathogen != null ? entry.pathogen.enemyName : null;

            if (!string.IsNullOrWhiteSpace(pathogenName)) view.Label.SetText(pathogenName);
        }
    }

    private void ShowLocked(CardView view)
    {
        if (view.Background != null) view.Background.sprite = view.LockedBackground;
        if (view.LockIcon   != null) view.LockIcon.SetActive(true);

        if (view.Portrait != null)
        {
            view.Portrait.sprite         = view.LockedPortrait;
            view.Portrait.color          = view.LockedPortraitColor;
            view.Portrait.preserveAspect = view.LockedPreserveAspect;
        }

        if (view.Label != null && view.LockedLabel != null) view.Label.SetText(view.LockedLabel);
    }
}
