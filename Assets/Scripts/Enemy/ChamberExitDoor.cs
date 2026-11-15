using UnityEngine;

/// <summary>
/// Sits on the arena's exit door — a trigger Collider2D (e.g. the CircleCollider2D
/// placed on the door) the player must walk through after clearing the chamber
/// to leave. Forwards player entry to the owning Chamber, which then swaps the
/// environment back to the overworld and fades the transition.
///
/// SETUP:
///   1. Place this on the door GameObject inside the chamber's arena prefab,
///      alongside its trigger Collider2D.
///   2. Assign _chamber (the Chamber this door belongs to).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ChamberExitDoor : MonoBehaviour
{
    [SerializeField] private Chamber _chamber;

    /// <summary>The Chamber this door belongs to — let its owner find it back (see Chamber.ExitDoor).</summary>
    public Chamber Chamber => _chamber;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_chamber == null) return;
        if (other.GetComponent<PlayerHealth>() == null) return;

        _chamber.ExitThroughDoor();
    }
}
