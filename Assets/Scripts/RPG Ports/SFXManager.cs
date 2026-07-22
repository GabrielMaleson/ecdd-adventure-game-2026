using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Singleton para efeitos sonoros. Persiste entre cenas (DontDestroyOnLoad).
/// Atribua os AudioClips no inspector e chame SFXManager.Instance.Play(clip).
/// Para sons em loop (ambientação), use PlayLoop / StopLoop.
/// </summary>
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("Audio Mixer Groups (opcional — necessário para o controle de volume funcionar)")]
    [Tooltip("Grupo do AudioMixer para efeitos sonoros (SFX). Deixe em branco para saída direta.")]
    public AudioMixerGroup sfxMixerGroup;
    [Tooltip("Grupo do AudioMixer para sons em loop/ambientação. Deixe em branco para saída direta.")]
    public AudioMixerGroup loopMixerGroup;

    [Header("UI")]
    public AudioClip uiForward;
    public AudioClip uiBackward;

    [Header("Combate")]
    public AudioClip defeatFail;
    public AudioClip successAcquired;

    [Header("Mundo")]
    public AudioClip chestDoorOpen;
    public AudioClip chestDoorClose;

    [Header("Crafting")]
    public AudioClip pieceCraftFound;
    public AudioClip pieceCraftFound2;

    [Header("Ambientação (loop)")]
    public AudioClip boatAmbiance;
    public AudioClip boatAmbianceInside;

    private AudioSource sfxSource;
    private AudioSource loopSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<AudioListener>();

            sfxSource  = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f; // 2D — volume não depende de distância
            if (sfxMixerGroup != null) sfxSource.outputAudioMixerGroup = sfxMixerGroup;

            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
            loopSource.loop = true;
            loopSource.spatialBlend = 0f; // 2D
            if (loopMixerGroup != null) loopSource.outputAudioMixerGroup = loopMixerGroup;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>Reproduz um clip de efeito sonoro (one-shot).</summary>
    public void Play(AudioClip clip)
    {
        if (clip == null) { Debug.Log("[SFXManager] Play ignorado — clip é null."); return; }
        if (sfxSource == null) { Debug.LogWarning("[SFXManager] sfxSource é null."); return; }
        Debug.Log($"[SFXManager] Tocando SFX: '{clip.name}'");
        sfxSource.PlayOneShot(clip);
    }

    /// <summary>Inicia um som em loop (ambientação). Substitui qualquer loop anterior.</summary>
    public void PlayLoop(AudioClip clip)
    {
        if (clip == null || loopSource == null) return;
        if (loopSource.clip == clip && loopSource.isPlaying) { Debug.Log($"[SFXManager] Loop '{clip.name}' já está tocando."); return; }
        Debug.Log($"[SFXManager] Iniciando loop: '{clip.name}'");
        loopSource.clip = clip;
        loopSource.Play();
    }

    /// <summary>Para o som em loop atual.</summary>
    public void StopLoop()
    {
        if (loopSource != null)
        {
            Debug.Log($"[SFXManager] StopLoop — clip anterior: '{(loopSource.clip != null ? loopSource.clip.name : "nenhum")}'");
            loopSource.Stop();
        }
    }
}
