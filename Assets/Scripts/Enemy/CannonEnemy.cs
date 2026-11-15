using UnityEngine;

/// <summary>
/// Enemy variant fired like a cannon shot from its spawn pore straight toward
/// the player, instead of chasing. Flies in a fixed direction at a fixed speed
/// and is silently removed (no kill rewards) once it exits the arena bounds —
/// used by Chamber for <see cref="ChamberSpawnType.Shootable"/> entries.
///
/// SETUP:
///   Chamber adds this component itself when spawning a Shootable entry, so no
///   manual prefab setup is required. If you want per-prefab tuning instead,
///   add it to the prefab alongside Enemy and Chamber will reuse it.
/// </summary>
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(Rigidbody2D))]
public class CannonEnemy : MonoBehaviour
{
    private Rigidbody2D   _rb;
    private Enemy         _enemy;
    private BoxCollider2D _arenaBounds;
    private bool          _launched;

    /// <summary>True once Launch has set the enemy flying — Enemy.FixedUpdate checks this to skip its default chase.</summary>
    public bool IsLaunched => _launched;

    private void Awake()
    {
        _rb    = GetComponent<Rigidbody2D>();
        _enemy = GetComponent<Enemy>();
    }

    /// <summary>Fires this enemy in a straight line toward the player's position at spawn time.</summary>
    public void Launch(BoxCollider2D arenaBounds, Transform player, float speed)
    {
        _arenaBounds = arenaBounds;

        Vector2 direction = player != null
            ? ((Vector2)player.position - (Vector2)transform.position).normalized
            : (Vector2)transform.right;

        _rb.linearVelocity = direction * speed;
        _launched = true;
    }

    private void FixedUpdate()
    {
        if (!_launched || _arenaBounds == null) return;

        Bounds  b = _arenaBounds.bounds;
        Vector3 p = transform.position;

        if (p.x < b.min.x || p.x > b.max.x || p.y < b.min.y || p.y > b.max.y)
            _enemy.DespawnSilently();
    }
}
