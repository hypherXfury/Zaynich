using UnityEngine;

/// <summary>
/// Lightweight tag component added automatically by SpriteSpawner.
/// Stores which prefab index this GameObject was created from so it
/// can be returned to the correct object pool when recycled.
/// You do NOT need to add this manually – the spawner handles it.
/// </summary>
public class PoolTag : MonoBehaviour
{
    [HideInInspector]
    public int prefabIndex = 0;
}