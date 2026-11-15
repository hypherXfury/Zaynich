using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sits on the GO with the level-exit trigger Collider2D. The script stays disabled
/// (inert to trigger events) until every assigned arena <see cref="Chamber"/> is cleared,
/// at which point it enables itself. Player then walks through the trigger, the screen
/// fades out, and the next scene loads — mirroring <see cref="Chamber"/>'s own
/// fade-transition convention.
///
/// SETUP:
///   1. Place this on the TriggerNextLevel GameObject, alongside its trigger Collider2D.
///      The GameObject itself stays active — only this script's enabled state is gated,
///      so it can listen for chambers clearing from scene start.
///   2. Assign _chambers to all arena chambers that must be cleared to unlock the exit.
///   3. Assign _nextLevelSceneName to the exact scene name registered in Build Settings.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TriggerNextLevel : MonoBehaviour
{
    [SerializeField] private string _nextLevelSceneName;
    [SerializeField] private float _fadeDuration = 0.5f;

    [Header("Unlock Condition")]
    [Tooltip("This script stays disabled until every chamber here is cleared.")]
    [SerializeField] private Chamber[] _chambers = new Chamber[4];

    private bool _triggered;

    private void Awake()
    {
        foreach (Chamber chamber in _chambers)
            if (chamber != null) chamber.OnChamberCleared += HandleChamberCleared;

        enabled = AllChambersCleared();
    }

    private void OnDestroy()
    {
        foreach (Chamber chamber in _chambers)
            if (chamber != null) chamber.OnChamberCleared -= HandleChamberCleared;
    }

    private void HandleChamberCleared(Chamber chamber)
    {
        if (AllChambersCleared()) enabled = true;
    }

    private bool AllChambersCleared()
    {
        foreach (Chamber chamber in _chambers)
            if (chamber == null || !chamber.IsCleared) return false;
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered) return;
        if (other.GetComponent<PlayerHealth>() == null) return;

        if (string.IsNullOrEmpty(_nextLevelSceneName))
        {
            Debug.LogWarning($"[TriggerNextLevel] {name} has no _nextLevelSceneName assigned.");
            return;
        }

        _triggered = true;
        GetComponent<Collider2D>().enabled = false;
        StartCoroutine(LoadNextLevelRoutine());
    }

    private IEnumerator LoadNextLevelRoutine()
    {
        yield return StartCoroutine(ScreenFader.Instance.FadeOut(_fadeDuration));
        SceneManager.LoadScene(_nextLevelSceneName);
    }
}
