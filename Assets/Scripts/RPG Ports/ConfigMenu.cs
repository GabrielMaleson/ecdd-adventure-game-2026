using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI controller for the Config scene (loaded additively via ConfigSceneManager).
/// Pure UI: reads and writes through SettingsManager, which owns persistence and keeps
/// settings applied everywhere — this menu doesn't need to exist in a scene for that
/// scene's settings to be correct, only to let the player change them.
///
/// Setup in Inspector:
///   mixerSliders      — one entry per mixer group exposed parameter:
///                         exposedParameterName: the name you exposed in the AudioMixer
///                                               (e.g. "MasterVolume", "MusicVolume", "SFXVolume")
///                         slider: the Slider UI element for that category
///   resolutionDropdown — TMP_Dropdown listing available resolutions
///   fullscreenToggle  — (optional) Toggle for fullscreen
///
/// Important AudioMixer setup:
///   In the AudioMixer window, right-click each group's Volume and choose
///   "Expose parameter", then rename the exposed parameter to match
///   exposedParameterName above. Assign the mixer itself on SettingsManager, not here.
/// </summary>
public class ConfigMenu : MonoBehaviour
{
    [System.Serializable]
    public class MixerSliderEntry
    {
        public string label;                    // Display label (informational)
        public string exposedParameterName;     // Must match name in AudioMixer Exposed Parameters
        public Slider slider;
    }

    [Header("Mixer Sliders")]
    public List<MixerSliderEntry> mixerSliders = new List<MixerSliderEntry>();

    [Header("Resolution")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    private bool hasInitialized = false;

    private void OnEnable()
    {
        if (!hasInitialized)
        {
            SetupResolutionDropdown();
            hasInitialized = true;
        }
        LoadSettings();
    }

    // ── Resolution ─────────────────────────────────────────────────────────

    private void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null || SettingsManager.Instance == null) return;

        Resolution[] resolutions = SettingsManager.Instance.AvailableResolutions;
        resolutionDropdown.ClearOptions();

        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        for (int i = 0; i < resolutions.Length; i++)
            options.Add(new TMP_Dropdown.OptionData($"{resolutions[i].width} x {resolutions[i].height}"));

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
    }

    public void OnResolutionChanged(int index) => SettingsManager.Instance?.SetResolution(index);

    public void OnFullscreenChanged(bool isFullscreen) => SettingsManager.Instance?.SetFullscreen(isFullscreen);

    // ── Audio Mixer ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by each Slider's OnValueChanged event.
    /// Wire in the inspector: pass the index matching its entry in mixerSliders.
    /// Example: Master slider → OnMixerSliderChanged(0)
    ///          Music slider  → OnMixerSliderChanged(1)
    /// </summary>
    public void OnMixerSliderChanged(int sliderIndex)
    {
        if (SettingsManager.Instance == null || sliderIndex >= mixerSliders.Count) return;

        MixerSliderEntry entry = mixerSliders[sliderIndex];
        if (entry.slider == null || string.IsNullOrEmpty(entry.exposedParameterName)) return;

        SettingsManager.Instance.SetVolume(entry.exposedParameterName, entry.slider.value);
    }

    // ── Settings Persistence ───────────────────────────────────────────────

    private void LoadSettings()
    {
        if (SettingsManager.Instance == null) return;

        if (resolutionDropdown != null)
        {
            resolutionDropdown.value = SettingsManager.Instance.CurrentResolutionIndex();
            resolutionDropdown.RefreshShownValue();
        }

        if (fullscreenToggle != null)
            fullscreenToggle.isOn = SettingsManager.Instance.IsFullscreen;

        foreach (var entry in mixerSliders)
        {
            if (entry.slider == null || string.IsNullOrEmpty(entry.exposedParameterName)) continue;
            entry.slider.value = SettingsManager.Instance.GetVolume(entry.exposedParameterName);
        }
    }

    // ── Close ──────────────────────────────────────────────────────────────

    public void CloseConfig()
    {
        gameObject.SetActive(false); // fallback
    }
}
