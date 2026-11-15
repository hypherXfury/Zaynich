using UnityEngine;

/// <summary>
/// Optional override that pins an AudioSource to a specific channel.
///
/// <see cref="AudioSettingsManager"/> classifies sources by GameObject name and
/// treats anything that isn't recognisably music as a sound effect, which is
/// correct for every source currently in the project. Drop this component next
/// to an AudioSource only when that guess would be wrong — e.g. a second music
/// track on an object not named "Music"/"BGM"/"AudioManager_Level".
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class AudioChannelTag : MonoBehaviour
{
    [Tooltip("Which Settings toggle this object's AudioSource obeys.")]
    [SerializeField] private AudioChannel _channel = AudioChannel.Sound;

    public AudioChannel Channel => _channel;
}
