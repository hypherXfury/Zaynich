using UnityEngine;

/// <summary>
/// Continuously spins this enemy around its Z axis, so it visually looks like
/// it's rolling/spinning as it chases the player. Purely cosmetic — it has no
/// effect on Enemy's movement, collision, or damage.
/// </summary>
public class SpinningEnemy : MonoBehaviour
{
    [Tooltip("Degrees per second. Positive spins counter-clockwise, negative spins clockwise.")]
    [SerializeField] private float _spinSpeed = 360f;

    private void Update()
    {
        transform.Rotate(0f, 0f, _spinSpeed * Time.deltaTime);
    }
}
