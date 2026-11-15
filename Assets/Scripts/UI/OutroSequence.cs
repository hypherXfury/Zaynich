using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Outro scene: play the ending cutscene, then reveal the run time.
///
/// The whole end-of-game flow is now split across two scenes:
///
///   Boss scene   BossWinPanel — boss dies, beat, fade to black, load "Outro"
///   Outro scene  THIS — play the ending video, then show "Game Completed" + time
///
/// <see cref="CutscenePlayer"/> is deliberately inert on its own: it hides itself
/// in Awake and waits for someone to call Play(). Copying it into a scene without
/// a caller is why the Outro scene did nothing — this script is that caller.
///
/// SETUP:
///   1. Put this on any object that stays active for the whole scene (the Canvas
///      or a bare "OutroSequence" object). It must NOT go on the panel it reveals.
///   2. Assign _cutscenePlayer (falls back to finding the one in the scene).
///   3. _panelRoot / _messageText are optional — leave them empty and a plain
///      black panel with a centered label is built at runtime, so the scene works
///      with no extra UI. Assign them once there's proper art.
/// </summary>
public class OutroSequence : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The scene's CutscenePlayer. Left empty, the one in this scene is found at runtime.")]
    [SerializeField] private CutscenePlayer _cutscenePlayer;
    [Tooltip("Optional. The 'Game Completed' panel to reveal after the video. Built at runtime if empty.")]
    [SerializeField] private GameObject _panelRoot;
    [Tooltip("Optional. Label inside that panel. Built at runtime if empty.")]
    [SerializeField] private TextMeshProUGUI _messageText;

    [Header("Message")]
    [SerializeField] private string _message = "Game Completed";
    [Tooltip("Shown before the mm:ss run time, on the line under the message.")]
    [SerializeField] private string _timePrefix = "Time: ";

    [Header("Timing")]
    [Tooltip("Fade from black onto the completion panel once the video is done.")]
    [SerializeField] private float _panelFadeInDuration = 0.8f;

    private bool _completed;

    private void Awake()
    {
        // The boss scene freezes nothing on its way out, but a pause menu left
        // open in an earlier scene would carry a zero timeScale into this one —
        // and every fade below advances on Time.deltaTime.
        Time.timeScale = 1f;

        // Include inactive: CutscenePlayer hides its own GameObject in Awake, and
        // there is no guaranteed ordering between its Awake and ours.
        if (_cutscenePlayer == null)
            _cutscenePlayer = FindFirstObjectByType<CutscenePlayer>(FindObjectsInactive.Include);

        if (_panelRoot == null) BuildFallbackPanel();
        if (_panelRoot != null) _panelRoot.SetActive(false);
    }

    private void Start()
    {
        // GameTimer is a static, so it still holds the total the boss banked when
        // it dies and loads us. Restore covers opening this scene directly in the
        // editor, where the static starts at zero.
        if (GameTimer.Elapsed <= 0f) GameTimer.Restore(SaveSystem.Data.elapsedSeconds);

        // Touch the fader now so its overlay canvas exists before the handover —
        // building it mid-frame during the fade would flash a frame of camera.
        _ = ScreenFader.Instance;

        if (_cutscenePlayer == null)
        {
            Debug.LogError("[OutroSequence] No CutscenePlayer in the scene — showing the completion panel directly.", this);
            ShowCompletion();
            return;
        }

        _cutscenePlayer.Finished += HandleCutsceneFinished;
        _cutscenePlayer.Play();
    }

    private void OnDestroy()
    {
        if (_cutscenePlayer != null) _cutscenePlayer.Finished -= HandleCutsceneFinished;
    }

    private void HandleCutsceneFinished()
    {
        if (_cutscenePlayer != null) _cutscenePlayer.Finished -= HandleCutsceneFinished;
        ShowCompletion();
    }

    /// <summary>
    /// Runs synchronously, inside CutscenePlayer's Finished callback: the screen
    /// is black right now because of the cutscene's own overlay, and that overlay
    /// vanishes the moment we return. Raising ScreenFader's black here — FadeIn
    /// snaps it to opaque on the first step, before its first yield — means the
    /// two blacks overlap for that frame and the swap is invisible.
    /// </summary>
    private void ShowCompletion()
    {
        if (_completed) return;
        _completed = true;

        if (_messageText != null)
            _messageText.SetText($"{_message}\n{_timePrefix}{GameTimer.Formatted}");

        if (_panelRoot != null) _panelRoot.SetActive(true);

        StartCoroutine(ScreenFader.Instance.FadeIn(_panelFadeInDuration));
    }

    // ── Fallback UI ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal completion panel so the outro reads correctly before any
    /// art exists — the same fallback trick MainMenu uses for its Continue button.
    /// Assigning _panelRoot in the Inspector skips all of this.
    /// </summary>
    private void BuildFallbackPanel()
    {
        var canvasGO = new GameObject("OutroCompletionPanel (auto)",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the cutscene canvas, below ScreenFader's overlay (1000) — the
        // fader has to be able to cover this panel to fade it in.
        canvas.sortingOrder = 900;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        var backgroundGO = new GameObject("Background", typeof(Image));
        backgroundGO.transform.SetParent(canvasGO.transform, false);
        var background = backgroundGO.GetComponent<Image>();
        background.color         = Color.black;
        background.raycastTarget = false;
        Stretch(background.rectTransform);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(canvasGO.transform, false);
        Stretch((RectTransform)labelGO.transform);

        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.alignment     = TextAlignmentOptions.Center;
        label.fontSize      = 72f;
        label.color         = Color.white;
        label.font          = TMP_Settings.defaultFontAsset;
        label.raycastTarget = false;

        _panelRoot   = canvasGO;
        _messageText = label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
