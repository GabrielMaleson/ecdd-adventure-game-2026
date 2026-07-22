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
