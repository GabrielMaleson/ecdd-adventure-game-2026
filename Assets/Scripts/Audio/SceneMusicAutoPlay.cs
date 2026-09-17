using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Toca a trilha certa do MusicManager (Assets/Scripts/RPG Ports/MusicManager.cs)
/// sozinho quando a cena muda — sem duplicar aquele sistema nem mexer nele. O
/// MusicManager já existe, já tem AudioSource, mixer group e a lista nomeada
/// musicTracks; isto só decide QUAL nome tocar para cada cena.
///
/// Setup:
///  1. Em MusicManager > Named Music Tracks, cadastre duas entradas:
///     "Overworld" → Ato_1_The_Last_Breath, "Dungeon" → Dungeon.
///  2. Coloque ESTE componente no MESMO GameObject do MusicManager (não em outro
///     objeto — ele não é um singleton próprio, e sobrevive à troca de cena de
///     carona no DontDestroyOnLoad do MusicManager).
/// </summary>
public class SceneMusicAutoPlay : MonoBehaviour
{
    [System.Serializable]
    public class SceneTrack
    {
        public string sceneName;
        public string trackName;
    }

    public List<SceneTrack> sceneTracks = new List<SceneTrack>
    {
        new SceneTrack { sceneName = "MainScene", trackName = "Overworld" },
        new SceneTrack { sceneName = "CryptPuzzle", trackName = "Dungeon" },
    };

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        PlayForScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayForScene(scene.name);
    }

    private void PlayForScene(string sceneName)
    {
        var match = sceneTracks.Find(t => t.sceneName == sceneName);
        if (match != null)
            MusicManager.PlayMusicCommand(match.trackName);
        else
            MusicManager.StopMusicCommand();
    }
}
