using TMPro;
using UnityEngine;

/// <summary>
/// HUD label showing how many of this level's objectives the player has taken
/// down, in "done/total" form — nests in Level 1 (<see cref="LevelProgression.points"/>)
/// and chambers in Level 2 (<see cref="LevelProgression.chambers"/>). It reads
/// 0/4 at level start and climbs to 4/4, so it's a progress count rather than a
/// countdown.
///
/// The same script covers both levels: whichever array LevelProgression has
/// populated is the one counted, so the panel can be dropped into either scene
/// unchanged. If a scene somehow uses both, nests and chambers are shown side
/// by side, separated by <see cref="_separator"/>.
///
/// A nest counts as destroyed once its GameObject is gone — EnemyNest destroys
/// itself the moment its health hits 0, which nulls its slot in the points array.
/// A chamber counts once <see cref="Chamber.IsCleared"/> is true, i.e. its whole
/// wave is dead.
///
/// SETUP:
///   1. Put this on the panel's TextMeshProUGUI GameObject (or any object, with
///      _text assigned to that label).
///   2. Leave _levelProgression empty to auto-find the scene's LevelProgression,
///      or assign it directly.
///   3. Tweak _nestPrefix / _chamberPrefix — set them to "" for a bare "0/4".
/// </summary>
public class ObjectiveCounter : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Label to write into. Left empty = the TextMeshProUGUI on this GameObject.")]
    [SerializeField] private TextMeshProUGUI _text;
    [Tooltip("Scene's LevelProgression. Left empty = found automatically at Start.")]
    [SerializeField] private LevelProgression _levelProgression;

    [Header("Format")]
    [Tooltip("Prefix for the nest count (Level 1). Empty = just the numbers.")]
    [SerializeField] private string _nestPrefix = "Nests: ";
    [Tooltip("Prefix for the chamber count (Level 2). Empty = just the numbers.")]
    [SerializeField] private string _chamberPrefix = "Chambers: ";
    [Tooltip("Only used if a scene has both nests and chambers.")]
    [SerializeField] private string _separator = "   ";
    [Tooltip("Hide the label once every objective is done.")]
    [SerializeField] private bool _hideWhenCleared = false;

    // Totals are taken once at Start: nest slots go null as they're destroyed,
    // so the array can't be re-counted for the denominator later.
    private int _totalNests;
    private int _totalChambers;

    // Last values written, so the label is only rebuilt when a count changes.
    // -1 rather than 0, because 0 is the real starting value now.
    private int _shownNests    = -1;
    private int _shownChambers = -1;

    private void Start()
    {
        if (_text == null) _text = GetComponent<TextMeshProUGUI>();
        if (_levelProgression == null) _levelProgression = FindFirstObjectByType<LevelProgression>();

        if (_text == null)
        {
            Debug.LogWarning("[ObjectiveCounter] No TextMeshProUGUI assigned or found — disabling.");
            enabled = false;
            return;
        }

        if (_levelProgression == null)
        {
            Debug.LogWarning("[ObjectiveCounter] No LevelProgression in this scene — disabling.");
            enabled = false;
            return;
        }

        _totalNests    = CountNonNullNests();
        _totalChambers = CountNonNullChambers();

        Refresh();
    }

    private void Update() => Refresh();

    /// <summary>
    /// Recomputes both counts and rewrites the label, but only when something
    /// actually changed — this runs every frame.
    /// </summary>
    private void Refresh()
    {
        int nestsDone    = _totalNests    - CountNestsLeft();
        int chambersDone = _totalChambers - CountChambersLeft();

        if (nestsDone == _shownNests && chambersDone == _shownChambers) return;

        _shownNests    = nestsDone;
        _shownChambers = chambersDone;

        string label = BuildLabel(nestsDone, chambersDone);

        if (_hideWhenCleared && nestsDone == _totalNests && chambersDone == _totalChambers)
        {
            _text.gameObject.SetActive(false);
            return;
        }

        _text.SetText(label);
    }

    private string BuildLabel(int nestsDone, int chambersDone)
    {
        bool hasNests    = _totalNests    > 0;
        bool hasChambers = _totalChambers > 0;

        string nests    = $"{_nestPrefix}{nestsDone}/{_totalNests}";
        string chambers = $"{_chamberPrefix}{chambersDone}/{_totalChambers}";

        if (hasNests && hasChambers) return $"{nests}{_separator}{chambers}";
        if (hasChambers)             return chambers;
        return nests;   // nest-only, and the fallback when neither array is filled ("0/0")
    }

    // ── Counting ──────────────────────────────────────────────────────────────

    private int CountNonNullNests()
    {
        var points = _levelProgression.points;
        if (points == null) return 0;

        int count = 0;
        foreach (var nest in points)
            if (nest != null) count++;

        return count;
    }

    private int CountNonNullChambers()
    {
        var chambers = _levelProgression.chambers;
        if (chambers == null) return 0;

        int count = 0;
        foreach (var chamber in chambers)
            if (chamber != null) count++;

        return count;
    }

    /// <summary>
    /// Nests still standing — subtracted from the total to get the destroyed
    /// count. A destroyed nest calls Destroy(gameObject) on itself, so its array
    /// slot compares equal to null from then on.
    /// </summary>
    private int CountNestsLeft() => CountNonNullNests();

    /// <summary>Chambers whose wave hasn't been wiped out yet.</summary>
    private int CountChambersLeft()
    {
        var chambers = _levelProgression.chambers;
        if (chambers == null) return 0;

        int left = 0;
        foreach (var chamber in chambers)
            if (chamber != null && !chamber.IsCleared) left++;

        return left;
    }
}
