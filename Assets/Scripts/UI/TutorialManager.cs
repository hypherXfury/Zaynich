using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the Archero-style first-launch tutorial as one continuous sequence:
///
///   Step 1 — Movement : action card; dismisses once the player has actually
///            walked _moveDistanceToDismiss world units.
///   Step 2 — First shot: one enemy spawns, then an action card explains that
///            releasing the joystick auto-aims and shoots. The card dismisses on
///            the first bolt actually leaving the barrel — which can only happen
///            while standing still with an enemy in range, so it's proof the rule
///            landed. The step then waits for that enemy to die.
///   Step 3 — Oxygen   : action card; dismisses when the drop is collected.
///   Step 4 — Pulse    : unlocks the pulse ability + button, spawns a single
///            shielded enemy, and shows an action card that dismisses the moment
///            the player fires the pulse. Waits for the enemy to die (which
///            requires the shield broken first, since Enemy.TakeDamage blocks all
///            damage while shielded).
///   Step 5 — Wave     : info card to brace the player, then a wave of enemies
///            around them; waits until all are dead. Each enemy already drops
///            XP/Oxygen via Enemy.Die() -> UtillitySpawner.
///   Step 6 — Complete : info cards for "tutorial complete" and "follow the guide
///            arrow", then hands control to the player. There is no upgrade-panel
///            wait here — it can still pop up on its own via PlayerProgression.
///
/// CARDS DO NOT PAUSE THE GAME. Info cards (complete, follow-arrow, nest and
/// chamber intros) carry a Next button and swallow taps; action cards have no
/// button, let input through to the joystick, and dismiss themselves when the
/// player does the thing being taught. If an action card goes unanswered for
/// _actionNudgeDelay seconds it starts pulsing its glow rather than sitting there
/// silently. Nothing in the main sequence shows a card while an enemy is already
/// on the player — every spawn happens with its card, not behind it.
///
/// Beyond the sequence there are three one-shot intros, each keyed separately so
/// they never replay:
///
///   NEST (Level 1)    — ShowNest1Popup, called by LevelProgression the first
///     time nest 1 is on screen: the nest's spawning is held, the camera pans off
///     the player and onto the nest (the player doesn't move), the nest blinks
///     behind the card, and Next pans back and resumes spawning.
///   CHAMBER (Level 2) — the same treatment for whichever chamber the player
///     first lays eyes on, via Chamber.OnFirstVisible.
///   BLOOD SURGE       — the first surge the player is actually subject to shows
///     a card for _surgeCardSeconds while the push is happening; it's an action-
///     style card so they can feel the current as they read.
///
/// First-launch detection uses PlayerPrefs key "TutorialDone"; the three intros
/// use "Nest1PopupDone", "ChamberIntroDone" and "BloodSurgeIntroDone".
/// The Zidebactam Pulse button/ability unlocks in Step 4 only — returning players
/// (who skip the tutorial) get it via EnableAllGameplay().
///
/// This component is also worth putting in Level 2, with only _tooltip,
/// _cameraController, _imageChamberIntro and _imageBloodSurge assigned: every
/// other reference is optional, the tutorial itself won't run (the key is set),
/// and its Start() is what re-enables blood flow when a player Continues straight
/// into that scene.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    /// <summary>
    /// True once gameplay is fully enabled — either the tutorial just finished,
    /// or this is a returning player who skips it. LevelProgression waits on
    /// this before activating the first enemy nest.
    /// </summary>
    public bool IsComplete { get; private set; }

    private const string TUTORIAL_DONE_KEY = "TutorialDone";
    private const string NEST1_POPUP_KEY   = "Nest1PopupDone";
    private const string CHAMBER_POPUP_KEY = "ChamberIntroDone";
    private const string SURGE_POPUP_KEY   = "BloodSurgeIntroDone";


    [Header("Player")]
    [SerializeField] private GameObject _joystickGO;
    [SerializeField] private CefepiimeBolt _cefepiimeBolt;
    [SerializeField] private ZidebactamPulse _zidebactamPulse;
    [SerializeField] private GameObject _pulseButton;
    [SerializeField] private Transform _player;

    [Header("Enemy Spawning")]
    [SerializeField] private Enemy _basicEnemyPrefab;
    [SerializeField] private EnemyData _basicEnemyData;
    [SerializeField] private float _tutorialEnemySpawnOffset = 5f;

    [Header("Zidebactam Pulse Tutorial (Step 4)")]
    [Tooltip("A shielded enemy — its EnemyData must have hasShield enabled, e.g. KPCKlebsiella_Data.")]
    [SerializeField] private Enemy _shieldedEnemyPrefab;
    [SerializeField] private EnemyData _shieldedEnemyData;

    [Header("Wave (Step 5)")]
    [SerializeField] private int _waveEnemyCount = 10;
    [SerializeField] private float _waveSpawnRadius = 6f;
    [SerializeField] private float _waveSpawnInterval = 0.4f;

    [Header("Tutorial Text")]
    [SerializeField] private TutorialTooltip _tooltip;

    [Header("Sequence")]
    [Tooltip("Scene the step-by-step tutorial belongs to. Anywhere else this component skips the " +
             "sequence and only does its other jobs — the chamber/blood-surge intros and switching " +
             "gameplay on. That matters because the tutorial's 'has it run yet' flag is global, so a " +
             "player who opens Level 2 without ever finishing Level 1 would otherwise get Step 1 " +
             "there. Leave empty to let the sequence run in whatever scene this sits in.")]
    [SerializeField] private string _tutorialSceneName = "Level 1";

    [Header("Card Behaviour")]
    [Tooltip("World units the player must travel before the Step 1 movement card dismisses itself.")]
    [SerializeField] private float _moveDistanceToDismiss = 2.5f;
    [Tooltip("Seconds an action card waits for the player before it starts pulsing its glow as a nudge.")]
    [SerializeField] private float _actionNudgeDelay = 8f;
    [Tooltip("Shortest time an action card can stay on screen. Stops a card the player has already " +
             "satisfied — e.g. they were standing still when the shooting card appeared — from " +
             "flashing past before it's been read.")]
    [SerializeField] private float _minActionCardSeconds = 1.5f;
    [Tooltip("Seconds the blood-surge card stays up. It has no Next button so the player can keep " +
             "moving — and feel the current — while reading.")]
    [SerializeField] private float _surgeCardSeconds = 4.5f;

    [Header("Tutorial Images")]
    [Tooltip("Step 1 — Movement")]
    [SerializeField] private Sprite _imageMovement;
    [Tooltip("Step 2 — First shot")]
    [SerializeField] private Sprite _imageFirstShot;
    [Tooltip("Step 3 — Oxygen pickup")]
    [SerializeField] private Sprite _imageOxygen;
    [Tooltip("Step 4 — Zidebactam Pulse introduction")]
    [SerializeField] private Sprite _imagePulseIntro;
    [Tooltip("Step 5 — Incoming wave")]
    [SerializeField] private Sprite _imageWave;
    [Tooltip("Step 7 — Tutorial complete")]
    [SerializeField] private Sprite _imageComplete;
    [Tooltip("Step 7 — Follow the guide arrow (shown right before the arrow is enabled)")]
    [SerializeField] private Sprite _imageFollowArrow;
    [Tooltip("Nest 1 — shown the first time nest 1 comes on screen, while the camera holds on it.")]
    [SerializeField] private Sprite _imageNest1;
    [Tooltip("Chamber — shown the first time any Level 2 chamber comes on screen, while the camera holds on it.")]
    [SerializeField] private Sprite _imageChamberIntro;
    [Tooltip("Blood surge — shown during the first surge the player is actually pushed by.")]
    [SerializeField] private Sprite _imageBloodSurge;

    [Header("Intro Blink")]
    [Tooltip("Blink speed of the subject (nest 1, or the first chamber) while its intro card is up.")]
    [SerializeField] private float _nest1BlinkSpeed = 3f;

    [Header("Camera")]
    [Tooltip("Used to pan onto a nest/chamber for its intro card. Left empty = found automatically.")]
    [SerializeField] private CameraController _cameraController;

    [Header("Guide Arrow")]
    [Tooltip("Compass guide arrow — disabled during the tutorial, enabled at the very end.")]
    [SerializeField] private WayPoints _wayPoints;

    // ── State ─────────────────────────────────────────────────────────────────

    private int _waveRemaining;
    private GameObject _tutorialOxygen;
    private bool _introRunning;   // a camera-pan intro is on screen right now

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (_cameraController == null) _cameraController = FindFirstObjectByType<CameraController>();

        // Both intros live outside the main sequence and are wanted on every
        // launch, not just the first, so they're wired up before the branch.
        SubscribeChamberIntro();
        SubscribeBloodSurgeIntro();

        if (!PlayerPrefs.HasKey(TUTORIAL_DONE_KEY) && IsTutorialScene())
        {
            StartCoroutine(RunTutorial());
        }
        else
        {
            // Returning player, or a scene the sequence doesn't belong to —
            // everything on. IsComplete is set without marking the tutorial done,
            // so a player who jumped straight here still gets the real tutorial
            // when they do reach its own level.
            EnableAllGameplay();
            if (_wayPoints != null) _wayPoints.EnableGuiding();
            IsComplete = true;
        }
    }

    /// <summary>
    /// True when the step-by-step sequence is allowed to run here. The "tutorial
    /// done" flag is global rather than per-scene, so without this check a
    /// TutorialManager placed in Level 2 (for the chamber intro) would start
    /// Step 1 there for anyone who hasn't finished Level 1 — including anyone
    /// opening Level 2 directly in the editor.
    /// </summary>
    private bool IsTutorialScene()
    {
        if (string.IsNullOrEmpty(_tutorialSceneName)) return true;

        if (SceneManager.GetActiveScene().name == _tutorialSceneName) return true;

        Debug.Log($"[TutorialManager] Sequence skipped — this is '{SceneManager.GetActiveScene().name}', " +
                  $"not '{_tutorialSceneName}'. Intros and gameplay enabling still run.");
        return false;
    }

    private void OnDestroy()
    {
        if (BloodFlowManager.Instance != null)
            BloodFlowManager.Instance.OnSurgeStart -= HandleSurgeStart;

        UnsubscribeChamberIntro();

        if (Instance == this) Instance = null;
    }

    // ── Enable helpers ────────────────────────────────────────────────────────

    private void DisableAllGameplay()
    {
        if (_cefepiimeBolt != null) _cefepiimeBolt.enabled = false;
        if (_zidebactamPulse != null) _zidebactamPulse.enabled = false;
        if (_pulseButton != null) _pulseButton.SetActive(false);

        // Keep blood flow off until the tutorial completes.
        BloodFlowReceiver.SetGameplayEnabled(false);
    }

    private void EnableAllGameplay()
    {
        if (_cefepiimeBolt != null) _cefepiimeBolt.enabled = true;
        if (_zidebactamPulse != null) _zidebactamPulse.enabled = true;

        // Zidebactam Pulse unlocks during Step 4 for first-time players. For
        // returning players (who skip RunTutorial entirely), this is the only
        // place left that unlocks it — LevelProgression no longer does.
        if (_pulseButton != null) _pulseButton.SetActive(true);

        // Tutorial done (or returning player) — turn blood flow on.
        BloodFlowReceiver.SetGameplayEnabled(true);

        // The surge clock has been running since scene start, regardless of the
        // tutorial. Restart it so the player's first surge is a full interval
        // away instead of landing on the heels of the last card — or having
        // already fired, unfelt, while every receiver was still disabled.
        BloodFlowManager.Instance?.ResetSurgeTimer();
    }

    private void MarkTutorialDone()
    {
        PlayerPrefs.SetInt(TUTORIAL_DONE_KEY, 1);
        PlayerPrefs.Save();

    }

    private static bool AlreadyShown(string key) => PlayerPrefs.HasKey(key);

    private static void MarkShown(string key)
    {
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }

    private void ShowImage(Sprite image, bool showNext) =>
        _tooltip?.ShowImage(image, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), showNext);

    private void HideText() => _tooltip?.Hide();

    // ── Card helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Shows an info card — Next button, taps blocked behind it — and holds until
    /// the player taps Next. The game keeps running underneath: these are only
    /// used at moments when nothing is attacking the player.
    /// </summary>
    private IEnumerator ShowInfoCardAndWait(Sprite image)
    {
        if (image == null || _tooltip == null) yield break;

        ShowImage(image, showNext: true);

        yield return _tooltip.WaitForNext();

        HideText();
    }

    /// <summary>
    /// Shows an action card — no Next button, input passes straight through to
    /// the joystick and ability buttons — and holds until <paramref name="done"/>
    /// returns true. After _actionNudgeDelay with no progress the card's glow
    /// starts pulsing, so a stuck player gets a hint instead of silence.
    ///
    /// <paramref name="onShown"/> runs the frame the card goes up, and is where a
    /// step arms whatever it's about to measure — enabling the ability, clearing
    /// a "did it happen" flag. Anything armed earlier could be satisfied before
    /// the player has seen the card: releasing the joystick a moment too early
    /// would fire a bolt, and the shooting card would then appear pre-completed.
    ///
    /// The card also stays up for at least _minActionCardSeconds, so even an
    /// instantly-satisfied condition can't make it flash past unread.
    /// </summary>
    private IEnumerator ShowActionCardUntil(Sprite image, Func<bool> done, Action onShown = null)
    {
        if (done == null) yield break;

        if (image == null || _tooltip == null)
        {
            onShown?.Invoke();
            yield return new WaitUntil(done);
            yield break;
        }

        ShowImage(image, showNext: false);
        onShown?.Invoke();

        float waited  = 0f;
        bool  nudging = false;

        while (!done() || waited < _minActionCardSeconds)
        {
            waited += Time.deltaTime;

            if (!nudging && waited >= _actionNudgeDelay)
            {
                nudging = true;
                _tooltip.SetGlow(true);
            }
            yield return null;
        }

        if (nudging) _tooltip.SetGlow(false);
        HideText();
    }

    /// <summary>
    /// Shows a card with no Next button for a fixed time — used for the blood
    /// surge, where the lesson is the push the player is feeling right now, so
    /// stopping to tap a button would defeat the point.
    /// </summary>
    private IEnumerator ShowCardForSeconds(Sprite image, float seconds)
    {
        if (image == null || _tooltip == null) yield break;

        ShowImage(image, showNext: false);
        yield return new WaitForSeconds(seconds);
        HideText();
    }

    // ── Camera-pan intros (nest / chamber) ────────────────────────────────────

    /// <summary>
    /// Pans the camera off the player and onto <paramref name="subject"/>, shows
    /// an info card, and pans back when the player taps Next. The player is never
    /// moved — they stay exactly where they were standing.
    ///
    /// Unlike the sequence's cards, these two DO pause the game: the camera is
    /// looking away from the player, so leaving the world running would let
    /// enemies chew on them off-screen while they read. The pan is unaffected —
    /// CameraController drives a focus move on unscaled time precisely so it can
    /// keep working through the freeze (as does the nest's blink).
    ///
    /// With no camera available this degrades to just showing the card, which is
    /// still the old behaviour rather than nothing.
    /// </summary>
    private IEnumerator FocusIntroRoutine(Transform subject, Sprite card)
    {
        _introRunning = true;

        // Remember rather than assume 1: something else (an enemy card) could
        // already own the pause, and clobbering its value would resume a game it
        // still wants frozen.
        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (_cameraController != null && subject != null)
        {
            _cameraController.FocusOn(subject);
            yield return new WaitUntil(() => _cameraController == null || _cameraController.FocusSettled);
        }

        yield return ShowInfoCardAndWait(card);

        if (_cameraController != null)
        {
            _cameraController.ReturnToPlayer();
            yield return new WaitUntil(() => _cameraController == null || _cameraController.FocusSettled);
        }

        Time.timeScale = previousTimeScale;
        _introRunning = false;
    }

    /// <summary>
    /// Blinks nest 1, holds its spawning, pans the camera onto it and shows its
    /// explanation card. Called by LevelProgression the first time nest 1 enters
    /// the camera bounds. Shown once ever — returning players get none of it.
    /// </summary>
    public void ShowNest1Popup(EnemyNest nest)
    {
        if (_imageNest1 == null) return;
        if (AlreadyShown(NEST1_POPUP_KEY)) return;
        StartCoroutine(Nest1PopupRoutine(nest));
    }

    private IEnumerator Nest1PopupRoutine(EnemyNest nest)
    {
        // The nest is already active — that's what brought it on screen. Hold its
        // spawning while the camera is away so the player doesn't pan back into a
        // crowd that appeared behind them.
        nest?.SetSpawningPaused(true);
        nest?.StartBlink(_nest1BlinkSpeed);

        yield return FocusIntroRoutine(nest != null ? nest.transform : null, _imageNest1);

        nest?.StopBlink();
        nest?.SetSpawningPaused(false);

        MarkShown(NEST1_POPUP_KEY);
    }

    // ── Chamber intro (Level 2) ───────────────────────────────────────────────

    private Chamber[] _sceneChambers;

    /// <summary>
    /// Listens to every chamber in the scene for whichever one the player sees
    /// first. Level 1 has no chambers, so this is a no-op there.
    /// </summary>
    private void SubscribeChamberIntro()
    {
        if (_imageChamberIntro == null || AlreadyShown(CHAMBER_POPUP_KEY)) return;

        _sceneChambers = FindObjectsByType<Chamber>(FindObjectsSortMode.None);
        foreach (Chamber chamber in _sceneChambers)
            if (chamber != null) chamber.OnFirstVisible += HandleChamberFirstVisible;
    }

    private void UnsubscribeChamberIntro()
    {
        if (_sceneChambers == null) return;

        foreach (Chamber chamber in _sceneChambers)
            if (chamber != null) chamber.OnFirstVisible -= HandleChamberFirstVisible;

        _sceneChambers = null;
    }

    private void HandleChamberFirstVisible(Chamber chamber)
    {
        // Only the first chamber seen gets the intro; drop every subscription so
        // the other three pass by quietly.
        UnsubscribeChamberIntro();

        if (AlreadyShown(CHAMBER_POPUP_KEY)) return;
        StartCoroutine(ChamberIntroRoutine(chamber));
    }

    private IEnumerator ChamberIntroRoutine(Chamber chamber)
    {
        chamber?.StartBlink(_nest1BlinkSpeed);

        yield return FocusIntroRoutine(chamber != null ? chamber.transform : null, _imageChamberIntro);

        chamber?.StopBlink();

        MarkShown(CHAMBER_POPUP_KEY);
    }

    // ── Blood surge intro ─────────────────────────────────────────────────────

    private void SubscribeBloodSurgeIntro()
    {
        if (_imageBloodSurge == null || AlreadyShown(SURGE_POPUP_KEY)) return;

        if (BloodFlowManager.Instance == null)
        {
            Debug.LogWarning("[TutorialManager] No BloodFlowManager in the scene — the blood-surge card can't be shown.");
            return;
        }

        BloodFlowManager.Instance.OnSurgeStart += HandleSurgeStart;
    }

    /// <summary>
    /// First surge the player is actually subject to. Surges that fire while the
    /// tutorial is still running are ignored: receivers are disabled until it
    /// completes, so nothing would be pushed and the lesson would be spent on a
    /// surge the player never felt.
    /// </summary>
    private void HandleSurgeStart()
    {
        if (!IsComplete || _introRunning) return;
        if (AlreadyShown(SURGE_POPUP_KEY)) return;

        BloodFlowManager.Instance.OnSurgeStart -= HandleSurgeStart;
        StartCoroutine(BloodSurgeIntroRoutine());
    }

    private IEnumerator BloodSurgeIntroRoutine()
    {
        yield return ShowCardForSeconds(_imageBloodSurge, _surgeCardSeconds);

        MarkShown(SURGE_POPUP_KEY);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Main sequence
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator RunTutorial()
    {
        DisableAllGameplay();

        yield return StartCoroutine(Step1_Movement());
        yield return StartCoroutine(Step2_FirstEnemy());
        yield return new WaitForSeconds(.25f);  // give the player a moment to breathe
        yield return StartCoroutine(Step3_Oxygen());
        yield return StartCoroutine(Step4_PulseTutorial());
        yield return StartCoroutine(Step5_Wave());
        yield return StartCoroutine(Step6_Complete());

        // No upgrade-panel wait here — it can still pop up and pause the game
        // on its own via PlayerProgression, just not gated by the tutorial.
        EnableAllGameplay();
        MarkTutorialDone();
        IsComplete = true;
        if (_wayPoints != null) _wayPoints.EnableGuiding();
    }

    // ── Step 1 — Movement ─────────────────────────────────────────────────────

    /// <summary>
    /// The card comes down once the player has covered real ground, not on the
    /// first twitch of the stick — walking somewhere is the lesson.
    /// </summary>
    private IEnumerator Step1_Movement()
    {
        // No player transform to measure against — fall back to the joystick
        // being pushed, and failing that to a plain timed card.
        if (_player == null)
        {
            Joystick joystick = _joystickGO != null ? _joystickGO.GetComponent<Joystick>() : null;

            if (joystick != null)
                yield return ShowActionCardUntil(_imageMovement, () => joystick.Direction.magnitude > 0.3f);
            else
                yield return ShowCardForSeconds(_imageMovement, 3f);

            yield break;
        }

        // Measured from where the player stands when the card appears, for the
        // same reason Steps 2 and 4 arm themselves there: distance covered
        // beforehand isn't distance covered in response to the card.
        Vector3 start = _player.position;
        yield return ShowActionCardUntil(
            _imageMovement,
            () => Vector3.Distance(_player.position, start) >= _moveDistanceToDismiss,
            onShown: () => start = _player.position);
    }

    // ── Step 2 — First enemy / first shot ─────────────────────────────────────

    private IEnumerator Step2_FirstEnemy()
    {
        yield return new WaitForSeconds(1f);

        // The enemy spawns BEFORE the card so the player has something to shoot
        // while the instruction is still on screen.
        bool    enemyDead = false;
        Vector3 deathPos  = Vector3.zero;

        Enemy enemy = SpawnTutorialEnemy(_basicEnemyPrefab, _basicEnemyData);
        if (enemy != null)
            enemy.OnDeath += e => { deathPos = e.transform.position; enemyDead = true; };

        // Firing only happens while idle with an enemy in range (CefepiimeBolt
        // .SetCanShoot), so the first bolt is proof the player actually stopped
        // to shoot — a far better dismissal cue than the enemy dying somehow.
        bool hasFired = false;
        Action onFired = () => hasFired = true;
        if (_cefepiimeBolt != null) _cefepiimeBolt.OnBoltFired += onFired;

        // The bolt is armed as the card appears, not before. A player who lets go
        // of the joystick while walking up to the enemy would otherwise fire on
        // the spot, and the card explaining that mechanic would arrive already
        // satisfied — the lesson gone before it was shown.
        yield return ShowActionCardUntil(
            _imageFirstShot,
            () => hasFired || enemyDead,
            onShown: () =>
            {
                hasFired = false;
                if (_cefepiimeBolt != null) _cefepiimeBolt.enabled = true;
            });

        if (_cefepiimeBolt != null) _cefepiimeBolt.OnBoltFired -= onFired;

        yield return new WaitUntil(() => enemyDead || enemy == null);

        // Guarantee an oxygen drop from the tutorial enemy.
        if (UtillitySpawner.Instance != null)
            _tutorialOxygen = UtillitySpawner.Instance.SpawnOxygen(deathPos);
        else
            Debug.LogWarning("[TutorialManager] UtillitySpawner.Instance is NULL — no guaranteed oxygen drop for Step 3.");
    }

    // ── Step 3 — Oxygen pickup ────────────────────────────────────────────────

    private IEnumerator Step3_Oxygen()
    {
        // No drop to teach with — skip rather than show a card whose condition
        // ("the oxygen is gone") is already true and would flash past.
        if (_tutorialOxygen == null)
        {
            Debug.LogWarning("[TutorialManager] Step 3 skipped — no tutorial oxygen was spawned.");
            yield break;
        }

        // Collected is the real condition; the null check is only a safety net
        // for a pickup that despawns on its own, so the step can't hang.
        OxygenPickup pickup = _tutorialOxygen.GetComponent<OxygenPickup>();

        yield return ShowActionCardUntil(
            _imageOxygen,
            () => (pickup != null && pickup.Collected) || _tutorialOxygen == null);

        yield return new WaitForSeconds(1.5f);
    }

    // ── Step 4 — Zidebactam Pulse / shielded enemy ────────────────────────────

    private IEnumerator Step4_PulseTutorial()
    {
        bool enemyDead = false;
        Enemy enemy = SpawnTutorialEnemy(_shieldedEnemyPrefab, _shieldedEnemyData);
        if (enemy != null)
            enemy.OnDeath += _ => enemyDead = true;

        // The card comes down as soon as they fire the pulse — the button press
        // is the lesson. Breaking the shield and finishing the kill happens after.
        bool hasPulsed = false;
        Action onPulsed = () => hasPulsed = true;
        if (_zidebactamPulse != null) _zidebactamPulse.OnPulseFired += onPulsed;

        // Unlock the pulse ability + button as the card appears, so the button
        // can't be tapped before the player has been told what it does. (This
        // used to happen when LevelProgression's nest 3 activated; that hook is
        // gone, so this is the only unlock path for first-time players —
        // returning players get it via EnableAllGameplay.)
        yield return ShowActionCardUntil(
            _imagePulseIntro,
            () => hasPulsed || enemyDead,
            onShown: () =>
            {
                hasPulsed = false;
                if (_zidebactamPulse != null) _zidebactamPulse.enabled = true;
                if (_pulseButton != null) _pulseButton.SetActive(true);
            });

        if (_zidebactamPulse != null) _zidebactamPulse.OnPulseFired -= onPulsed;

        // The shielded enemy blocks all damage until its shield is broken
        // (Enemy.TakeDamage early-returns while HasShield is true), so its
        // death is proof the player actually used the pulse.
        yield return new WaitUntil(() => enemyDead || enemy == null);
    }

    // ── Step 5 — Wave of enemies ─────────────────────────────────────────────

    /// <summary>
    /// The one card in the sequence that keeps its Next button: it's a warning,
    /// not an instruction, and nothing is on the player yet — the wave spawns
    /// only after they've acknowledged it.
    /// </summary>
    private IEnumerator Step5_Wave()
    {
        yield return ShowInfoCardAndWait(_imageWave);

        _waveRemaining = 0;

        for (int i = 0; i < _waveEnemyCount; i++)
        {
            SpawnWaveEnemy();
            _waveRemaining++;
            yield return new WaitForSeconds(_waveSpawnInterval);
        }

        yield return new WaitUntil(() => _waveRemaining <= 0);
    }

    private void SpawnWaveEnemy()
    {
        if (_basicEnemyPrefab == null || _basicEnemyData == null || _player == null) return;

        Vector2 offset = UnityEngine.Random.insideUnitCircle.normalized * _waveSpawnRadius;
        Vector3 spawnPos = _player.position + new Vector3(offset.x, offset.y, 0f);

        Enemy enemy = Instantiate(_basicEnemyPrefab, spawnPos, Quaternion.identity);
        enemy.Initialize(_basicEnemyData, _player);
        enemy.OnDeath += _ => _waveRemaining--;
    }

    // ── Step 6 — Complete ─────────────────────────────────────────────────────

    private IEnumerator Step6_Complete()
    {
        yield return ShowInfoCardAndWait(_imageComplete);
        yield return ShowInfoCardAndWait(_imageFollowArrow);
    }

    // ── Utility — spawn tutorial enemy near player ────────────────────────────

    private Enemy SpawnTutorialEnemy(Enemy prefab, EnemyData data)
    {
        if (prefab == null || data == null || _player == null) return null;

        Vector3 spawnPos = _player.position
                         + new Vector3(_tutorialEnemySpawnOffset, 0f, 0f);

        Enemy e = Instantiate(prefab, spawnPos, Quaternion.identity);
        e.Initialize(data, _player);
        return e;
    }
}
