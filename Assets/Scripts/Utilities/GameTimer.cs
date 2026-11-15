using UnityEngine;

/// <summary>
/// Total elapsed time for the WHOLE playthrough — Level 1 + Level 2 + the Boss
/// fight — not the time spent in the current scene. LevelProgression ticks it
/// during Level 1/2 and Boss.cs ticks it during the boss fight; each of those
/// commits the running total to <see cref="SaveSystem"/> when its level is
/// cleared, so the next scene picks up exactly where the last one left off.
///
/// Callers tick with Time.deltaTime, which is 0 while the game is paused, so
/// the pause menu and the upgrade panel don't inflate the run time.
///
/// Like <see cref="InfectionState"/> this is a plain static class rather than a
/// MonoBehaviour — statics already survive SceneManager.LoadScene within a play
/// session, so no DontDestroyOnLoad object is needed. <see cref="Restore"/> is
/// what covers the cases a static can't: Continue from the main menu, and
/// entering a level scene directly in the editor.
///
/// This total is what the boss-kill win panel reports, and what the leaderboard
/// will eventually submit.
/// </summary>
public static class GameTimer
{
    private static float _elapsed;

    /// <summary>Seconds elapsed across the whole run so far.</summary>
    public static float Elapsed => _elapsed;

    /// <summary>The running total as mm:ss, ready to drop into a UI label.</summary>
    public static string Formatted => Format(_elapsed);

    /// <summary>Zeroes the clock — called alongside the other new-game resets from the main menu.</summary>
    public static void ResetForNewGame() => _elapsed = 0f;

    /// <summary>
    /// Seeds the clock from a saved total. Called at the start of every level so
    /// the count is right whether the player arrived from the previous level, hit
    /// Continue, or opened the scene directly in the editor.
    /// </summary>
    public static void Restore(float seconds) => _elapsed = Mathf.Max(0f, seconds);

    /// <summary>Advances the clock. Pass Time.deltaTime so pauses don't count.</summary>
    public static void Tick(float deltaTime) => _elapsed += Mathf.Max(0f, deltaTime);

    /// <summary>
    /// mm:ss. Minutes deliberately aren't wrapped into hours — a long run just
    /// reads 72:15, which stays sortable for the leaderboard.
    /// </summary>
    public static string Format(float seconds)
    {
        int total = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }
}
