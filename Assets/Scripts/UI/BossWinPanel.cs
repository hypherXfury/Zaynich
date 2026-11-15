using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// End of the Boss level. Waits a beat after the boss dies (and takes its
/// summoned enemies with it — see Boss.KillSpawnedEnemies), fades the screen to
/// black, then loads the Outro scene.
///
/// This used to reveal a "Game Completed" panel here in the Boss scene. That
/// panel now lives at the end of the outro instead: Outro plays the ending
/// cutscene and <see cref="OutroSequence"/> shows the run time afterwards. The
/// Boss scene's old WinPanel object is unused and can be deleted.
///
/// This deliberately does NOT live on the Boss: Enemy.Die() calls
/// Destroy(gameObject) on the boss in the same frame it raises OnDeath, and
/// destroying a GameObject kills its coroutines — the sequence below would be
/// cut off partway through.
///
/// SETUP:
///   1. Put THIS script on something that stays active for the whole fight —
///      the Canvas, or an empty "WinPanel Controller" object.
///   2. Assign _boss -> the Boss's Enemy component.
///   3. Make sure Assets/Scenes/Outro.unity is in Build Settings.
/// </summary>
public class BossWinPanel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Boss's Enemy component — this listens for its OnDeath.")]
    [SerializeField] private Enemy _boss;

    [Header("Scene")]
    [Tooltip("Scene loaded once the screen is black. Must be in Build Settings.")]
    [SerializeField] private string _outroSceneName = "Outro";

    [Header("Timing")]
    [Tooltip("Seconds between the boss dying and the fade to black starting.")]
    [SerializeField] private float _delayAfterDeath = 3.5f;
    [SerializeField] private float _fadeDuration = 0.6f;

    private bool _sequenceStarted;

    private void OnEnable()
    {
        if (_boss != null) _boss.OnDeath += HandleBossDeath;
    }

    private void OnDisable()
    {
        if (_boss != null) _boss.OnDeath -= HandleBossDeath;
    }

    private void HandleBossDeath(Enemy enemy)
    {
        if (_sequenceStarted) return;
        _sequenceStarted = true;

        StartCoroutine(WinSequence());
    }

    private IEnumerator WinSequence()
    {
        // Realtime so the beat still passes if anything else has paused the game.
        yield return new WaitForSecondsRealtime(_delayAfterDeath);

        // ScreenFader builds its own overlay canvas on first use, so the Boss
        // scene needs no fade setup of its own.
        yield return StartCoroutine(ScreenFader.Instance.FadeOut(_fadeDuration));

        // LoadScene does not reset timeScale, so anything that paused the game
        // during the fight would otherwise freeze the outro's own fades.
        Time.timeScale = 1f;

        SceneManager.LoadScene(_outroSceneName);
    }
}
