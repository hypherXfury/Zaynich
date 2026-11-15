using UnityEngine;

/// <summary>
/// Attach to the parent GameObject that contains your level sprites.
/// Drifts the entire object in X and Y using Perlin noise — extremely subtle,
/// gives the level a gentle organic breathing feel.
///
/// SETUP: Just attach to the parent GO. No other configuration needed.
/// Tune _amplitude in the Inspector — 0.001 is barely perceptible (recommended).
/// </summary>
public class LevelWander : MonoBehaviour
{
    [Tooltip("Maximum displacement in world units. Keep this tiny — 0.001 to 0.005.")]
    [SerializeField] private float _amplitude = 0.001f;

    [Tooltip("How fast the drift moves. Lower = slower, lazier movement.")]
    [SerializeField] private float _frequency = 0.3f;

    private Vector3 _originPosition;
    private float   _seedX;
    private float   _seedY;
    private float   _time;

    private void Awake()
    {
        _originPosition = transform.position;

        // Different seeds so X and Y drift independently
        _seedX = Random.Range(0f, 100f);
        _seedY = Random.Range(100f, 200f);
    }

    private void Update()
    {
        _time += Time.deltaTime;

        float dx = (Mathf.PerlinNoise(_time * _frequency + _seedX, _seedX) - 0.5f) * 2f * _amplitude;
        float dy = (Mathf.PerlinNoise(_seedY, _time * _frequency + _seedY) - 0.5f) * 2f * _amplitude;

        transform.position = _originPosition + new Vector3(dx, dy, 0f);
    }
}