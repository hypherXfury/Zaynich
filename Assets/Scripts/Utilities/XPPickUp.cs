using UnityEngine;

public class XPPickUp : MonoBehaviour
{
    [SerializeField] private float xpAmount = 5f;

    void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("Player"))
        {
            FindFirstObjectByType<PlayerProgression>().AddXP(xpAmount);
            Destroy(gameObject);
        }
    }
}
