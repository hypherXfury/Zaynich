using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that manages the global blood flow surge cycle.
/// Every _surgeInterval seconds, a surge begins for _surgeDuration seconds,
/// then snaps back to idle. During a surge every registered Rigidbody2D in
/// the scene receives a strong downward push each physics frame.
///
/// Objects register themselves automatically via BloodFlowReceiver.
///
/// SETUP:
///   1. Add this component to a persistent scene GameObject (e.g. "GameManager").
///   2. Tune _surgeInterval, _surgeDuration, _surgeForce in the Inspector.
///   3. Add BloodFlowReceiver to any prefab that should be pushed:
///        - Player prefab
///        - Enemy prefab
///        - Oxygen pickup prefab
///        - Any other collectible prefab
/// </summary>
public class BloodFlowManager : MonoBehaviour
{
    public static BloodFlowManager Instance { get; private set; }

    [Header("Surge Timing")]
    [Tooltip("Seconds between surges (idle period).")]
    [SerializeField] private float _surgeInterval = 20f;

    [Tooltip("Seconds the surge lasts before snapping back to idle.")]
    [SerializeField] private float _surgeDuration = 5f;

    [Header("Force")]
    [Tooltip("Downward force applied to every registered Rigidbody2D per physics frame during surge.")]
    [SerializeField] private float _surgeForce = 12f;

    [Header("Cell Speed")]
    [Tooltip("Speed multiplier applied to CellWander scripts during a surge.")]
    [SerializeField] private float _cellSurgeMultiplier = 4f;

    // ── State ─────────────────────────────────────────────────────────────────

    private float _timer;
    private bool  _isSurging;

    public bool  IsSurging    => _isSurging;
    public float SurgeForce   => _surgeForce;

    /// <summary>Fired when a surge starts.</summary>
    public event System.Action OnSurgeStart;
    /// <summary>Fired when a surge ends.</summary>
    public event System.Action OnSurgeEnd;

    // ── Receiver registry ─────────────────────────────────────────────────────

    private readonly List<BloodFlowReceiver> _receivers = new();

    /// <summary>
    /// Pending queue — receivers that called Register before Awake ran on this
    /// manager are stored here and flushed on Start.
    /// </summary>
    private static readonly List<BloodFlowReceiver> s_pending = new();

    public void Register(BloodFlowReceiver r)
    {
        if (!_receivers.Contains(r))
            _receivers.Add(r);
    }

    public void Unregister(BloodFlowReceiver r) => _receivers.Remove(r);

    /// <summary>
    /// Called by BloodFlowReceiver when Instance is not yet available.
    /// Queued receivers are flushed in Start().
    /// </summary>
    public static void RegisterPending(BloodFlowReceiver r)
    {
        if (!s_pending.Contains(r))
            s_pending.Add(r);
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        
    }

    private void Start()
    {
        _timer = _surgeInterval;

        // Flush any receivers that registered before this manager was ready
        foreach (var r in s_pending)
            if (r != null) Register(r);
        s_pending.Clear();
    }

    /// <summary>
    /// Restarts the idle countdown, so the next surge is a full _surgeInterval
    /// away. Called by TutorialManager when it hands control to the player: the
    /// timer runs from scene start regardless of the tutorial, so without this
    /// the player's first-ever surge could land seconds after the last card —
    /// or have already been spent, unseen, while receivers were still disabled.
    /// </summary>
    public void ResetSurgeTimer()
    {
        if (_isSurging) EndSurge();
        _timer = _surgeInterval;
    }

    private void Update()
    {
        _timer -= Time.deltaTime;

        if (!_isSurging && _timer <= 0f)
        {
            StartSurge();
        }
        else if (_isSurging && _timer <= 0f)
        {
            EndSurge();
        }
    }

    private void FixedUpdate()
    {
        if (!_isSurging) return;

        // Push every registered receiver downward
        Vector2 force = Vector2.down * _surgeForce;
        for (int i = _receivers.Count - 1; i >= 0; i--)
        {
            if (_receivers[i] == null) { _receivers.RemoveAt(i); continue; }
            _receivers[i].ApplySurgeForce(force);
        }
    }

    // ── Surge control ─────────────────────────────────────────────────────────

    private void StartSurge()
    {
        _isSurging = true;
        _timer     = _surgeDuration;

        // Tell all CellWander scripts to snap to surge speed
        foreach (var cell in Object.FindObjectsByType<CellWander>(FindObjectsSortMode.None))
            cell.SetSurging(true, _cellSurgeMultiplier);

        OnSurgeStart?.Invoke();
        Debug.Log("[BloodFlowManager] Surge START");
    }

    private void EndSurge()
    {
        _isSurging = false;
        _timer     = _surgeInterval;

        // Snap cells back to base speed
        foreach (var cell in Object.FindObjectsByType<CellWander>(FindObjectsSortMode.None))
            cell.SetSurging(false, 1f);

        OnSurgeEnd?.Invoke();
        Debug.Log("[BloodFlowManager] Surge END");
    }
}