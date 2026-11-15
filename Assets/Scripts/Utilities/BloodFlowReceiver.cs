using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Add to any GameObject that should be pushed during a blood flow surge
/// (player, enemies, oxygen pickups, collectibles, etc).
/// Registers itself with BloodFlowManager automatically.
///
/// While the first-launch tutorial is running, blood flow is disabled: every
/// receiver keeps its component disabled and does not register. TutorialManager
/// calls SetGameplayEnabled(true) when the tutorial completes (and immediately
/// for returning players), which switches all receivers on.
///
/// SETUP: Just attach to the prefab. Needs a Rigidbody2D on the same GameObject.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BloodFlowReceiver : MonoBehaviour
{
    [Tooltip("Multiplier on the global surge force for this specific object.\\n" +
             "1 = full force. 0.5 = half force (lighter objects).\\n" +
             "Set lower on the player so they can still fight the current.")]
    [SerializeField] private float _forceMultiplier = 1f;

    private Rigidbody2D _rb;

    // Global gate: blood flow stays off until the tutorial is complete.
    private static bool s_gameplayEnabled;
    private static readonly List<BloodFlowReceiver> s_all = new();

    /// <summary>
    /// Enables/disables blood flow for every receiver at once. Called by
    /// TutorialManager: false while the tutorial runs, true once it completes.
    /// </summary>
    public static void SetGameplayEnabled(bool enabled)
    {
        s_gameplayEnabled = enabled;
        for (int i = s_all.Count - 1; i >= 0; i--)
        {
            if (s_all[i] == null) { s_all.RemoveAt(i); continue; }
            s_all[i].enabled = enabled;
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        if (!s_all.Contains(this)) s_all.Add(this);

        // Stay disabled until the tutorial hands over control.
        if (!s_gameplayEnabled) enabled = false;
    }

    private void OnEnable()
    {
        if (!s_gameplayEnabled) { enabled = false; return; }

        if (BloodFlowManager.Instance != null)
            BloodFlowManager.Instance.Register(this);
        else
            // Manager not ready yet — queue for registration on its Start()
            BloodFlowManager.RegisterPending(this);
    }

    private void OnDisable()
    {
        if (BloodFlowManager.Instance != null)
            BloodFlowManager.Instance.Unregister(this);
    }

    private void OnDestroy()
    {
        s_all.Remove(this);
    }

    /// <summary>Called by BloodFlowManager every FixedUpdate during a surge.</summary>
    public void ApplySurgeForce(Vector2 force)
    {
        _rb.AddForce(force * _forceMultiplier, ForceMode2D.Force);
    }
}