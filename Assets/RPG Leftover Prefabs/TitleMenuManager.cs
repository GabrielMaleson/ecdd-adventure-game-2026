using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gerencia o fluxo de menus da tela de título.
/// ESC fecha qualquer painel aberto; se nada estiver aberto, abre configurações.
/// </summary>
public class TitleMenuManager : MonoBehaviour
{
    [Header("Painéis")]
    public GameObject configPanel;

    private void Start()
    {
        if (configPanel != null)
            configPanel.SetActive(false);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

        ToggleSettings();
    }

    public void ToggleSettings()
    {
        if (configPanel == null) return;
        bool willOpen = !configPanel.activeSelf;
        SFXManager.Instance?.Play(willOpen ? SFXManager.Instance.uiForward : SFXManager.Instance.uiBackward);
        configPanel.SetActive(willOpen);
    }

    public void CloseSettings()
    {
        if (configPanel != null && configPanel.activeSelf)
            configPanel.SetActive(false);
    }

    public void StartNewGame()
    {
        SFXManager.Instance?.Play(SFXManager.Instance.uiForward);
        SaveLoadManager.Instance?.NewGame();
    }

    public void LoadGame()
    {
        if (SaveLoadManager.Instance == null || !SaveLoadManager.Instance.SaveExists())
        {
            Debug.LogWarning("[TitleMenuManager] Nenhum save encontrado.");
            return;
        }
        SFXManager.Instance?.Play(SFXManager.Instance.uiForward);
        SaveLoadManager.Instance.LoadGame();
    }

    public void QuitGame()
    {
        SFXManager.Instance?.Play(SFXManager.Instance.uiBackward);
        Application.Quit();
    }
}
