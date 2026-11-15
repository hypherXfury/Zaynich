using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Tracks player hit points and broadcasts health-change and death events.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    private const float DefaultMaxHealth = 100f;

    [SerializeField] private float _maxHealth = DefaultMaxHealth;

    [Header("Damage Feedback")]
    [Tooltip("Sprite renderer flashed on hit. Defaults to one on this object/children.")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [Tooltip("Camera shaken on hit. Defaults to Camera.main's CameraController.")]
    [SerializeField] private CameraController _cameraController;
    [Tooltip("Alpha (0-255) the sprite dips to at the peak of the flash.")]
    [SerializeField][Range(0f, 255f)] private float _flashMinAlpha = 180f;
    [Tooltip("Seconds for one full down-then-up alpha flash.")]
    [SerializeField] private float _flashDuration = 0.15f;
    [SerializeField] private float _shakeMagnitude = 0.25f;
    [SerializeField] private float _shakeDuration  = 0.2f;

    public float MaxHealth     => _maxHealth;
    public float CurrentHealth { get; private set; }
    public bool  IsDead        { get; private set; }

    /// <summary>Raised whenever health changes. Args: (currentHealth, maxHealth).</summary>
    public event Action<float, float> OnHealthChanged;

    /// <summary>Raised once when the player's health reaches zero.</summary>
    public event Action OnDeath;

    private Coroutine _flashRoutine;

    private void Awake()
    {
        CurrentHealth = _maxHealth;

        if (_spriteRenderer == null)
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_cameraController == null && Camera.main != null)
            _cameraController = Camera.main.GetComponent<CameraController>();
    }

    /// <summary>Reduces current health by <paramref name="amount"/>. Triggers death if health reaches zero.</summary>
    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
        VitalsManager.Instance?.ReportPlayerDamaged(amount);

        PlayDamageFeedback();

        if (CurrentHealth <= 0f)
            HandleDeath();
    }

    // ── Damage feedback ───────────────────────────────────────────────────────

    private void PlayDamageFeedback()
    {
        _cameraController?.Shake(_shakeMagnitude, _shakeDuration);

        if (_spriteRenderer != null && isActiveAndEnabled)
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        float minAlpha = _flashMinAlpha / 255f;
        float half     = Mathf.Max(_flashDuration * 0.5f, 0.0001f);

        // 255 → 180
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            SetAlpha(Mathf.Lerp(1f, minAlpha, t / half));
            yield return null;
        }
        // 180 → 255
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            SetAlpha(Mathf.Lerp(minAlpha, 1f, t / half));
            yield return null;
        }

        SetAlpha(1f);
        _flashRoutine = null;
    }

    private void SetAlpha(float a)
    {
        Color c = _spriteRenderer.color;
        c.a = a;
        _spriteRenderer.color = c;
    }

    private void HandleDeath()
    {
        IsDead = true;
        OnDeath?.Invoke();
        Debug.Log("[PlayerHealth] Player died.");
    }

    /// <summary>
    /// Syncs CurrentHealth to a value driven externally (VitalsManager's derived Health,
    /// scaled from its 0–100 range to this component's MaxHealth).
    /// </summary>
    public void AdjustHealth(float health)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Clamp(health, 0f, _maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);

        if (CurrentHealth <= 0f)
            HandleDeath();
    }
}
