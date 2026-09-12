using System.Collections.Generic;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    private const string SaveKey = "Progress";

    [SerializeField] private List<string> progress = new List<string>();

    public static SaveManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddProgress(string tag)
    {
        if (!progress.Contains(tag))
            progress.Add(tag);
    }

    public void RemoveProgress(string tag)
    {
        progress.Remove(tag);
    }

    public bool HasProgress(string tag) => progress.Contains(tag);

    // Read-only view of every tag currently held — used by SaveLoadManager to snapshot
    // the whole progress set into its own save file, without SaveManager needing to
    // know anything about scenes, Yarn variables, or puzzle state.
    public IReadOnlyList<string> AllProgress => progress;

    // Replaces the whole tag set at once — used by SaveLoadManager when restoring a
    // save, so a loaded game doesn't just ADD to whatever was already here (e.g. from
    // New Game having freshly started this scene).
    public void SetAllProgress(IEnumerable<string> tags)
    {
        progress.Clear();
        if (tags != null) progress.AddRange(tags);
    }

    public void SaveGame()
    {
        PlayerPrefs.SetString(SaveKey, string.Join(",", progress));
        PlayerPrefs.Save();
    }

    public void LoadGame()
    {
        string saved = PlayerPrefs.GetString(SaveKey, "");
        progress.Clear();
        if (!string.IsNullOrEmpty(saved))
            progress.AddRange(saved.Split(','));
    }
}
