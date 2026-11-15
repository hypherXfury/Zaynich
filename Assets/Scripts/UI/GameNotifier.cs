using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic on-screen notification image (top-left doctor-style alerts).
/// One instance per scene. Each notification is a full-info sprite — the text
/// is baked into the image, so this just shows the sprite for a while and hides
/// it. Call <see cref="Show"/> with any sprite; use the typed helpers for the
/// game's standard alerts.
///
/// SETUP:
///   1. Put this on a UI GameObject anchored top-left, with an Image child.
///   2. Assign _imageDisplay (the Image that shows the alert sprite).
///   3. Assign the standard alert sprites in the Inspector.
///   4. Call from gameplay code, e.g. GameNotifier.Instance.ShowInfectionReduced().
/// </summary>
public class GameNotifier : MonoBehaviour
{
    public static GameNotifier Instance { get; private set; }

    [Header("Display")]
    [Tooltip("The Image that shows the alert sprite. Its GameObject is toggled on/off.")]
    [SerializeField] private Image _imageDisplay;
    [Tooltip("Seconds the alert stays on screen before auto-hiding.")]
    [SerializeField] private float _displayDuration = 3f;

    [Header("Standard Alert Sprites")]
    [Tooltip("Shown when oxygen falls below the low-oxygen threshold.")]
    [SerializeField] private Sprite _lowOxygenSprite;
    [Tooltip("Shown each time an enemy nest is destroyed.")]
    [SerializeField] private Sprite _infectionReducedSprite;
    [Tooltip("Shown once when all enemy nests are destroyed.")]
    [SerializeField] private Sprite _allNestsClearedSprite;

    [Header("Low Oxygen")]
    [Tooltip("Oxygen (0-100) at or below which the low-oxygen alert fires.")]
    [SerializeField] private float _lowOxygenThreshold = 30f;

    private Coroutine _hideRoutine;
    private bool _lowOxygenActive;   // true while oxygen is under the threshold (debounce)

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (_imageDisplay != null) _imageDisplay.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (VitalsManager.Instance != null)
            VitalsManager.Instance.OnOxygenChanged += HandleOxygenChanged;
    }

    private void OnDestroy()
    {
        if (VitalsManager.Instance != null)
            VitalsManager.Instance.OnOxygenChanged -= HandleOxygenChanged;
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Show any alert sprite for the configured duration.</summary>
    public void Show(Sprite sprite)
    {
        if (sprite == null || _imageDisplay == null) return;

        _imageDisplay.sprite = sprite;
        _imageDisplay.gameObject.SetActive(true);

        if (_hideRoutine != null) StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(HideAfterDelay());
    }

    public void ShowLowOxygen()       => Show(_lowOxygenSprite);
    public void ShowInfectionReduced() => Show(_infectionReducedSprite);
    public void ShowAllNestsCleared()  => Show(_allNestsClearedSprite);

    // ── Low-oxygen watcher ──────────────────────────────────────────────────────

    private void HandleOxygenChanged(float oxygen)
    {
        if (oxygen <= _lowOxygenThreshold)
        {
            // Only fire once per dip below the threshold, not every frame.
            if (!_lowOxygenActive)
            {
                _lowOxygenActive = true;
                ShowLowOxygen();
            }
        }
        else
        {
            _lowOxygenActive = false;
        }
    }

    // ── Internal ────────────────────────────────────────────────────────────────

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(_displayDuration);
        if (_imageDisplay != null) _imageDisplay.gameObject.SetActive(false);
        _hideRoutine = null;
    }
}
