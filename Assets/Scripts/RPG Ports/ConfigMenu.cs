using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI controller for the Config scene (loaded additively via ConfigSceneManager).
///
/// Setup in Inspector:
///   masterMixer       — assign the Master.mixer asset
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
///   exposedParameterName above.
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

    [Header("Audio Mixer")]
    public AudioMixer masterMixer;
    public List<MixerSliderEntry> mixerSliders = new List<MixerSliderEntry>();

    [Header("Resolution")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    private Resolution[] availableResolutions;
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
        if (resolutionDropdown == null) return;

        availableResolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        int currentIndex = 0;

        for (int i = 0; i < availableResolutions.Length; i++)
        {
            string label = $"{availableResolutions[i].width} x {availableResolutions[i].height}";
            options.Add(new TMP_Dropdown.OptionData(label));

            if (availableResolutions[i].width == Screen.currentResolution.width &&
                availableResolutions[i].height == Screen.currentResolution.height)
                currentIndex = i;
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentIndex;
        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = Screen.fullScreen;
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }
    }

    public void OnResolutionChanged(int index)
    {
        if (availableResolutions == null || index >= availableResolutions.Length) return;

        Resolution res = availableResolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);

        PlayerPrefs.SetInt("ResolutionIndex", index);
        PlayerPrefs.Save();
    }

    public void OnFullscreenChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    // ── Audio Mixer ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by each Slider's OnValueChanged event.
    /// Wire in the inspector: pass the index matching its entry in mixerSliders.
    /// Example: Master slider → OnMixerSliderChanged(0)
    ///          Music slider  → OnMixerSliderChanged(1)
    /// </summary>
    public void OnMixerSliderChanged(int sliderIndex)
    {
        if (masterMixer == null || sliderIndex >= mixerSliders.Count) return;

        MixerSliderEntry entry = mixerSliders[sliderIndex];
        if (entry.slider == null || string.IsNullOrEmpty(entry.exposedParameterName)) return;

        float db = SliderToDb(entry.slider.value);
        masterMixer.SetFloat(entry.exposedParameterName, db);

        PlayerPrefs.SetFloat("Vol_" + entry.exposedParameterName, entry.slider.value);
        PlayerPrefs.Save();
    }

    // ── Settings Persistence ───────────────────────────────────────────────

    private void LoadSettings()
    {
        // Resolution
        if (resolutionDropdown != null && availableResolutions != null)
        {
            int savedIndex = PlayerPrefs.GetInt("ResolutionIndex", -1);
            if (savedIndex >= 0 && savedIndex < availableResolutions.Length)
            {
                resolutionDropdown.value = savedIndex;
                resolutionDropdown.RefreshShownValue();
                Resolution res = availableResolutions[savedIndex];
                Screen.SetResolution(res.width, res.height, Screen.fullScreen);
            }
        }

        // Fullscreen
        if (fullscreenToggle != null && PlayerPrefs.HasKey("Fullscreen"))
            fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen") == 1;

        // Mixer volumes
        if (masterMixer != null)
        {
            foreach (var entry in mixerSliders)
            {
                if (entry.slider == null || string.IsNullOrEmpty(entry.exposedParameterName)) continue;

                float savedValue = PlayerPrefs.GetFloat("Vol_" + entry.exposedParameterName, 0.75f);
                entry.slider.value = savedValue;
                masterMixer.SetFloat(entry.exposedParameterName, SliderToDb(savedValue));
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a 0–1 slider value to decibels.
    /// 0   → -80 dB (silence)
    /// 0.75→   0 dB (unity)
    /// 1   →  +3 dB (slight boost)
    /// Uses logarithmic scaling so the slider feels natural.
    /// </summary>
    private float SliderToDb(float value)
    {
        value = Mathf.Clamp(value, 0.0001f, 1f);
        return Mathf.Log10(value) * 20f;
    }

    // ── Close ──────────────────────────────────────────────────────────────

    // ── Close ──────────────────────────────────────────────────────────────

    public void CloseConfig()
    {
            gameObject.SetActive(false); // fallback
    }
}
