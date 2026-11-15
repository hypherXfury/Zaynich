using UnityEngine;

/// <summary>
/// Runtime behaviour for a single urine droplet spawned by <see cref="UrineEffect"/>.
/// Moves along a designer-authored <see cref="SplinePath"/> and drops puddles along the way.
/// Purely transform-driven — no Rigidbody2D/Collider2D required on the prefab.
/// Added automatically on spawn — no need to put it on the prefab.
/// </summary>
public class UrineDroplet : MonoBehaviour
{
    private const float TangentEpsilon = 0.01f;

    private SplinePath _path;
    private float      _travelTime;
    private float      _elapsed;

    private GameObject[] _puddlePrefabs;
    private int          _maxPuddles;
    private int          _puddlesDropped;
    private float        _puddleMinScale;
    private float        _puddleMaxScale;
    private float        _puddleSpreadDuration;
    private float        _puddleMinLifetime;
    private float        _puddleMaxLifetime;

    public void Initialize(
        SplinePath   path,
        float        travelTime,
        GameObject[] puddlePrefabs,
        int          maxPuddles,
        float        puddleMinScale,
        float        puddleMaxScale,
        float        puddleSpreadDuration,
        float        puddleMinLifetime,
        float        puddleMaxLifetime)
    {
        _path                 = path;
        _travelTime           = Mathf.Max(travelTime, 0.01f);
        _puddlePrefabs        = puddlePrefabs;
        _maxPuddles           = maxPuddles;
        _puddleMinScale       = puddleMinScale;
        _puddleMaxScale       = puddleMaxScale;
        _puddleSpreadDuration = puddleSpreadDuration;
        _puddleMinLifetime    = puddleMinLifetime;
        _puddleMaxLifetime    = puddleMaxLifetime;

        transform.position = _path.Evaluate(0f);
    }

    private void Update()
    {
        if (_path == null) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _travelTime);

        transform.position = _path.Evaluate(t);
        OrientToTangent(t);
        DropPuddles(t);

        if (t >= 1f) enabled = false;
    }

    private void OrientToTangent(float t)
    {
        float aheadT = Mathf.Min(t + TangentEpsilon, 1f);
        Vector3 direction = _path.Evaluate(aheadT) - transform.position;
        if (direction.sqrMagnitude <= 0.0001f) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void DropPuddles(float t)
    {
        if (_puddlePrefabs == null || _puddlePrefabs.Length == 0 || _puddlesDropped >= _maxPuddles) return;

        float nextThreshold = (_puddlesDropped + 1) / (float)(_maxPuddles + 1);
        if (t < nextThreshold) return;

        _puddlesDropped++;

        GameObject prefab = _puddlePrefabs[Random.Range(0, _puddlePrefabs.Length)];

        // Parented to the droplet's own parent (UrineEffect's spawn root), not to
        // the droplet itself — puddles must stay put where they land, but still be
        // reachable so UrineEffect can clear them when the hazard is suspended.
        GameObject instance = Instantiate(prefab, transform.position, Quaternion.identity, transform.parent);

        float scaleFactor = Random.Range(_puddleMinScale, _puddleMaxScale);
        Vector3 targetScale = prefab.transform.localScale * scaleFactor;
        PuddleSpreadEffect spread = instance.AddComponent<PuddleSpreadEffect>();
        spread.Initialize(targetScale, _puddleSpreadDuration);

        instance.AddComponent<PuddleSlowEffect>();
        Destroy(instance, Random.Range(_puddleMinLifetime, _puddleMaxLifetime));
    }
}
