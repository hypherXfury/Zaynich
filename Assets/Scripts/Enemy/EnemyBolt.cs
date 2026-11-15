using UnityEngine;

/// <summary>
/// Projectile fired by <see cref="SpitterEnemy"/>.
/// Requires a Rigidbody2D and a trigger Collider2D on the same GameObject.
///
/// PREFAB SETUP:
///   • Rigidbody2D  — Gravity Scale = 0, Collision Detection = Continuous
///   • CircleCollider2D  — Is Trigger = true
///   • SpriteRenderer with your bolt sprite
///   • This component
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBolt : MonoBehaviour
{
    private float _damage;
    private bool  _hasHit;   // prevent double-damage if two colliders overlap briefly

    /// <summary>Called by SpitterEnemy immediately after Instantiate.</summary>
    public void Launch(Vector2 direction, float speed, float damage, float lifetime)
    {
        _damage = damage;

        var rb            = GetComponent<Rigidbody2D>();
        rb.gravityScale   = 0f;
        rb.linearVelocity = direction * speed;

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasHit) return;

        // Damage the player
        var playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            _hasHit = true;
            playerHealth.TakeDamage(_damage);
            Destroy(gameObject);
            return;
        }

        // Optionally destroy on hitting solid geometry (tag or layer check)
        // Uncomment if you have environment colliders tagged "Wall":
        // if (other.CompareTag("Wall")) Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
         if (_hasHit) return;

        // Damage the player
        var playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            _hasHit = true;
            playerHealth.TakeDamage(_damage);
            Destroy(gameObject);
            return;
        }
    }
}