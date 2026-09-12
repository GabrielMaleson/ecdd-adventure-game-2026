using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The actual "save the game" / "load the game" orchestrator that TitleMenuManager
// (and, in-game, a pause menu's Save button) calls. SaveManager only ever stored a
// flat list of progress tags — it doesn't know what scene you're in, doesn't touch
// Yarn variables, and doesn't know a puzzle exists. This is the layer that DOES,
// built on top of SaveManager rather than replacing it.
//
// What "remember everything" means here, concretely — a save records:
//   1. Which scene you were in.
//   2. Every SaveManager progress tag (delegates to SaveManager.AllProgress).
//   3. Every Yarn variable (via VariableStorageBehaviour.GetAllVariables/
//      SetAllVariables, which Yarn Spinner ships specifically for this).
//   4. The current CELL of every PushableCrate and GridObstacle in the scene, so a
//      save made mid-puzzle (some crates moved, a statue partway through turning
//      things) resumes exactly where it was — not just "puzzle solved or not".
//
// What it deliberately does NOT record: NPC formation/follow state, mid-cutscene
// position, exact player position within a scene. Those aren't self-contained data —
// DebugSceneJump's own notes are the reason why (a beat is progress + Yarn vars +
// which objects are enabled + who's following whom + a scripted formation, and
// replaying only some of that produces a scene that LOOKS right and behaves wrong).
// The existing progress-gated systems (ProgressGate, DialogueStarter conditions,
// Pickup's progressoAoColetar, NpcEntrance/NpcGather) already reconstruct the right
// world state from progress tags as the scene starts, the same way DebugSceneJump's
// jumps do it for testing — so leaning on that, rather than snapshotting every
// object's position, is what keeps this from becoming a second, parallel state
// system that can drift out of sync with the real one.
//
// Self-bootstrapping: no scene needs to place this by hand. It needs to be reachable
// from the title screen (New Game / Load Game) AND from an in-game pause menu (Save),
// i.e. before AND after the first real gameplay scene loads — a RuntimeInitializeOnLoad
// hook guarantees it exists from the very first frame, in every scene, with nothing to
// wire in the Editor.
public class SaveLoadManager : MonoBehaviour
{
    private const string SaveKey = "GameSave";

    [Tooltip("Scene loaded by New Game.")]
    [SerializeField] private string startingScene = "MainScene";

    public static SaveLoadManager Instance { get; private set; }

    [System.Serializable]
    private class StringEntry { public string key; public string value; }

    [System.Serializable]
    private class FloatEntry { public string key; public float value; }

    [System.Serializable]
    private class BoolEntry { public string key; public bool value; }

    [System.Serializable]
    private class PuzzleObjectEntry { public string name; public int cellX; public int cellY; }

    [System.Serializable]
    private class SaveData
    {
        public string sceneName;
        public List<string> progressTags = new List<string>();
        public List<FloatEntry> yarnFloats = new List<FloatEntry>();
        public List<StringEntry> yarnStrings = new List<StringEntry>();
        public List<BoolEntry> yarnBools = new List<BoolEntry>();
        public List<PuzzleObjectEntry> puzzleObjects = new List<PuzzleObjectEntry>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("SaveLoadManager");
        go.AddComponent<SaveLoadManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool SaveExists() => PlayerPrefs.HasKey(SaveKey);

    // Loads the starting scene fresh. Doesn't touch any existing save file — a New
    // Game only overwrites the old save once the player actually saves again, so
    // clicking New Game by mistake can't destroy progress they meant to keep.
    public void NewGame()
    {
        SceneManager.LoadScene(startingScene);
    }

    public void SaveGame()
    {
        var data = new SaveData
        {
            sceneName = SceneManager.GetActiveScene().name
        };

        if (SaveManager.Instance != null)
            data.progressTags.AddRange(SaveManager.Instance.AllProgress);

        var runner = DialogueManager.Instance != null ? DialogueManager.Instance.dialogueRunner : null;
        if (runner != null && runner.VariableStorage != null)
        {
            var (floats, strings, bools) = runner.VariableStorage.GetAllVariables();
            foreach (var kv in floats) data.yarnFloats.Add(new FloatEntry { key = kv.Key, value = kv.Value });
            foreach (var kv in strings) data.yarnStrings.Add(new StringEntry { key = kv.Key, value = kv.Value });
            foreach (var kv in bools) data.yarnBools.Add(new BoolEntry { key = kv.Key, value = kv.Value });
        }
        else
        {
            Debug.LogWarning("[SaveLoadManager] No DialogueRunner/VariableStorage found — Yarn variables were not saved.");
        }

        foreach (var crate in FindObjectsByType<PushableCrate>(FindObjectsSortMode.None))
            data.puzzleObjects.Add(new PuzzleObjectEntry { name = crate.name, cellX = crate.Cell.x, cellY = crate.Cell.y });

        foreach (var obstacle in FindObjectsByType<GridObstacle>(FindObjectsSortMode.None))
            data.puzzleObjects.Add(new PuzzleObjectEntry { name = obstacle.name, cellX = obstacle.Cell.x, cellY = obstacle.Cell.y });

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();

        Debug.Log($"[SaveLoadManager] Saved '{data.sceneName}' — {data.progressTags.Count} progress tag(s), " +
                  $"{data.yarnFloats.Count + data.yarnStrings.Count + data.yarnBools.Count} Yarn variable(s), " +
                  $"{data.puzzleObjects.Count} puzzle object(s).");
    }

    public void LoadGame()
    {
        if (!SaveExists())
        {
            Debug.LogWarning("[SaveLoadManager] LoadGame called but there is no save file.");
            return;
        }

        SaveData data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
        if (data == null || string.IsNullOrEmpty(data.sceneName))
        {
            Debug.LogError("[SaveLoadManager] Save file is corrupt or empty — can't load.");
            return;
        }

        StartCoroutine(LoadRoutine(data));
    }

    private IEnumerator LoadRoutine(SaveData data)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(data.sceneName);
        while (op != null && !op.isDone)
            yield return null;

        // One frame for the new scene's own Awake/Start (SaveManager, DialogueManager,
        // PuzzleGrid, every crate/obstacle claiming its authored cell) to finish before
        // this overwrites any of it.
        yield return null;

        if (SaveManager.Instance != null)
            SaveManager.Instance.SetAllProgress(data.progressTags);
        else
            Debug.LogWarning("[SaveLoadManager] No SaveManager in the loaded scene — progress tags were not restored.");

        var runner = DialogueManager.Instance != null ? DialogueManager.Instance.dialogueRunner : null;
        if (runner != null && runner.VariableStorage != null)
        {
            var floats = new Dictionary<string, float>();
            var strings = new Dictionary<string, string>();
            var bools = new Dictionary<string, bool>();
            foreach (var e in data.yarnFloats) floats[e.key] = e.value;
            foreach (var e in data.yarnStrings) strings[e.key] = e.value;
            foreach (var e in data.yarnBools) bools[e.key] = e.value;

            runner.VariableStorage.SetAllVariables(floats, strings, bools);
        }
        else
        {
            Debug.LogWarning("[SaveLoadManager] No DialogueRunner/VariableStorage in the loaded scene — Yarn variables were not restored.");
        }

        RestorePuzzleObjects(data.puzzleObjects);

        Debug.Log($"[SaveLoadManager] Loaded '{data.sceneName}'.");
    }

    // Two passes, deliberately — vacate every saved object's CURRENT cell first, then
    // move and reclaim. Restoring one at a time can transiently "claim" a cell another
    // saved object hasn't vacated yet (two crates that swapped places, say), which
    // only logs a warning here but is the exact bug class ObstacleGroup's own
    // RestoreMembers avoids the same way.
    private void RestorePuzzleObjects(List<PuzzleObjectEntry> entries)
    {
        if (entries == null || entries.Count == 0) return;

        PuzzleGrid grid = PuzzleGrid.Active;
        if (grid == null || !grid.IsReady)
        {
            Debug.LogWarning("[SaveLoadManager] No usable PuzzleGrid in the loaded scene — puzzle object positions were not restored.");
            return;
        }

        var byName = new Dictionary<string, GridOccupant>();
        foreach (var crate in FindObjectsByType<PushableCrate>(FindObjectsSortMode.None))
            byName[crate.name] = crate;
        foreach (var obstacle in FindObjectsByType<GridObstacle>(FindObjectsSortMode.None))
            byName[obstacle.name] = obstacle;

        var toMove = new List<(GridOccupant occupant, Vector2Int cell)>();
        foreach (var entry in entries)
        {
            if (byName.TryGetValue(entry.name, out GridOccupant occupant))
                toMove.Add((occupant, new Vector2Int(entry.cellX, entry.cellY)));
            else
                Debug.LogWarning($"[SaveLoadManager] Save file mentions puzzle object '{entry.name}', not found in this scene — skipped.");
        }

        foreach (var (occupant, _) in toMove)
            occupant.ClearCell();

        foreach (var (occupant, cell) in toMove)
        {
            Vector3 worldPos = grid.CellCenter(cell) - occupant.VisualOffset;
            worldPos.z = occupant.transform.position.z;
            occupant.transform.position = worldPos;

            Rigidbody2D rb = occupant.GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = worldPos;

            occupant.ReassignCell(cell);
        }

        CrateTarget.EvaluateWin();
    }
}
