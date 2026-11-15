using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the MUSIC and SOUND toggles on SettingsMenuUI.
///
/// Put this on the SettingsMenuUI object and drag in the two Toggle components
/// (SettingBox/Item2/Toggle is MUSIC, SettingBox/Item3/Toggle is SOUND). The
/// panel only reads and writes <see cref="AudioOptions"/>; actually silencing
/// the AudioSources is <see cref="AudioSettingsManager"/>'s job, which is why
/// this works unchanged in MainMenu where the scene has no AudioSource.
///
/// The toggles are re-synced every time the panel is shown rather than once at
/// startup, since the panel starts disabled (PauseMenu hides it in Start) and
/// the settings may have been changed in an earlier scene.
/// </summary>
public class AudioSettingsPanel : MonoBehaviour
{
    [Header("Toggles")]
    [Tooltip("SettingBox/Item2/Toggle — the MUSIC row.")]
    [SerializeField] private Toggle _musicToggle;
    [Tooltip("SettingBox/Item3/Toggle — the SOUND row.")]
    [SerializeField] private Toggle _soundToggle;

    [Header("State labels (optional)")]
    [Tooltip("The ON/OFF text on the MUSIC row. Left empty, the 'On/Off' object beside the toggle is used.")]
    [SerializeField] private TMP_Text _musicStateLabel;
    [Tooltip("The ON/OFF text on the SOUND row. Left empty, the 'On/Off' object beside the toggle is used.")]
    [SerializeField] private TMP_Text _soundStateLabel;
    [SerializeField] private string _onText  = "ON";
    [SerializeField] private string _offText = "OFF";

    private bool _wired;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        Wire();
        Refresh();
    }

    private void OnDestroy()
    {
        if (_musicToggle != null) _musicToggle.onValueChanged.RemoveListener(SetMusicEnabled);
        if (_soundToggle != null) _soundToggle.onValueChanged.RemoveListener(SetSoundEnabled);
    }

    // ── Public API — also usable straight from a Toggle's OnValueChanged ──────

    public void SetMusicEnabled(bool enabled)
    {
        AudioOptions.MusicEnabled = enabled;
        UpdateLabel(_musicStateLabel, enabled);
    }

    public void SetSoundEnabled(bool enabled)
    {
        AudioOptions.SoundEnabled = enabled;
        UpdateLabel(_soundStateLabel, enabled);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void Wire()
    {
        if (_wired) return;
        _wired = true;

        if (_musicToggle != null)
        {
            if (_musicStateLabel == null) _musicStateLabel = FindStateLabel(_musicToggle);
            _musicToggle.onValueChanged.AddListener(SetMusicEnabled);
        }
        else Debug.LogWarning($"[{nameof(AudioSettingsPanel)}] Music toggle is not assigned on '{name}'.", this);

        if (_soundToggle != null)
        {
            if (_soundStateLabel == null) _soundStateLabel = FindStateLabel(_soundToggle);
            _soundToggle.onValueChanged.AddListener(SetSoundEnabled);
        }
        else Debug.LogWarning($"[{nameof(AudioSettingsPanel)}] Sound toggle is not assigned on '{name}'.", this);
    }

    /// <summary>Pulls the saved values into the UI without firing the listeners back at us.</summary>
    private void Refresh()
    {
        bool music = AudioOptions.MusicEnabled;
        bool sound = AudioOptions.SoundEnabled;

        if (_musicToggle != null) _musicToggle.SetIsOnWithoutNotify(music);
        if (_soundToggle != null) _soundToggle.SetIsOnWithoutNotify(sound);

        UpdateLabel(_musicStateLabel, music);
        UpdateLabel(_soundStateLabel, sound);
    }

    private void UpdateLabel(TMP_Text label, bool enabled)
    {
        if (label != null) label.text = enabled ? _onText : _offText;
    }

    /// <summary>
    /// Falls back to the "On/Off" text sitting beside the toggle in each Item row.
    /// Matched by walking the siblings rather than Transform.Find, whose path
    /// syntax would read the '/' in "On/Off" as a child separator.
    /// </summary>
    private static TMP_Text FindStateLabel(Toggle toggle)
    {
        Transform row = toggle.transform.parent;
        if (row == null) return null;

        foreach (Transform child in row)
        {
            if (child.name == "On/Off") return child.GetComponent<TMP_Text>();
        }

        return null;
    }
}
