using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// One instance per level scene. Tracks this scene's clear objectives — either
/// enemy nests (<see cref="points"/>, used by Level 1) or arena chambers
/// (<see cref="chambers"/>, used by Level 2), or in principle both at once.
/// Whichever array(s) are actually populated in a given scene are the ones
/// that count; an empty/unassigned array is simply ignored.
/// Once every populated objective in THIS scene is cleared, the level is
/// complete: the persistent Bloodstream Infection stat (<see cref="InfectionState"/>)
/// drops by <see cref="_infectionPerLevel"/> (100/3, i.e. one third, by default —
/// there are 3 levels: Level 1, Level 2, and the Boss level, the last of which
/// drops infection to exactly 0 when the boss dies, via Boss.cs), and a panel
/// shows this level's completion message — or, once infection has reached 0,
/// "Blood stream infection clear" instead.
///
/// Infection is a runtime-only static value that survives scene loads for the
/// current play session (see InfectionState) so it correctly carries over from
/// Level 1's scene into Level 2's, etc.
/// </summary>
public class LevelProgression : MonoBehaviour
{
    /// <summary>Scene-wide access so nest-spawned/chamber-spawned enemies can report their deaths.</summary>
    public static LevelProgression Instance { get; private set; }

    /// <summary>
    /// True once the level has been cleared (final panel / black screen shown).
    /// Used to suppress the level-up upgrade panel when the last enemy's death
    /// both levels the player up and completes the level.
    /// </summary>
    public bool IsCompleted => _levelCompleted;

    [Tooltip("Level 1-style objectives: nests that must all be destroyed. Leave empty for a chamber-based level.")]
    public EnemyNest[] points;

    [Tooltip("Level 2-style objectives: arena chambers that must all be cleared. Leave empty for a nest-based level.")]
    public Chamber[] chambers;

    public GameObject _panel;
    public TextMeshProUGUI text;

    [Header("Bloodstream Infection")]
    [Tooltip("Persistent HUD label showing the current infection percentage.")]
    [SerializeField] private TextMeshProUGUI infectionText;
    [SerializeField] private float _infectionPerLevel = 100f / 3f;

    [Header("This Level")]
    [Tooltip("Shown once every nest in this scene is destroyed — unless infection " +
             "has reached 0, in which case the final message is shown instead.")]
    [SerializeField] private string _levelCompleteMessage = "Level 1 Completed";
    [Tooltip("Image with Image Type = Filled. Its fillAmount (0-1) mirrors the infection percentage.")]
    [SerializeField] private Image _infectionFill;
    [Tooltip("Optional prefix for this level's infection label, e.g. 'Level Infection: '.")]
    [SerializeField] private string _levelInfectionPrefix = "";
    [SerializeField] private Image blackScreen;

    [Header("Completion Timer")]
    [Tooltip("Text (mm:ss) showing the WHOLE run's elapsed time (see GameTimer), not just this level's — " +
             "it resumes from wherever the previous level left off and is frozen at the final time on completion.")]
    [SerializeField] private TextMeshProUGUI _timerText;

    private const string FinalMessage = "Blood stream infection clear";

    private bool _levelCompleted;
    private int _lastLoggedRemaining = -1;
    private int _lastLoggedUnits = -1;
    private int _activeNestIndex = -1;   // index into points of the currently active nest
    private int _nestsDestroyed;         // count of nests destroyed by the player
    private int _nestCount;              // total non-null nests assigned at Start

    // ── Progressive infection tracking ─────────────────────────────────────────
    // Infection eases from _levelStartInfection down to (_levelStartInfection -
    // _infectionPerLevel) across the level. One "unit" is one nest-spawned enemy
    // killed, one nest destroyed, or one chamber-spawned enemy killed (chambers
    // have no separate "destroy" action — clearing IS the last enemy's death,
    // already counted). Tutorial enemies never register here because only
    // nest-spawned/chamber-spawned enemies report in.
    private float _levelStartInfection;
    private int   _totalUnits;
    private int   _completedUnits;

    // ── Completion timer ────────────────────────────────────────────────────────
    // The elapsed value itself lives in GameTimer so it survives the scene load
    // into the next level; only the run/stop state is per-level.
    private bool _timerRunning;

    private PlayerProgression _playerProgression;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _panel.SetActive(false);

        _playerProgression = FindFirstObjectByType<PlayerProgression>();

        // Pick the whole-run clock back up where the previous level left it —
        // the static already holds it within a session, but this also covers
        // Continue and opening this scene directly in the editor.
        GameTimer.Restore(SaveSystem.Data.elapsedSeconds);

        if (_timerText != null)
        {
            _timerText.gameObject.SetActive(true);
            _timerText.SetText(GameTimer.Formatted);
        }

        _levelStartInfection = InfectionState.Value;

        _totalUnits = 0;
        if (points != null)
        {
            foreach (var nest in points)
            {
                if (nest == null) continue;
                _totalUnits += nest.TotalEnemies + 1;   // +1 for destroying the nest itself
                _nestCount++;
                nest.OnDestroyedByPlayer += HandleNestDestroyed;
                nest.OnFullyCleared      += HandleNestFullyCleared;
            }
        }

        if (chambers != null)
        {
            foreach (var chamber in chambers)
            {
                if (chamber == null) continue;
                _totalUnits += chamber.TotalEnemies;   // no "+1" — clearing a chamber IS its last enemy's death
            }
        }

        // Sequential activation: only the first nest starts active — but not
        // until the tutorial has finished (returning players skip it, so this
        // resolves immediately for them). Each next nest activates when the
        // previous one is fully cleared.
        StartCoroutine(ActivateFirstNestAfterTutorial());

        RecomputeInfection();
    }

    private IEnumerator ActivateFirstNestAfterTutorial()
    {
        if (TutorialManager.Instance != null)
            yield return new WaitUntil(() => TutorialManager.Instance == null || TutorialManager.Instance.IsComplete);

        _timerRunning = true;   // start counting once gameplay actually begins
        ActivateNextNest();
    }

    // ── Sequential nest activation ─────────────────────────────────────────────

    /// <summary>
    /// Activates the next non-null nest in the points array. Called once at
    /// level start (activating nest 0) and again each time a nest reports
    /// OnFullyCleared (nest destroyed + all its enemies killed).
    /// </summary>
    private void ActivateNextNest()
    {
        if (points == null) return;

        while (++_activeNestIndex < points.Length)
        {
            var nest = points[_activeNestIndex];
            if (nest == null) continue;   // already destroyed or unassigned — skip

            nest.Activate();
            Debug.Log($"[LevelProgression] Activated nest {_activeNestIndex + 1}/{points.Length}: {nest.name}");

            // Nest 1 explains what a nest is: the card shows (and the nest
            // blinks) the first time the player actually has it on screen.
            if (_activeNestIndex == 0)
                nest.OnNestVisible += HandleNest1Visible;

            return;
        }

        Debug.Log("[LevelProgression] No nests left to activate.");
    }

    /// <summary>Nest 1 came on screen for the first time — show its tutorial card.</summary>
    private void HandleNest1Visible(EnemyNest nest)
    {
        nest.OnNestVisible -= HandleNest1Visible;
        TutorialManager.Instance?.ShowNest1Popup(nest);
    }

    private void HandleNestFullyCleared(EnemyNest nest)
    {
        nest.OnFullyCleared -= HandleNestFullyCleared;
        if (_levelCompleted) return;
        ActivateNextNest();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Called by EnemyNest for each nest-spawned enemy that dies.</summary>
    public void ReportNestEnemyKilled()
    {
        if (_levelCompleted) return;
        _completedUnits++;
        RecomputeInfection();
    }

    /// <summary>Called by Chamber for each chamber-spawned enemy that dies.</summary>
    public void ReportChamberEnemyKilled()
    {
        if (_levelCompleted) return;
        _completedUnits++;
        RecomputeInfection();
    }

    private void HandleNestDestroyed(EnemyNest nest)
    {
        if (_levelCompleted) return;
        _completedUnits++;
        _nestsDestroyed++;

        // Last nest of the level → "all nests cleared" alert; otherwise the
        // standard "infection reduced" alert.
        if (GameNotifier.Instance != null)
        {
            if (_nestsDestroyed >= _nestCount) GameNotifier.Instance.ShowAllNestsCleared();
            else                               GameNotifier.Instance.ShowInfectionReduced();
        }

        RecomputeInfection();
    }

    /// <summary>
    /// Maps kill progress onto the level's infection band and pushes it to the
    /// HUD text, the slider, and the VitalsManager health formula.
    /// </summary>
    private void RecomputeInfection()
    {
        float fraction = _totalUnits > 0
            ? Mathf.Clamp01((float)_completedUnits / _totalUnits)
            : 0f;

        InfectionState.Value = Mathf.Max(0f, _levelStartInfection - _infectionPerLevel * fraction);
        UpdateInfectionText();
        UpdateLevelInfectionText(fraction);
    }

    /// <summary>
    /// This level's OWN infection meter: starts at 100% and falls to 0% as this
    /// scene's nest enemies and nests are destroyed (independent of the global
    /// bloodstream infection, which only moves within this level's 25% band).
    /// </summary>
    private void UpdateLevelInfectionText(float clearedFraction)
    {
        float levelInfection = (1f - clearedFraction) * 100f;
        if (VitalsManager.Instance != null)
            VitalsManager.Instance.SetInfectionDescription(
                $"{_levelInfectionPrefix}{levelInfection:0}%");

        // Player level/XP progress is hidden, so its former fill bar + text
        // now double as this level's remaining-infection display instead.
        _playerProgression?.SetInfectionDisplay(levelInfection);
    }

    void Update()
    {
        if (_timerRunning)
        {
            GameTimer.Tick(Time.deltaTime);
            if (_timerText != null) _timerText.SetText(GameTimer.Formatted);
        }

        if (!_levelCompleted)
            CheckProgress();
    }

    void CheckProgress()
    {
        if (!AllObjectivesCleared()) return;

        // Nests/chambers are gone, but some of their spawned enemies may still
        // be alive. Every unit must be accounted for before the level counts
        // as complete.
        if (_completedUnits < _totalUnits)
        {
            if (_completedUnits != _lastLoggedUnits)
            {
                Debug.Log($"[LevelProgression] Objectives cleared, waiting on remaining enemies: {_completedUnits}/{_totalUnits} units.");
                _lastLoggedUnits = _completedUnits;
            }
            return;
        }

        Debug.Log("[LevelProgression] All objectives cleared and all their enemies killed — dropping infection.");

        _levelCompleted = true;
        _timerRunning = false;
        RevealTimer();
        DropInfection();
        SaveSystem.MarkLevelCleared(SceneManager.GetActiveScene().name);

        // Hand the running total to the next scene (and to Continue).
        SaveSystem.SetElapsedTime(GameTimer.Elapsed);

        string message = InfectionState.Value <= 0f ? FinalMessage : _levelCompleteMessage;
        ShowPanel(message);
    }

    /// <summary>
    /// True once every populated objective array (nests and/or chambers) in
    /// this scene is fully cleared. An array left empty/unassigned is simply
    /// not required — this is what lets the same script drive a nest-based
    /// level (Level 1), a chamber-based level (Level 2), or in principle both
    /// at once, without special-casing either.
    /// </summary>
    private bool AllObjectivesCleared()
    {
        bool hasNests    = points    != null && points.Length    > 0;
        bool hasChambers = chambers  != null && chambers.Length  > 0;

        if (!hasNests && !hasChambers) return false;
        if (hasNests    && !AllNestsDestroyed())  return false;
        if (hasChambers && !AllChambersCleared()) return false;

        return true;
    }

    private bool AllNestsDestroyed()
    {
        if (points == null || points.Length == 0) return false;

        int remaining = 0;
        foreach (var nest in points)
            if (nest != null) remaining++;

        if (remaining != _lastLoggedRemaining)
        {
            Debug.Log($"[LevelProgression] Nests remaining: {remaining}/{points.Length}");
            _lastLoggedRemaining = remaining;
        }

        return remaining == 0;
    }

    private bool AllChambersCleared()
    {
        if (chambers == null || chambers.Length == 0) return true;   // vacuous — this level doesn't use chambers

        foreach (var chamber in chambers)
            if (chamber != null && !chamber.IsCleared) return false;

        return true;
    }

    // ── Completion timer ────────────────────────────────────────────────────────

    private void RevealTimer()
    {
        if (_timerText == null) return;
        _timerText.gameObject.SetActive(true);   // ensure visible even if hidden elsewhere
        _timerText.SetText(GameTimer.Formatted); // freeze the running total
    }

    private void DropInfection()
    {
        // Progressive kills have already eased infection down; snap to the exact
        // target so float accumulation can't leave it a hair off (e.g. 75.0001).
        InfectionState.Value = Mathf.Max(0f, _levelStartInfection - _infectionPerLevel);
        Debug.Log($"[LevelProgression] InfectionState.Value is now {InfectionState.Value}");
        UpdateInfectionText();
        UpdateLevelInfectionText(1f);   // level fully cleared → 0%
    }

    private void UpdateInfectionText()
    {
        if (infectionText != null)
            infectionText.SetText($"Infection: {InfectionState.Value:0}%");

        if (VitalsManager.Instance == null)
        {
            Debug.LogWarning("[LevelProgression] VitalsManager.Instance is NULL — cannot push infection to slider.");
            return;
        }
        if (_infectionFill != null)
        {
            _infectionFill.fillAmount = Mathf.Clamp01(InfectionState.Value / VitalsManager.MaxValue);
        }
        VitalsManager.Instance.SetInfectionPercent(InfectionState.Value);
        Debug.Log($"[LevelProgression] Pushed {InfectionState.Value} to VitalsManager. Slider should now read that value.");
    }

    private void ShowPanel(string message)
    {
        if(SceneManager.GetActiveScene().name == "Level 2")
        {
            _panel.SetActive(true);
//            text.SetText(message);
            StartCoroutine(Delay2());
            return;
        }
        // If the last enemy's death also leveled the player up this frame, hide
        // the upgrade panel so only this completion / black screen is shown.
        _playerProgression?.SuppressUpgrade();

        _panel.SetActive(true);

        text.SetText(message);

        Time.timeScale = 0f;

        StartCoroutine(Delay());
    }
    IEnumerator Delay2()
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(4f);
        _panel.SetActive(false);
        Time.timeScale = 1f;
    }
    IEnumerator Delay()
    {
        yield return new WaitForSecondsRealtime(2f);
        Time.timeScale = 1f;
        StartCoroutine(FadeInOut());
        //_panel.SetActive(false);
    }


    private IEnumerator FadeInOut()
    {
        float elapsed = 0;
        while (elapsed < 3f)
        {
            elapsed += Time.deltaTime;
            Color _color = blackScreen.color;
            _color.a = Mathf.Clamp01(elapsed / 3f);
            blackScreen.color = _color;
            yield return null;
        }
        if(SceneManager.GetActiveScene().name == "Level 1")
        {
            SceneManager.LoadScene("Level 2");
        }
    }
}

