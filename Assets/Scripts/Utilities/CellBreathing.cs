using UnityEngine;

/// <summary>
/// Attach to any blood cell sprite prefab (RBC, WBC, Platelet, etc.)
/// Applies a smooth sine-wave "breathing" pulse to the object's local scale.
/// 
/// Each cell gets a randomised phase offset on Enable so they never all
/// pulse in sync — the crowd looks organic rather than mechanical.
/// </summary>
public class CellBreathing : MonoBehaviour
{
    [Header("Pulse Shape")]
    [Tooltip("Base scale multiplier. Keep this at 1 unless you want cells globally smaller/larger.")]
    public float baseScale = 1f;

    [Tooltip("How much the scale grows above and below baseScale. " +
             "0.08 = ±8% — subtle. 0.2 = ±20% — dramatic.")]
    [Range(0.01f, 0.5f)]
    public float amplitude = 0.08f;

    [Tooltip("Breaths per second. 0.3–0.6 feels biological; higher feels mechanical.")]
    [Range(0.1f, 4f)]
    public float frequency = 0.4f;

    [Header("Per-Cell Variation")]
    [Tooltip("Randomly offset each cell's phase on spawn so they don't pulse in unison.")]
    public bool randomisePhase = true;

    [Tooltip("Add a small random ±delta to amplitude so cells aren't identical.")]
    [Range(0f, 0.1f)]
    public float amplitudeVariance = 0.02f;

    [Tooltip("Add a small random ±delta to frequency so cells breathe at slightly different rates.")]
    [Range(0f, 0.3f)]
    public float frequencyVariance = 0.1f;

    // ── Runtime values (set per-activation so pooled objects re-randomise) ──
    private float phase;
    private float resolvedAmplitude;
    private float resolvedFrequency;

    // Cache original local scale set in the prefab so we respect non-uniform scales.
    private Vector3 originalScale;
    private bool originalScaleCached = false;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        // Cache the prefab's authored scale once.
        CacheOriginalScale();
    }

    void OnEnable()
    {
        // Re-randomise every time the object is pulled from the pool.
        phase             = randomisePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        resolvedAmplitude = amplitude + Random.Range(-amplitudeVariance, amplitudeVariance);
        resolvedFrequency = frequency + Random.Range(-frequencyVariance, frequencyVariance);

        resolvedAmplitude = Mathf.Max(0f, resolvedAmplitude); // never negative
        resolvedFrequency = Mathf.Max(0.05f, resolvedFrequency);
    }

    void OnDisable()
    {
        // Reset scale cleanly when returned to pool.
        transform.localScale = originalScale * baseScale;
    }

    void Update()
    {
        float sine      = Mathf.Sin(Time.time * resolvedFrequency * Mathf.PI * 2f + phase);
        float scaleMult = baseScale + sine * resolvedAmplitude;
        transform.localScale = originalScale * scaleMult;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void CacheOriginalScale()
    {
        if (!originalScaleCached)
        {
            originalScale       = transform.localScale;
            originalScaleCached = true;
        }
    }

    // ── Editor helper ─────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnValidate()
    {
        // Live-preview the base scale in the editor without entering Play mode.
        if (!Application.isPlaying && originalScaleCached)
            transform.localScale = originalScale * baseScale;
    }
#endif
}