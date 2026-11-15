using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-level mission brief. Holds the level frozen behind a panel showing this
/// level's brief image until the player presses Start, then fades the panel away
/// and hands over to normal play.
///
/// Distinct from MissionBrief, which lives in the MainMenu scene and LOADS a
/// scene when its Start button is pressed. This one is already in the gameplay
/// scene and simply un-pauses it.
///
/// Freezing with Time.timeScale = 0 is what holds everything else off, rather
/// than each system needing its own "wait for the brief" check: the run timer,
/// the urine hazard, enemy movement and the level timer all advance on
/// Time.deltaTime, so they're all still at their starting state when the player
/// presses Start.
///
/// SETUP:
///   1. Build the panel under the Canvas with the brief Image and a Start button.
///   2. Attach this to the panel, leave it ACTIVE in the scene (it shows at
///      level start by design).
///   3. Assign _startButton, and _canvasGroup if you want the fade-out.
/// </summary>
public class LevelBriefPanel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The panel to hide once Start is pressed. Defaults to this GameObject.")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _startButton;
    [Tooltip("Optional — used to fade the panel out. Without one it just disappears.")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Behaviour")]
    [Tooltip("Freeze the level until Start is pressed.")]
    [SerializeField] private bool _pauseUntilStarted = true;
    [SerializeField] private float _fadeOutDuration = 0.25f;

    private bool _started;

    private void Awake()
    {
        if (_panelRoot == null) _panelRoot = gameObject;

        _panelRoot.SetActive(true);

        if (_canvasGroup != null) _canvasGroup.alpha = 1f;

        if (_startButton != null)
        {
            WarnAboutRivalWiring();
            _startButton.onClick.AddListener(OnStartClicked);
        }
        else Debug.LogError($"[LevelBriefPanel] {name}: _startButton is unassigned — " +
                            "the level would stay frozen with no way to dismiss the brief.", this);
    }

    /// <summary>
    /// Anything else driving this button fires alongside this script rather than
    /// instead of it, and the results tend to be confusing rather than obviously
    /// broken — a duplicated MissionBrief, for instance, reloads the scene on
    /// click AND resets timeScale in OnEnable, so the brief looks like it
    /// "restarts the level". Neither can be cleared from here: onClick's
    /// RemoveAllListeners() only drops runtime listeners, never the persistent
    /// calls configured in the Inspector. So name them and let the author decide.
    /// </summary>
    private void WarnAboutRivalWiring()
    {
        int persistentCalls = _startButton.onClick.GetPersistentEventCount();
        for (int i = 0; i < persistentCalls; i++)
        {
            Object target = _startButton.onClick.GetPersistentTarget(i);
            Debug.LogWarning($"[LevelBriefPanel] {name}: the Start button has an Inspector-wired OnClick → " +
                             $"{(target != null ? target.GetType().Name : "<missing>")}." +
                             $"{_startButton.onClick.GetPersistentMethodName(i)}(). It will fire in addition " +
                             "to dismissing the brief — remove it unless that's intended.", this);
        }

        if (GetComponent<MissionBrief>() != null)
            Debug.LogWarning($"[LevelBriefPanel] {name}: a MissionBrief component is on this same panel. " +
                             "That one belongs to the MainMenu — it loads a scene on Start and forces " +
                             "timeScale back to 1 in OnEnable, which reloads this level and cancels the " +
                             "brief's pause. Remove it.", this);
    }

    private void OnEnable()
    {
        // Set here rather than in Awake so re-showing the panel re-freezes the
        // level, and so it wins against anything that reset timeScale during the
        // scene transition into this level.
        if (_pauseUntilStarted && !_started) Time.timeScale = 0f;
    }

    private void OnStartClicked()
    {
        if (_started) return;
        _started = true;

        if (_startButton != null) _startButton.interactable = false;

        StartCoroutine(DismissRoutine());
    }

    private IEnumerator DismissRoutine()
    {
        // Unscaled: timeScale is 0 right now, so a scaled fade would never advance.
        if (_canvasGroup != null && _fadeOutDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(1f - elapsed / _fadeOutDuration);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
        }

        // Unfreeze BEFORE hiding: deactivating this GameObject kills the
        // coroutine, so anything left after that point may never run.
        if (_pauseUntilStarted) Time.timeScale = 1f;

        _panelRoot.SetActive(false);
    }
}
