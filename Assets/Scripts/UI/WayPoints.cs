using UnityEngine;

/// <summary>
/// Compass-style guide arrow: orbits around the player at a fixed radius,
/// sitting on the side of the player that faces the current objective, and
/// pointing at it. Handles both level layouts:
///
///   LEVEL 1 (nests, linear) — assign the nest transforms to _waypoints in the
///   same order as LevelProgression's points array. The arrow targets the first
///   one that still exists; a destroyed nest leaves a null Transform behind, so
///   the arrow automatically moves on to the next.
///
///   LEVEL 2 (chambers, non-linear) — chambers aren't destroyed when beaten,
///   they just flip IsCleared, and the player can tackle them in any order. So
///   instead of walking a fixed sequence, the arrow re-picks the CLOSEST
///   uncleared chamber every frame and turns to follow it as the player moves.
///   Inside an arena the compass changes job: it hides for the fight (there's
///   nowhere to navigate to, and the other chambers are out on the map), then
///   comes back pointing at that arena's exit door once the wave is cleared, and
///   only returns to hunting chambers after the player walks back out.
///
///   BOSS (one moving target) — points at the boss and tracks it as it patrols.
///
/// Priority is chambers → boss → _waypoints → level exit, and since a scene only
/// ever has one kind of objective the first three never compete. The level exit
/// (<see cref="TriggerNextLevel"/>) is the last leg rather than a rival target:
/// with every other objective done the arrow stays up and walks the player to the
/// exit instead of hiding. It only hides when there's no exit to point at either.
///
/// SETUP:
///   1. Put this on the arrow GameObject (a world-space sprite, NOT a child of
///      the player — the script positions it every frame).
///   2. Assign _player.
///   3. Level 1: drag the enemy-nest transforms into _waypoints, in activation
///      order. Level 2 and the Boss level: leave everything empty — the chambers
///      and the boss are found automatically.
///   4. Set _orbitRadius to how far from the player the arrow floats.
///   5. Set _angleOffset to match the arrow sprite: 0 if the sprite points
///      right at rotation 0, 90 if it points up, etc.
/// </summary>
public class WayPoints : MonoBehaviour
{
    [Header("Targets — Level 1 (nests)")]
    [Tooltip("Enemy nest transforms in activation order (nest 1 → nest 4). Destroyed nests are skipped automatically.")]
    [SerializeField] private Transform[] _waypoints = new Transform[4];

    [Header("Targets — Level 2 (chambers)")]
    [Tooltip("Leave empty to auto-find every Chamber in the scene. When any are present they take " +
             "priority over _waypoints, and the arrow points at whichever uncleared one is nearest.")]
    [SerializeField] private Chamber[] _chambers;

    [Tooltip("A different chamber has to be at least this many world units closer before the arrow " +
             "switches to it — stops the arrow flip-flopping when two are roughly equidistant.")]
    [SerializeField] private float _targetSwitchMargin = 1.5f;

    [Header("Target — Level exit")]
    [Tooltip("Where the arrow points once every other objective is done: the level-exit trigger. " +
             "Leave empty to auto-find the scene's TriggerNextLevel.")]
    [SerializeField] private Transform _levelExit;

    [Header("Targets — Boss level")]
    [Tooltip("Leave empty to auto-find the Boss in the scene. The arrow tracks it live as it patrols, " +
             "and hides once it's dead.")]
    [SerializeField] private Boss _boss;

    [Header("Compass")]
    [Tooltip("The player the arrow orbits around.")]
    [SerializeField] private Transform _player;
    [Tooltip("Distance from the player at which the arrow floats.")]
    [SerializeField] private float _orbitRadius = 1.5f;
    [Tooltip("Degrees per second the arrow swings around the player toward the target direction. 0 = snap instantly.")]
    [SerializeField] private float _rotateSpeed = 360f;
    [Tooltip("Degrees to subtract so the sprite's forward matches the target direction: 0 = sprite points right, 90 = sprite points up.")]
    [SerializeField] private float _angleOffset = 0f;

    private int     _currentIndex;
    private float   _currentAngle;      // current compass bearing in degrees
    private bool    _angleInitialized;
    private bool    _guidingEnabled;    // stays false during the tutorial
    private Chamber _currentChamber;    // held between frames for the switch margin
    private Renderer[] _renderers;      // toggled instead of deactivating this GameObject
    private bool    _isVisible;

    /// <summary>
    /// Enables the guide arrow. The arrow is invisible and inert until this is
    /// called — the tutorial calls it once, after showing the "follow the arrow"
    /// image, so the compass never appears mid-tutorial.
    /// </summary>
    public void EnableGuiding()
    {
        _guidingEnabled = true;
    }

    private void Awake()
    {
        // Hiding used to be gameObject.SetActive(false), which made the arrow
        // dependent on someone else switching it back on: an inactive GameObject
        // runs no Update and no Start, so if EnableGuiding() never arrived (a
        // scene with no TutorialManager, or one where the tutorial can't finish)
        // the arrow was stuck off for good and couldn't recover by itself.
        // Toggling the renderers instead keeps this component alive and in
        // charge of its own visibility.
        _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        SetVisible(false);
    }

    /// <summary>
    /// Runs on the first frame — after every Chamber has woken up — so
    /// auto-discovery is safe here and saves wiring Level 2's four chambers by
    /// hand. Level 1 has no Chambers, so this leaves the array empty and the
    /// nest waypoints take over.
    /// </summary>
    private void Start()
    {
        if (_chambers == null || _chambers.Length == 0)
            _chambers = FindObjectsByType<Chamber>(FindObjectsSortMode.None);

        if (_boss == null) _boss = FindFirstObjectByType<Boss>();

        // TriggerNextLevel keeps itself disabled until the chambers are cleared,
        // and FindFirstObjectByType skips disabled components — so search with
        // Include and take the transform, which is all the arrow needs. The
        // gating is TriggerNextLevel's business; the arrow only points at it
        // once every other objective is done anyway.
        if (_levelExit == null)
        {
            var exit = FindObjectsByType<TriggerNextLevel>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None);
            if (exit.Length > 0) _levelExit = exit[0].transform;
        }
    }

    private void SetVisible(bool visible)
    {
        if (_renderers == null) return;

        // Coming back from hidden (e.g. out of a chamber fight) the old bearing is
        // stale — usually pointing at wherever the objective was before. Drop it so
        // the arrow snaps onto the new target instead of visibly swinging around.
        if (visible && !_isVisible) _angleInitialized = false;
        _isVisible = visible;

        foreach (Renderer r in _renderers)
            if (r != null) r.enabled = visible;
    }

    private void Update()
    {
        if (!_guidingEnabled)
        {
            // Wait only while a tutorial is genuinely still running. Polling
            // IsComplete rather than relying purely on the EnableGuiding() call
            // is what lets the arrow work in Level 2, where the tutorial has
            // usually already been completed back in Level 1 — and keeps it
            // working in any scene that has no TutorialManager at all.
            if (TutorialManager.Instance != null && !TutorialManager.Instance.IsComplete)
            {
                SetVisible(false);
                return;
            }
            _guidingEnabled = true;
        }

        Transform target = GetCurrentTarget();

        if (target == null || _player == null)
        {
            // Every objective is done (or no player) — nothing to point at.
            SetVisible(false);
            return;
        }

        SetVisible(true);

        Vector3 dir = target.position - _player.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // Swing smoothly around the player instead of snapping, so nest
        // handovers read as the compass turning to the new target.
        if (!_angleInitialized)
        {
            _currentAngle = targetAngle;
            _angleInitialized = true;
        }
        else
        {
            _currentAngle = _rotateSpeed > 0f
                ? Mathf.MoveTowardsAngle(_currentAngle, targetAngle, _rotateSpeed * Time.deltaTime)
                : targetAngle;
        }

        // Position on the orbit circle around the player…
        float rad = _currentAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * _orbitRadius;
        transform.position = _player.position + offset;

        // …and face outward along that same bearing.
        transform.rotation = Quaternion.Euler(0f, 0f, _currentAngle - _angleOffset);
    }

    /// <summary>
    /// This scene's current objective: nearest uncleared chamber, else the boss,
    /// else the next surviving nest waypoint — and once all of those are done,
    /// the level exit. Null only when there's no exit to fall back on either.
    /// </summary>
    private Transform GetCurrentTarget()
    {
        // Level 2: while the player is inside an arena, the overworld compass is
        // meaningless — every chamber (including the one they're standing in) sits
        // out on the map, so pointing at the nearest one just aims the player at a
        // wall. Hide the arrow for the fight, then bring it back aimed at the exit
        // door once the wave is down, until they walk out through it.
        Chamber insideArena = GetChamberInProgress();
        if (insideArena != null)
            return insideArena.IsCleared ? insideArena.ExitDoor : null;

        Transform chamber = GetNearestUnclearedChamber();
        if (chamber != null) return chamber;

        // Boss level: a single moving target. Enemy.Die() destroys the boss, so
        // once it's dead this reads as Unity's destroyed-object null and the
        // arrow hides itself — no explicit "boss defeated" hook needed.
        if (_boss != null) return _boss.transform;

        // Level 1: first waypoint whose nest still exists. Destroyed nests leave
        // a null Transform behind (Unity's destroyed-object null), so we just
        // walk past them; _currentIndex only ever moves forward.
        while (_currentIndex < _waypoints.Length)
        {
            Transform wp = _waypoints[_currentIndex];
            if (wp != null) return wp;
            _currentIndex++;
        }

        // Nothing left to fight — the last chamber is cleared and the player has
        // walked back out into the overworld. The arrow stays on screen and turns
        // into a signpost for the level exit instead of switching itself off.
        return _levelExit;
    }

    /// <summary>
    /// The chamber the player is currently shut inside, if any: from walking into
    /// the trigger (IsActive) right through to leaving via the exit door
    /// (IsCleared but not yet HasExited). Null while they're out in the overworld.
    /// </summary>
    private Chamber GetChamberInProgress()
    {
        if (_chambers == null) return null;

        foreach (Chamber chamber in _chambers)
        {
            if (chamber == null) continue;
            if (chamber.IsActive || (chamber.IsCleared && !chamber.HasExited)) return chamber;
        }
        return null;
    }

    /// <summary>
    /// Closest chamber the player still has to clear. Re-evaluated every frame
    /// rather than fixed up front, because Level 2 is open — the player can head
    /// for any chamber, and the arrow should follow whichever they're closest to.
    /// </summary>
    private Transform GetNearestUnclearedChamber()
    {
        if (_chambers == null || _chambers.Length == 0 || _player == null) return null;

        Chamber nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Chamber chamber in _chambers)
        {
            if (chamber == null || chamber.IsCleared) continue;

            float distance = Vector3.Distance(chamber.transform.position, _player.position);
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearest = chamber;
        }

        // Stay on the current target unless the new pick is clearly closer, so
        // crossing the midpoint between two chambers doesn't make the arrow
        // oscillate between them.
        if (_currentChamber != null && _currentChamber != nearest && !_currentChamber.IsCleared)
        {
            float currentDistance = Vector3.Distance(_currentChamber.transform.position, _player.position);
            if (currentDistance <= nearestDistance + _targetSwitchMargin)
                nearest = _currentChamber;
        }

        _currentChamber = nearest;
        return nearest != null ? nearest.transform : null;
    }
}
