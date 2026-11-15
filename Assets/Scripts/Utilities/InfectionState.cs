/// <summary>
/// Runtime-only Bloodstream Infection percentage, shared across all level scenes
/// for the current play session. Starts at 100 and is reduced by LevelProgression
/// (by 100/3, by default) each time a level's set of nests/chambers is fully
/// cleared, so it reaches roughly 0 after Level 1 and Level 2 are completed —
/// Boss.cs then snaps it to exactly 0 when the boss is killed.
///
/// This is a plain static class, not a MonoBehaviour/singleton — static fields
/// already survive SceneManager.LoadScene within the same play session, so no
/// DontDestroyOnLoad object is needed. It resets only on a fresh Play session
/// (editor) or when ResetForNewGame() is called (e.g. from the main menu's
/// Start button) so a new playthrough always begins at 100%.
/// </summary>
public static class InfectionState
{
    public const float StartingValue = 100f;

    public static float Value = StartingValue;

    public static void ResetForNewGame() => Value = StartingValue;
}
