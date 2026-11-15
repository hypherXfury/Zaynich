using UnityEngine;

/// <summary>
/// Grows a freshly spawned puddle from scale 0 up to its target scale, so it reads
/// as spreading liquid rather than popping into existence.
/// Added automatically on spawn — no need to put it on the prefab.
/// </summary>
public class PuddleSpreadEffect : MonoBehaviour
{
    private Vector3 _targetScale;
    private float   _duration;
    private float   _elapsed;

    public void Initialize(Vector3 targetScale, float duration)
    {
        _targetScale        = targetScale;
        _duration           = Mathf.Max(duration, 0.01f);
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        transform.localScale = Vector3.Lerp(Vector3.zero, _targetScale, t);

        if (t >= 1f) enabled = false;
    }
}
