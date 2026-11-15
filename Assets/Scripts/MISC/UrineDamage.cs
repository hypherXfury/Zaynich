using UnityEngine;

/// <summary>
/// Deals one-shot contact damage to the player, mirroring <see cref="EnemyBolt"/>'s pattern.
/// Attach directly to the UrineFinal droplet prefab (alongside its CircleCollider2D trigger).
/// <see cref="PlayerHealth.TakeDamage"/> already reports the hit to <see cref="VitalsManager"/>,
/// which drains oxygen proportionally to the damage dealt — no separate oxygen call needed.
/// </summary>
public class UrineDamage : MonoBehaviour
{
    [SerializeField] private float _damage = 5f;

    private bool _hasHit;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasHit) return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        _hasHit = true;
        playerHealth.TakeDamage(_damage);
    }
}
