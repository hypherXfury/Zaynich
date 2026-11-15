using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach this to an empty GameObject in your scene.
/// Assign your sprite prefabs to the `spritePrefabs` array in the Inspector.
/// The system will:
///   1. Fill the camera bounds with randomly placed sprites on Start.
///   2. As the player moves, recycle off-screen sprites and spawn new ones
///      ahead of the player using object pooling.
/// </summary>
public class SpriteSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag your player GameObject here.")]
    public Transform player;

    [Tooltip("The camera used to determine visible bounds. Defaults to Camera.main.")]
    public Camera cam;

    [Header("Sprite Prefabs")]
    [Tooltip("All sprite prefabs that can be spawned. They will be chosen randomly.")]
    public GameObject[] spritePrefabs;

    [Header("Spawn Settings")]
    [Tooltip("How many sprites to keep alive around the player at all times.")]
    public int targetCount = 30;

    [Tooltip("Minimum distance between any two sprites.")]
    public float minSpacing = 1.0f;

    [Tooltip("Extra margin beyond the camera edge to keep sprites alive (world units).")]
    public float cameraMargin = 1.5f;

    [Tooltip("Extra margin beyond the camera edge where new sprites are spawned (world units). Should be >= cameraMargin.")]
    public float spawnMargin = 2.5f;

    [Tooltip("Maximum attempts to find a valid (non-overlapping) spawn position per frame.")]
    public int maxPlacementAttempts = 10;

    // ── Object Pool ────────────────────────────────────────────────────────────
    // One pool list per prefab index so we return sprites to their correct pool.
    private List<List<GameObject>> pools;

    // All currently active sprites
    private List<GameObject> activeSprites = new List<GameObject>();

    // ── Internal state ─────────────────────────────────────────────────────────
    private Vector3 lastPlayerPos;

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        if (cam == null) cam = Camera.main;

        // Build one pool per prefab
        pools = new List<List<GameObject>>(spritePrefabs.Length);
        for (int i = 0; i < spritePrefabs.Length; i++)
            pools.Add(new List<GameObject>());
    }

    void Start()
    {
        if (player == null)
        {
            Debug.LogError("[SpriteSpawner] No player Transform assigned!");
            return;
        }

        lastPlayerPos = player.position;
        FillInitial();
    }

    void LateUpdate()
    {
        if (player == null) return;

        RecycleOffScreen();
        FillToTarget();

        lastPlayerPos = player.position;
    }

    // ── Initial fill ───────────────────────────────────────────────────────────

    /// <summary>Populate the camera view with sprites at startup.</summary>
    void FillInitial()
    {
        Bounds camBounds = GetCameraBounds(spawnMargin);

        for (int i = 0; i < targetCount; i++)
        {
            Vector2 pos;
            if (TryGetRandomPosition(camBounds, out pos))
                SpawnAt(pos);
        }
    }

    // ── Per-frame logic ────────────────────────────────────────────────────────

    /// <summary>
    /// Return any sprite that has moved outside the keep-alive margin to its pool.
    /// </summary>
    void RecycleOffScreen()
    {
        Bounds keepBounds = GetCameraBounds(cameraMargin);

        for (int i = activeSprites.Count - 1; i >= 0; i--)
        {
            GameObject go = activeSprites[i];
            if (go == null) { activeSprites.RemoveAt(i); continue; }

            if (!keepBounds.Contains(go.transform.position))
            {
                Recycle(go);
                activeSprites.RemoveAt(i);
            }
        }
    }

    /// <summary>Spawn new sprites until we reach targetCount.</summary>
    void FillToTarget()
    {
        if (activeSprites.Count >= targetCount) return;

        // Determine the direction the player is moving so we bias spawns ahead.
        Vector3 moveDir = (player.position - lastPlayerPos);

        // Extended spawn bounds – a strip just outside the camera on the leading edge.
        Bounds spawnBounds = GetCameraBounds(spawnMargin);

        int needed = targetCount - activeSprites.Count;
        int spawned = 0;

        for (int attempt = 0; attempt < needed * maxPlacementAttempts && spawned < needed; attempt++)
        {
            Vector2 pos;
            if (TryGetSpawnPosition(spawnBounds, moveDir, out pos))
            {
                SpawnAt(pos);
                spawned++;
            }
        }
    }

    // ── Spawn / Recycle helpers ────────────────────────────────────────────────

    void SpawnAt(Vector2 worldPos)
    {
        int prefabIndex = Random.Range(0, spritePrefabs.Length);
        GameObject go = GetFromPool(prefabIndex);

        go.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
        go.SetActive(true);

        // Store prefab index on the object so we know which pool it belongs to.
        PoolTag tag = go.GetComponent<PoolTag>();
        if (tag == null) tag = go.AddComponent<PoolTag>();
        tag.prefabIndex = prefabIndex;

        activeSprites.Add(go);
    }

    void Recycle(GameObject go)
    {
        go.SetActive(false);

        PoolTag tag = go.GetComponent<PoolTag>();
        int index = (tag != null) ? tag.prefabIndex : 0;
        pools[index].Add(go);
    }

    GameObject GetFromPool(int prefabIndex)
    {
        List<GameObject> pool = pools[prefabIndex];

        if (pool.Count > 0)
        {
            GameObject go = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            return go;
        }

        // Pool is empty – instantiate a new one.
        return Instantiate(spritePrefabs[prefabIndex], transform);
    }

    // ── Position helpers ───────────────────────────────────────────────────────

    /// <summary>Find a random non-overlapping position inside bounds.</summary>
    bool TryGetRandomPosition(Bounds bounds, out Vector2 result)
    {
        for (int i = 0; i < maxPlacementAttempts; i++)
        {
            Vector2 candidate = new Vector2(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y)
            );

            if (!IsTooClose(candidate))
            {
                result = candidate;
                return true;
            }
        }

        result = Vector2.zero;
        return false;
    }

    /// <summary>
    /// Like TryGetRandomPosition but biased towards the leading edge of movement
    /// so new sprites appear just off-screen ahead of the player.
    /// </summary>
    bool TryGetSpawnPosition(Bounds spawnBounds, Vector3 moveDir, out Vector2 result)
    {
        // If the player isn't really moving, fall back to a random position.
        if (moveDir.sqrMagnitude < 0.0001f)
            return TryGetRandomPosition(spawnBounds, out result);

        Vector2 dir2D = new Vector2(moveDir.x, moveDir.y).normalized;

        // Camera bounds without extra margin – the visible area.
        Bounds camBounds = GetCameraBounds(0f);

        for (int i = 0; i < maxPlacementAttempts; i++)
        {
            // Sample a random point in the full spawn bounds.
            Vector2 candidate = new Vector2(
                Random.Range(spawnBounds.min.x, spawnBounds.max.x),
                Random.Range(spawnBounds.min.y, spawnBounds.max.y)
            );

            // Prefer points that are in the direction of movement and outside camera.
            Vector2 toCandidate = candidate - new Vector2(player.position.x, player.position.y);
            bool isAhead = Vector2.Dot(toCandidate.normalized, dir2D) > 0;
            bool isOutsideCam = !camBounds.Contains(new Vector3(candidate.x, candidate.y, 0f));

            if (isAhead && isOutsideCam && !IsTooClose(candidate))
            {
                result = candidate;
                return true;
            }
        }

        // Fallback: any valid position inside spawn bounds.
        return TryGetRandomPosition(spawnBounds, out result);
    }

    bool IsTooClose(Vector2 candidate)
    {
        float minSqr = minSpacing * minSpacing;
        foreach (GameObject go in activeSprites)
        {
            if (go == null) continue;
            Vector2 p = go.transform.position;
            if ((p - candidate).sqrMagnitude < minSqr)
                return true;
        }
        return false;
    }

    // ── Camera bounds ──────────────────────────────────────────────────────────

    Bounds GetCameraBounds(float margin)
    {
        float height = 2f * cam.orthographicSize;
        float width  = height * cam.aspect;

        Vector3 center = cam.transform.position;
        center.z = 0f;

        return new Bounds(center, new Vector3(width + margin * 2f, height + margin * 2f, 0f));
    }
}