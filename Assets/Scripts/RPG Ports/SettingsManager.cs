using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Dono único das configurações de vídeo/áudio. Singleton persistente (DontDestroyOnLoad),
/// mesmo padrão do SFXManager/MusicManager/SaveManager — pode existir em toda cena (só a
/// primeira instância sobrevive), mas só PRECISA existir na cena inicial.
///
/// Aplica a configuração salva (resolução, fullscreen, volumes) sozinho no Awake, então
/// ela já vale assim que o jogo carrega, mesmo que nenhum ConfigMenu tenha sido aberto.
/// É isso que deixa de exigir um ConfigMenu (com sua própria lógica de aplicar/salvar) em
/// cada cena: o ConfigMenu vira só a UI — lê e escreve através deste singleton.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("Audio Mixer")]
    public AudioMixer masterMixer;

    [Tooltip("Nome de cada parâmetro exposto no AudioMixer que tem volume salvo (ex: MasterVolume, MusicVolume, SFXVolume). Precisa bater com o nome exposto no AudioMixer.")]
    public string[] volumeParameters = { "MasterVolume", "MusicVolume", "SFXVolume" };

    private Resolution[] availableResolutions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        availableResolutions = Screen.resolutions;
        ApplyAllSavedSettings();
    }

    private void ApplyAllSavedSettings()
    {
        int savedIndex = PlayerPrefs.GetInt("ResolutionIndex", -1);
        if (savedIndex >= 0 && savedIndex < availableResolutions.Length)
        {
            Resolution res = availableResolutions[savedIndex];
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        }

        if (PlayerPrefs.HasKey("Fullscreen"))
            Screen.fullScreen = PlayerPrefs.GetInt("Fullscreen") == 1;

        if (masterMixer != null)
        {
            foreach (var param in volumeParameters)
                masterMixer.SetFloat(param, SliderToDb(GetVolume(param)));
        }
    }

    // ── Resolução ──────────────────────────────────────────────────────────

    public Resolution[] AvailableResolutions => availableResolutions;

    public int CurrentResolutionIndex()
    {
        int saved = PlayerPrefs.GetInt("ResolutionIndex", -1);
        if (saved >= 0 && saved < availableResolutions.Length) return saved;

        for (int i = 0; i < availableResolutions.Length; i++)
            if (availableResolutions[i].width == Screen.currentResolution.width &&
                availableResolutions[i].height == Screen.currentResolution.height)
                return i;
        return 0;
    }

    public void SetResolution(int index)
    {
        if (index < 0 || index >= availableResolutions.Length) return;

        Resolution res = availableResolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);

        PlayerPrefs.SetInt("ResolutionIndex", index);
        PlayerPrefs.Save();
    }

    public bool IsFullscreen => Screen.fullScreen;

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    // ── Volume ─────────────────────────────────────────────────────────────

    public float GetVolume(string exposedParameterName, float defaultValue = 0.75f) =>
        PlayerPrefs.GetFloat("Vol_" + exposedParameterName, defaultValue);

    public void SetVolume(string exposedParameterName, float sliderValue)
    {
        if (masterMixer == null || string.IsNullOrEmpty(exposedParameterName)) return;

        masterMixer.SetFloat(exposedParameterName, SliderToDb(sliderValue));
        PlayerPrefs.SetFloat("Vol_" + exposedParameterName, sliderValue);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Converte um valor de slider (0–1) para decibéis.
    /// 0    → -80 dB (silêncio)
    /// 0.75 →   0 dB (unity)
    /// 1    →  +3 dB (leve boost)
    /// Escala logarítmica para o slider soar natural.
    /// </summary>
    private static float SliderToDb(float value)
    {
        value = Mathf.Clamp(value, 0.0001f, 1f);
        return Mathf.Log10(value) * 20f;
    }
}
