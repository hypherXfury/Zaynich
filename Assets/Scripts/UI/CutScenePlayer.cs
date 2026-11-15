using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Plays an mp4 cutscene via Unity's VideoPlayer after the UserDetailsPanel.
/// When the video ends OR the player taps Skip, loads the game scene.
/// Always plays — no save/skip memory.
///
/// HIERARCHY:
///   Canvas (Screen Space — Overlay)
///     └── CutscenePanel                  ← CanvasGroup + this script
///           ├── VideoDisplay             ← RawImage (full screen, video renders here)
///           ├── BlackOverlay             ← Image (solid black, for fade in/out)
///           └── SkipButton               ← Button
///                 └── Label              ← TextMeshProUGUI "Skip ›"
///
/// SETUP:
///   1. Add a VideoPlayer component to CutscenePanel (or any scene object).
///   2. Set VideoPlayer:
///        - Source     = Video Clip (drag your .mp4) OR URL
///        - Render Mode= Camera Far Plane OR API Only — we use RenderTexture below
///        - Play On Awake = OFF
///        - Loop        = OFF
///   3. Assign all references in the Inspector.
///   4. Something must CALL Play() — this panel deactivates itself in Awake and
///      does nothing on its own. In MainMenu the caller is <see cref="MainMenu"/>;
///      in the Outro scene it is <see cref="OutroSequence"/>.
///
/// Handing off when the video ends: assign <see cref="MissionBrief"/> to reveal a
/// panel (the main-menu flow), and/or subscribe to <see cref="Finished"/> for
/// anything more involved (the outro flow). Both happen while the screen is still
/// fully black, so a listener can raise its own black overlay in the same frame
/// and take over without a flash.
/// </summary>
public class CutscenePlayer : MonoBehaviour
{
    public static CutscenePlayer Instance { get; private set; }

    /// <summary>
    /// Raised once the video has ended (or been skipped) and the screen has faded
    /// to black, but BEFORE this panel hides itself. Listeners that want to own
    /// the screen next should put their own black up synchronously in the handler.
    /// </summary>
    public event Action Finished;

    [Header("References")]
    [SerializeField] private VideoPlayer   _videoPlayer;
    [SerializeField] private RawImage      _videoDisplay;   // fullscreen RawImage
    [SerializeField] private Image         _blackOverlay;   // fade in/out
    [SerializeField] private Button        _skipButton;
    [SerializeField] private CanvasGroup   _canvasGroup;

    

    [Header("Timing")]
    [SerializeField] private float _fadeInDuration  = 0.4f;
    [SerializeField] private float _fadeOutDuration = 0.5f;
    [SerializeField] private GameObject MissionBrief;

    // ── Private state ─────────────────────────────────────────────────────────

    private RenderTexture _rt;
    private bool          _skipped;
    private bool          _finished;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // RunCutscene drives playback itself (Prepare → fade in → Play), so a
        // scene that left Play On Awake ticked would otherwise start the clip
        // early, behind the hidden panel, and we'd restart it mid-way.
        if (_videoPlayer != null) _videoPlayer.playOnAwake = false;

        // Start hidden
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        SetOverlayAlpha(1f);
        gameObject.SetActive(false);

        if (_skipButton != null)
        {
            _skipButton.onClick.AddListener(OnSkipClicked);
            _skipButton.gameObject.SetActive(false);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Starts the cutscene. Nothing happens until someone calls this.</summary>
    public void Play()
    {
        if (_videoPlayer == null)
        {
            // Dead-ending here would strand whoever is waiting on us behind a
            // black screen, so hand straight off instead of just logging.
            Debug.LogError($"[CutscenePlayer] {name}: _videoPlayer is unassigned — skipping the cutscene.", this);
            if (MissionBrief != null) MissionBrief.SetActive(true);
            Finished?.Invoke();
            return;
        }

        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        gameObject.SetActive(true);
        _skipped  = false;
        _finished = false;
        StartCoroutine(RunCutscene());
    }

    // ── Cutscene sequence ─────────────────────────────────────────────────────

    private IEnumerator RunCutscene()
    {
        // 1 — Set up RenderTexture for the video
        SetupRenderTexture();

        // 2 — Prepare video (wait for it to be ready)
        _videoPlayer.Prepare();
        yield return new WaitUntil(() => _videoPlayer.isPrepared);

        // 3 — Fade in from black
        _videoPlayer.Play();
        yield return StartCoroutine(FadeOverlay(1f, 0f, _fadeInDuration));

        // 4 — Show skip button
        if (_skipButton != null) _skipButton.gameObject.SetActive(true);

        // 5 — Subscribe to video end
        _videoPlayer.loopPointReached += OnVideoFinished;

        // 6 — Wait for video to end or skip
        yield return new WaitUntil(() => _finished || _skipped);

        // 7 — Clean up
        _videoPlayer.loopPointReached -= OnVideoFinished;
        _videoPlayer.Stop();
        if (_skipButton != null) _skipButton.gameObject.SetActive(false);

        // 8 — Fade to black then hand off to the Mission Brief panel
        yield return StartCoroutine(FadeOverlay(0f, 1f, _fadeOutDuration));

        Time.timeScale = 1f;

        // Reveal the Mission Brief. It starts on its own black overlay and fades
        // in, so we hide this panel's visuals underneath it seamlessly. We keep
        // this GameObject active (in case MissionBrief is a child) but make it
        // fully transparent and stop it blocking clicks on the brief's button.
        if (MissionBrief != null) MissionBrief.SetActive(true);

        // Fired before we go transparent, so a listener can raise its own black
        // overlay in this same frame and the handover stays seamless.
        Finished?.Invoke();

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha          = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable   = false;
        }
    }

    // ── RenderTexture setup ───────────────────────────────────────────────────

    private void SetupRenderTexture()
    {
        int w = Screen.width;
        int h = Screen.height;

        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
        }

        _rt = new RenderTexture(w, h, 0)
        {
            filterMode = FilterMode.Bilinear
        };
        _rt.Create();

        _videoPlayer.renderMode    = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = _rt;

        ApplyAudioSafePlaybackSettings();

        if (_videoDisplay != null) _videoDisplay.texture = _rt;
    }

    /// <summary>
    /// Both cutscene clips carry an AAC soundtrack, and two non-default VideoPlayer
    /// settings will shred it. Forced here rather than left to the Inspector so the
    /// menu and outro scenes can't drift apart on it again.
    ///
    /// timeUpdateMode: whichever clock the player is told to follow, it drags the
    ///   audio back toward it whenever the two disagree — resampling and skipping,
    ///   which is what "corrupted" sounds like. DSPTime makes the audio hardware
    ///   clock the reference instead, so there is nothing to correct against. It is
    ///   Unity's default for a clip with audio, and (like UnscaledGameTime, which
    ///   both scenes were set to) it ignores Time.timeScale — so this keeps the
    ///   pause-immunity that setting was presumably chosen for.
    ///
    /// skipOnDrop: false means "never drop a video frame", so when decode can't
    ///   sustain realtime the whole clip stretches and the audio stretches with it.
    ///   1080p H.264 starting up right after the boss scene is torn down is exactly
    ///   where that bites, which is why the outro suffers more than the intro.
    ///   Dropping a frame is invisible; warping the audio is not.
    /// </summary>
    private void ApplyAudioSafePlaybackSettings()
    {
        _videoPlayer.skipOnDrop = true;

        _videoPlayer.timeUpdateMode = _videoPlayer.audioOutputMode != VideoAudioOutputMode.None
            ? VideoTimeUpdateMode.DSPTime          // only valid when we're playing audio
            : VideoTimeUpdateMode.UnscaledGameTime;
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    private void OnSkipClicked() => _skipped  = true;
    private void OnVideoFinished(VideoPlayer vp) => _finished = true;

    // ── Fade helpers ──────────────────────────────────────────────────────────

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetOverlayAlpha(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetOverlayAlpha(to);
    }

    private void SetOverlayAlpha(float a)
    {
        if (_blackOverlay == null) return;
        Color c = _blackOverlay.color;
        c.a = a;
        _blackOverlay.color = c;
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (_rt != null)
        {
            _videoPlayer.targetTexture = null;
            _rt.Release();
            Destroy(_rt);
        }
    }
}