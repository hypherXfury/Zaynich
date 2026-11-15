using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the enemy intro card panel.
/// Populated by EnemyCardManager and shown once per enemy type per save.
///
/// HIERARCHY:
///   Canvas
///     └── EnemyCardPanel           ← CanvasGroup + this script
///           ├── Background         ← Image (card background)
///           ├── Left               ← (layout group)
///           │     ├── AlertRow     ← HorizontalLayoutGroup
///           │     │     ├── AlertIcon   ← Image (triangle warning icon)
///           │     │     └── AlertLabel  ← TMP "PATHOGEN DETECTED"
///           │     ├── EnemyName    ← TMP (large bold)
///           │     ├── EnemySubtitle← TMP (smaller, purple)
///           │     ├── Divider      ← Image (horizontal line)
///           │     ├── ObjectiveRow ← HorizontalLayoutGroup
///           │     │     ├── ObjIcon    ← Image (crosshair)
///           │     │     └── ObjLabel   ← TMP "OBJECTIVE"
///           │     ├── ObjectiveText← TMP
///           │     └── ThreatRow    ← HorizontalLayoutGroup
///           │           ├── ThreatIcon ← Image (shield)
///           │           ├── ThreatLabel← TMP "THREAT LEVEL"
///           │           ├── ThreatValue← TMP (e.g. "MODERATE")
///           │           └── ThreatPips ← HorizontalLayoutGroup (4 pip images)
///           └── EnemyImage         ← Image (right side, enemy sprite)
/// </summary>
public class EnemyCardUI : MonoBehaviour
{
    [Header("Text Elements")]
    [SerializeField] private TextMeshProUGUI _enemyNameText;
    [SerializeField] private TextMeshProUGUI _enemySubtitleText;
    [SerializeField] private TextMeshProUGUI _objectiveText;
    [SerializeField] private TextMeshProUGUI _threatValueText;

    [Header("Visuals")]
    [SerializeField] private Image   _enemyImage;
    [SerializeField] private Image[] _threatPips;          // 4 pips, filled left to right
    [SerializeField] private Color   _pipFilledColor  = new Color(1f,   0.65f, 0.1f,  1f);
    [SerializeField] private Color   _pipEmptyColor   = new Color(0.6f, 0.6f,  0.6f,  0.5f);

    [Header("Threat Level Colors")]
    [SerializeField] private Color _minimalColor  = new Color(0.2f, 0.8f, 0.3f,  1f);
    [SerializeField] private Color _lowColor      = new Color(0.3f, 0.7f, 1f,    1f);
    [SerializeField] private Color _moderateColor = new Color(1f,   0.65f, 0.1f, 1f);
    [SerializeField] private Color _highColor     = new Color(1f,   0.3f,  0.1f, 1f);
    [SerializeField] private Color _extremeColor  = new Color(0.9f, 0.1f,  0.1f, 1f);

    [Header("Animation")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float       _fadeInDuration  = 0.4f;
    [SerializeField] private float       _fadeOutDuration = 0.3f;

    [Header("Dismiss")]
    [Tooltip("Next button — the card stays up until the player taps it.")]
    [SerializeField] private Button _dismissButton;

    // ── Events ────────────────────────────────────────────────────────────────

    public event System.Action OnDismissed;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        if (_dismissButton != null) _dismissButton.onClick.AddListener(Dismiss);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(EnemyCardData data)
    {
        Populate(data);
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    public void Dismiss()
    {
        StartCoroutine(FadeOutAndHide());
    }

    // ── Populate ──────────────────────────────────────────────────────────────

    private void Populate(EnemyCardData data)
    {
        if (_enemyNameText     != null) _enemyNameText.text     = data.enemyName.ToUpper();
        if (_enemySubtitleText != null) _enemySubtitleText.text = data.enemySubtitle.ToUpper();
        if (_objectiveText     != null) _objectiveText.text     = data.objective;
        if (_enemyImage        != null)
        {
            _enemyImage.sprite  = data.enemyImage;
            _enemyImage.enabled = data.enemyImage != null;
        }

        // Threat level
        string label = data.ThreatLabelText.ToUpper();
        Color  col   = ThreatColor(data.threatLevel);

        if (_threatValueText != null)
        {
            _threatValueText.text  = label;
            _threatValueText.color = col;
        }

        // Pips
        if (_threatPips != null)
        {
            for (int i = 0; i < _threatPips.Length; i++)
            {
                if (_threatPips[i] == null) continue;
                _threatPips[i].color = i < data.threatLevel ? col : _pipEmptyColor;
            }
        }
    }

    private Color ThreatColor(int level) => level switch
    {
        0 => _minimalColor,
        1 => _lowColor,
        2 => _moderateColor,
        3 => _highColor,
        4 => _extremeColor,
        _ => _moderateColor
    };

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator FadeIn()
    {
        if (_canvasGroup == null) yield break;
        float elapsed = 0f;
        Time.timeScale = 0f;   // pause game while card shows
        while (elapsed < _fadeInDuration)
        {
            elapsed            += Time.unscaledDeltaTime;
            _canvasGroup.alpha  = Mathf.Clamp01(elapsed / _fadeInDuration);
            yield return null;
        }
        _canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOutAndHide()
    {
        if (_canvasGroup == null) { Finish(); yield break; }
        float elapsed = 0f;
        while (elapsed < _fadeOutDuration)
        {
            elapsed            += Time.unscaledDeltaTime;
            _canvasGroup.alpha  = Mathf.Lerp(1f, 0f, elapsed / _fadeOutDuration);
            yield return null;
        }
        _canvasGroup.alpha = 0f;
        Finish();
    }

    private void Finish()
    {
        Time.timeScale = 1f;   // resume game
        gameObject.SetActive(false);
        OnDismissed?.Invoke();
    }
}