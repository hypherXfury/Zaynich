using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Boss scene has no LevelProgression (no nests/chambers), so the UI slot
/// that would normally show this level's remaining infection is unused there
/// — this repurposes it to show the boss's own health instead.
///
/// SETUP: attach to any GameObject in the Boss scene (e.g. the BossHealth
/// GameObject under the Canvas), then assign:
///   _boss    -> the Boss's Enemy component
///   _fillBar -> HealthFillBar's Image (Image Type = Filled)
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [SerializeField] private Enemy _boss;
    [SerializeField] private Image _fillBar;

    void OnEnable()
    {
        if (_boss != null) _boss.OnDeath += HandleBossDeath;
    }

    void OnDisable()
    {
        if (_boss != null) _boss.OnDeath -= HandleBossDeath;
    }

    void Update()
    {
        if (_boss == null || _fillBar == null || _boss.MaxHealth <= 0f) return;
        _fillBar.fillAmount = Mathf.Clamp01(_boss.CurrentHealth / _boss.MaxHealth);
    }

    /// <summary>
    /// Destroy(gameObject) flags _boss as "destroyed" (Unity's fake-null)
    /// immediately, before the object is actually removed at end of frame —
    /// so if Die() runs before this script's Update() in the same frame,
    /// Update() bails on the null check and the bar freezes at whatever
    /// health was left just before the killing blow instead of hitting 0.
    /// Snapping it here, straight off the death event, avoids that race.
    /// </summary>
    private void HandleBossDeath(Enemy enemy)
    {
        if (_fillBar != null) _fillBar.fillAmount = 0f;
    }
}
