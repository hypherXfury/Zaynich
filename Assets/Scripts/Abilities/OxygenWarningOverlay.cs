using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Flashes an overlay Image while Oxygen is below a threshold.
///
/// ── SETUP ─────────────────────────────────────────────────────────────────
///   1. Add this component to the "OxyGen warning overlay" GameObject.
///   2. Assign its Image in the Inspector (auto-grabbed from this object if left empty).
///   3. Tune Threshold / Flash Speed as desired.
///
/// It listens to VitalsManager.OnOxygenChanged, so the flash starts the moment
/// Oxygen drops below the threshold and stops as soon as it climbs back above.
/// </summary>
[RequireComponent(typeof(Image))]
public class OxygenWarningOverlay : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("Flash while Oxygen is strictly below this value.")]
    [SerializeField] [Range(0f, 100f)] private float _threshold = 15f;

    [Header("Flash")]
    [SerializeField] private Image _image;
    [Tooltip("Full flash cycles (0 → 1 → 0 alpha) per second.")]
    [SerializeField] private float _flashSpeed = 2f;
    [Tooltip("Alpha at the brightest point of the flash (0–1).")]
    [SerializeField] [Range(0f, 1f)] private float _maxAlpha = 1f;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip heartRate;

    private bool _flashing;
    private bool _isPlaying;

    private void Awake()
    {
        if (_image == null) _image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        if (VitalsManager.Instance != null)
        {
            VitalsManager.Instance.OnOxygenChanged += HandleOxygenChanged;
            HandleOxygenChanged(VitalsManager.Instance.Oxygen);   // sync to current value
        }
    }

    private void Start()
    {
        // VitalsManager may finish its Awake after ours; re-subscribe if we missed it.
        if (VitalsManager.Instance != null && !_flashing)
        {
            VitalsManager.Instance.OnOxygenChanged -= HandleOxygenChanged;
            VitalsManager.Instance.OnOxygenChanged += HandleOxygenChanged;
            HandleOxygenChanged(VitalsManager.Instance.Oxygen);
        }
    }

    private void OnDisable()
    {
        if (VitalsManager.Instance != null)
            VitalsManager.Instance.OnOxygenChanged -= HandleOxygenChanged;

        StopSound();
        _flashing = false;
    }

    private void HandleOxygenChanged(float oxygen)
    {
        SetFlashing(oxygen < _threshold);
    }

    private void SetFlashing(bool on)
    {
        if (_flashing == on) return;
        _flashing = on;

        if (_flashing)
        {
            StartSound();
        }
        else
        {
            SetAlpha(0f);   // fully hide when we stop
            StopSound();
        }
    }

    private void Update()
    {
        if (!_flashing || _image == null) return;

        // PingPong gives a smooth 0 → maxAlpha → 0 triangle wave.
        float alpha = Mathf.PingPong(Time.unscaledTime * _flashSpeed, _maxAlpha);
        SetAlpha(alpha);
    }

    private void SetAlpha(float a)
    {
        if (_image == null) return;
        Color c = _image.color;
        c.a = a;
        _image.color = c;
    }

    private void StartSound()
    {
        if (audioSource == null || heartRate == null || _isPlaying) return;
        audioSource.clip = heartRate;
        audioSource.loop = true;
        audioSource.Play();
        _isPlaying = true;
    }

    private void StopSound()
    {
        if (audioSource == null || !_isPlaying) return;
        audioSource.Stop();
        _isPlaying = false;
    }
}
