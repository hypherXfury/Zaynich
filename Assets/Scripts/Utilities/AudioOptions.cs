using System;
using UnityEngine;

/// <summary>Which volume switch an <see cref="AudioSource"/> answers to.</summary>
public enum AudioChannel
{
    /// <summary>Looping background track (AudioManager_Level in each play scene).</summary>
    Music,
    /// <summary>Everything else — UI clicks, footsteps, enemy deaths, pickups.</summary>
    Sound
}

/// <summary>
/// The two audio switches behind the Settings panel, persisted in PlayerPrefs.
///
/// Deliberately kept out of <see cref="SaveSystem"/>: that save slot is wiped by
/// ResetForNewGame(), and a player who turned the music off does not expect
/// starting a new game to turn it back on.
///
/// This only stores the values and announces changes — <see cref="AudioSettingsManager"/>
/// is what actually mutes the AudioSources in the loaded scene. That split is why
/// the Settings panel works in MainMenu, which has no AudioSource at all: the
/// toggle writes the preference there, and the first play scene applies it.
/// </summary>
public static class AudioOptions
{
    private const string MusicKey = "ZaynichMusicEnabled";
    private const string SoundKey = "ZaynichSoundEnabled";

    private static bool _loaded;
    private static bool _musicEnabled = true;
    private static bool _soundEnabled = true;

    /// <summary>Raised whenever either switch flips. Listeners re-read the properties below.</summary>
    public static event Action Changed;

    public static bool MusicEnabled
    {
        get { EnsureLoaded(); return _musicEnabled; }
        set
        {
            EnsureLoaded();
            if (_musicEnabled == value) return;

            _musicEnabled = value;
            Persist(MusicKey, value);
            Changed?.Invoke();
        }
    }

    public static bool SoundEnabled
    {
        get { EnsureLoaded(); return _soundEnabled; }
        set
        {
            EnsureLoaded();
            if (_soundEnabled == value) return;

            _soundEnabled = value;
            Persist(SoundKey, value);
            Changed?.Invoke();
        }
    }

    public static bool IsEnabled(AudioChannel channel) =>
        channel == AudioChannel.Music ? MusicEnabled : SoundEnabled;

    public static void SetEnabled(AudioChannel channel, bool enabled)
    {
        if (channel == AudioChannel.Music) MusicEnabled = enabled;
        else                               SoundEnabled = enabled;
    }

    /// <summary>Both switches default to on, so a fresh install plays everything.</summary>
    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        _musicEnabled = PlayerPrefs.GetInt(MusicKey, 1) != 0;
        _soundEnabled = PlayerPrefs.GetInt(SoundKey, 1) != 0;
    }

    private static void Persist(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
