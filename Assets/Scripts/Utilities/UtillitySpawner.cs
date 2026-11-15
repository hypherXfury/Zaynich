using UnityEngine;

public class UtillitySpawner : MonoBehaviour
{
    public static UtillitySpawner Instance;
    [SerializeField] private GameObject XP;
    [SerializeField] private GameObject oxygenBubble;
    [SerializeField] private float bubble_chance;
    [SerializeField] private Transform parent;

    void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Spawn(float chance, Transform pos)
    {
        Debug.Log(chance);
        if(chance > bubble_chance)
            Instantiate(oxygenBubble, pos.position, Quaternion.identity, parent);
        // else
        //     Instantiate(XP, pos.position, Quaternion.identity, parent);
    }

    /// <summary>Guarantees an oxygen bubble at <paramref name="pos"/> and returns the spawned instance.</summary>
    public GameObject SpawnOxygen(Vector3 pos)
    {
        return Instantiate(oxygenBubble, pos, Quaternion.identity, parent);
    }
}
