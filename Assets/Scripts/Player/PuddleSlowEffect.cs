using UnityEngine;

/// <summary>
/// Slows the player while they stand in a urine puddle, mirroring <see cref="Plasma"/>.
/// Added automatically on spawn — no need to put it on the puddle prefab.
///
/// Puddles spawn in bursts and can spatially overlap, so multiple instances may have the
/// player inside them at once. Overlap is tracked with a shared static counter and the
/// player's true original speed is cached only once — when the first puddle picks them up —
/// so a later-spawned overlapping puddle never mistakes an already-slowed speed for "original".
/// The real speed is restored only once the last overlapping puddle lets go.
/// </summary>
public class PuddleSlowEffect : MonoBehaviour
{
    private static int _overlapCount;
    private static float _cachedOriginalSpeed;
    private static FourDirPreviewController _cachedController;

    private FourDirPreviewController controller;
    private bool playerInside;

    void Start()
    {
        controller = FindFirstObjectByType<FourDirPreviewController>();
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        playerInside = true;

        if (_overlapCount == 0)
        {
            _cachedController    = controller;
            _cachedOriginalSpeed = controller.MoveSpeed;
        }
        _overlapCount++;

        controller.SetMoveSpeed(.25f);
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player")) EndOverlap();
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
            controller.SetMoveSpeed(.25f);
    }

    /// <summary>
    /// Puddles get destroyed on a timer and may still overlap the player when that happens.
    /// Destroying a trigger collider isn't guaranteed to fire OnTriggerExit2D in time, so
    /// end the overlap here as a safety net.
    /// </summary>
    void OnDestroy() => EndOverlap();

    private void EndOverlap()
    {
        if (!playerInside) return;

        playerInside = false;
        _overlapCount = Mathf.Max(0, _overlapCount - 1);

        if (_overlapCount == 0 && _cachedController != null)
            _cachedController.SetMoveSpeed(_cachedOriginalSpeed);
    }
}
