using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Yarn.Unity;

/// <summary>
/// Singleton music manager that persists across scenes.
/// Handles looping background music and exposes Yarn commands:
///   <<playmusic "trackName">>  — plays a named track from the musicTracks list
///   <<stopmusic>>              — stops current music
///
/// Also called directly by CombatSystem to play battleMusic from an EncounterFile.
///
/// Setup: Place this on a persistent GameObject in your starting scene.
///        Assign AudioSource and populate musicTracks in the inspector.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [System.Serializable]
    public class MusicEntry
    {
        public string musicName;
        public AudioClip clip;
    }

    [Header("Named Music Tracks")]
    public List<MusicEntry> musicTracks = new List<MusicEntry>();

    [Header("Música de Início")]
    [Tooltip("Trilha tocada automaticamente ao iniciar o jogo. Deixe em branco para não tocar nada.")]
    public string startupMusicName;

    [Header("Audio Source")]
    public AudioSource audioSource;

    [Header("Audio Mixer Group (opcional — necessário para o controle de volume funcionar)")]
    [Tooltip("Grupo do AudioMixer para música. Deixe em branco para saída direta.")]
    public AudioMixerGroup musicMixerGroup;

    // Nome da última trilha de ambiente tocada via PlayMusicCommand — restaurado após combate
    private string lastAmbienceTrackName;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.loop         = true;
        audioSource.playOnAwake  = false;
        audioSource.spatialBlend = 0f; // 2D — volume não depende de distância
        if (musicMixerGroup != null) audioSource.outputAudioMixerGroup = musicMixerGroup;

        if (!string.IsNullOrEmpty(startupMusicName))
            PlayMusicCommand(startupMusicName);
    }

    /// <summary>
    /// Play an AudioClip directly (used by CombatSystem for battleMusic).
    /// Does nothing if the clip is null or already playing.
    /// </summary>
    public void PlayClip(AudioClip clip)
    {
        if (clip == null) { Debug.Log("[MusicManager] PlayClip ignorado — clip é null."); return; }
        if (audioSource.clip == clip && audioSource.isPlaying) { Debug.Log($"[MusicManager] PlayClip ignorado — '{clip.name}' já está tocando."); return; }

        Debug.Log($"[MusicManager] Tocando: '{clip.name}'");
        audioSource.clip = clip;
        audioSource.Play();
    }

    /// <summary>
    /// Stop the currently playing music.
    /// </summary>
    public void StopMusic()
    {
        Debug.Log($"[MusicManager] StopMusic — clip anterior: '{(audioSource.clip != null ? audioSource.clip.name : "nenhum")}'");
        audioSource.Stop();
        audioSource.clip = null;
    }

    // ── Yarn Commands ──────────────────────────────────────────────────────

    /// <summary>
    /// Yarn: <<playmusic "trackName">>
    /// Plays a track from the musicTracks list by name.
    /// </summary>
    [YarnCommand("playmusic")]
    public static void PlayMusicCommand(string musicName)
    {
        if (Instance == null)
        {
            Debug.LogError("MusicManager: No instance found. Add a MusicManager to your scene.");
            return;
        }

        MusicEntry found = Instance.musicTracks.Find(m => m.musicName == musicName);
        if (found == null || found.clip == null)
        {
            Debug.LogError($"MusicManager: No track named '{musicName}' found. Check musicTracks in the inspector.");
            return;
        }

        Instance.lastAmbienceTrackName = musicName;
        Instance.PlayClip(found.clip);
    }

    /// <summary>
    /// Retoma a última trilha de ambiente tocada via PlayMusicCommand.
    /// Chamado automaticamente ao retornar do combate.
    /// </summary>
    public void ResumeAmbience()
    {
        if (!string.IsNullOrEmpty(lastAmbienceTrackName))
        {
            Debug.Log($"[MusicManager] Retomando ambiente: '{lastAmbienceTrackName}'");
            PlayMusicCommand(lastAmbienceTrackName);
        }
    }

    /// <summary>
    /// Yarn: <<stopmusic>>
    /// Stops whatever is currently playing.
    /// </summary>
    [YarnCommand("stopmusic")]
    public static void StopMusicCommand()
    {
        if (Instance == null) return;
        Instance.StopMusic();
    }
}
