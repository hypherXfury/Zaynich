using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Enforces the two <see cref="AudioOptions"/> switches on every AudioSource in
/// the loaded scene by muting them.
///
/// Creates itself before the first scene loads and survives scene changes, so
/// nothing has to be wired up per scene — that's also what lets the Settings
/// panel in MainMenu (a scene with no AudioSource of its own) still take effect
/// once Level 1 loads.
///
/// Muting rather than zeroing volume is deliberate: each source keeps its
/// authored volume (the level music sits at 0.05), so flipping a toggle back on
/// restores the intended mix instead of a flat 1.0.
///
/// Sources are re-swept periodically while a channel is off, because enemies and
/// pickups carry their own AudioSource and spawn long after the scene loads —
/// a one-shot pass at load time would leave those audible. The sweep is skipped
/// entirely while both channels are on, which is the common case: a newly
/// spawned source is unmuted by default, so there is nothing to enforce.
/// </summary>
[DefaultExecutionOrder(-100)]
public class AudioSettingsManager : MonoBehaviour
{
    /// <summary>Names treated as music. Everything else counts as a sound effect.</summary>
    private static readonly string[] MusicObjectNames = { "AudioManager_Level", "Music", "BGM" };

    /// <summary>Seconds between sweeps while at least one channel is muted.</summary>
    private const float RescanInterval = 0.25f;

    private static AudioSettingsManager _instance;

    private float _rescanTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        var go = new GameObject("AudioSettingsManager");
        _instance = go.AddComponent<AudioSettingsManager>();
        DontDestroyOnLoad(go);
    }

    private void OnEnable()
    {
        AudioOptions.Changed      += ApplyToScene;
        SceneManager.sceneLoaded  += OnSceneLoaded;
    }

    private void OnDisable()
    {
        AudioOptions.Changed      -= ApplyToScene;
        SceneManager.sceneLoaded  -= OnSceneLoaded;

        if (_instance == this) _instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToScene();

    private void Update()
    {
        // Both on → every source should be unmuted, which is already their
        // default, so there is nothing a sweep could fix.
        if (AudioOptions.MusicEnabled && AudioOptions.SoundEnabled) return;

        // Unscaled: the Settings panel is opened from the pause menu, which
        // runs at timeScale 0.
        _rescanTimer -= Time.unscaledDeltaTime;
        if (_rescanTimer > 0f) return;

        _rescanTimer = RescanInterval;
        ApplyToScene();
    }

    /// <summary>
    /// Pushes the current switches onto every AudioSource in the scene, inactive
    /// ones included — the SFX source lives under a Canvas panel that spends
    /// most of its time disabled.
    /// </summary>
    public static void ApplyToScene()
    {
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include,
                                                              FindObjectsSortMode.None);

        foreach (AudioSource source in sources)
            source.mute = !AudioOptions.IsEnabled(Classify(source));
    }

    private static AudioChannel Classify(AudioSource source)
    {
        if (source.TryGetComponent(out AudioChannelTag tag)) return tag.Channel;

        string name = source.gameObject.name;
        foreach (string musicName in MusicObjectNames)
        {
            if (name.IndexOf(musicName, StringComparison.OrdinalIgnoreCase) >= 0)
                return AudioChannel.Music;
        }

        return AudioChannel.Sound;
    }
}
