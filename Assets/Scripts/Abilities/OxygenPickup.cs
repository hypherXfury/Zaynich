using UnityEngine;

/// <summary>
/// Oxygen pickup — collected by the player to restore Oxygen.
/// Attach to your existing oxygen prefab alongside a trigger Collider2D.
///
/// SETUP:
///   1. Add this component to your oxygen pickup prefab.
///   2. Make sure the prefab's Collider2D has Is Trigger = true.
///   3. Set _oxygenAmount to how much oxygen this pickup restores.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class OxygenPickup : MonoBehaviour
{
    [Tooltip("Amount of Oxygen restored when collected (0–100 scale).")]
    [SerializeField] private float _oxygenAmount = 8f;

    [Tooltip("Optional pickup VFX/sound trigger hook — left empty if not needed.")]
    [SerializeField] private GameObject _collectEffectPrefab;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip audioClip;

    private bool _collected;

    /// <summary>
    /// True once the player has actually walked over this pickup. The tutorial
    /// waits on this rather than on the GameObject disappearing — a pickup that
    /// despawns on its own would otherwise read as "lesson learned".
    /// </summary>
    public bool Collected => _collected;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected) return;

        var playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        _collected = true;

        if (VitalsManager.Instance != null)
            VitalsManager.Instance.CollectOxygen(_oxygenAmount);

        if (_collectEffectPrefab != null)
            Instantiate(_collectEffectPrefab, transform.position, Quaternion.identity);

        if(audioClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(audioClip);
        }

        Destroy(gameObject, 1f);
     }

   
}