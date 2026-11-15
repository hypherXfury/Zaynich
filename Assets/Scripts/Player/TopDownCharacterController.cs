using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves the character through a 2D top-down world using joystick input.
/// Walk speed is used for low joystick deflection; run speed kicks in when
/// deflection exceeds <see cref="_runThreshold"/>.
/// Supports stacked speed modifiers (e.g., biofilm slow zones) via
/// <see cref="PushSpeedModifier"/> and <see cref="PopSpeedModifier"/>.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class TopDownCharacterController : MonoBehaviour
{
    [SerializeField] private Joystick _joystick;
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _runSpeed = 9f;
    [SerializeField] [Range(0f, 1f)] private float _runThreshold = 0.75f;

    private Rigidbody2D _rb;

    /// <summary>Stacked multiplicative speed modifiers. Each entry is a multiplier (e.g., 0.4 = 60% slow).</summary>
    private readonly List<float> _speedModifiers = new();

    /// <summary>Raw joystick deflection magnitude in the [0, 1] range.</summary>
    public float MoveMagnitude => _joystick != null ? _joystick.Direction.magnitude : 0f;

    /// <summary>Normalized movement direction derived from the joystick.</summary>
    public Vector2 MoveDirection => _joystick != null ? _joystick.Direction.normalized : Vector2.zero;

    /// <summary>Current movement speed in world units per second (rigidbody velocity magnitude).</summary>
    public float Speed => _rb != null ? _rb.linearVelocity.magnitude : 0f;

    /// <summary>True when the joystick deflection exceeds the run threshold.</summary>
    public bool IsRunning => MoveMagnitude >= _runThreshold;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void FixedUpdate()
    {
        float baseSpeed = IsRunning ? _runSpeed : _walkSpeed;
        _rb.linearVelocity = _joystick.Direction * (baseSpeed * ComputeModifierProduct());
    }

    // ── Speed modifier API ────────────────────────────────────────────────────

    /// <summary>
    /// Adds a multiplicative speed modifier. Values less than 1 slow the player.
    /// Pair every push with a matching <see cref="PopSpeedModifier"/> call.
    /// </summary>
    public void PushSpeedModifier(float multiplier) => _speedModifiers.Add(multiplier);

    /// <summary>
    /// Removes the first occurrence of <paramref name="multiplier"/> from the modifier stack.
    /// Must match the exact value passed to <see cref="PushSpeedModifier"/>.
    /// </summary>
    public void PopSpeedModifier(float multiplier) => _speedModifiers.Remove(multiplier);

    private float ComputeModifierProduct()
    {
        float result = 1f;
        foreach (float m in _speedModifiers)
            result *= m;
        return result;
    }
}
