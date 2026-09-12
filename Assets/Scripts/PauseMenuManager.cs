using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Menu de pausa em jogo. ESC abre/fecha; Time.timeScale = 0 enquanto pausado, o que
/// congela junto todo o resto que usa Time.deltaTime (movimento, animações,
/// corrotinas com WaitForSeconds) sem precisar avisar cada script individualmente.
/// A UI continua respondendo — o EventSystem do Unity usa tempo não-escalado.
///
/// Mesmo padrão do TitleMenuManager (painel + SFX), mas propósito diferente: aqui
/// existe um jogo rodando para salvar e retomar, então os dois não foram fundidos
/// numa única classe com um modo — cada um faz uma coisa.
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    [Header("Painéis")]
    public GameObject pausePanel;

    [Header("Voltar ao título")]
    [Tooltip("Nome da cena da tela de título, para o botão Sair.")]
    public string titleScene = "TitleScreen";

    public bool IsPaused { get; private set; }

    private void Start()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

        TogglePause();
    }

    public void TogglePause()
    {
        if (pausePanel == null) return;
        SetPaused(!IsPaused);
    }

    public void Resume() => SetPaused(false);

    private void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        SFXManager.Instance?.Play(paused ? SFXManager.Instance.uiForward : SFXManager.Instance.uiBackward);
        pausePanel.SetActive(paused);
    }

    public void SaveGame()
    {
        SFXManager.Instance?.Play(SFXManager.Instance.uiForward);
        SaveLoadManager.Instance?.SaveGame();
    }

    public void QuitToTitle()
    {
        SFXManager.Instance?.Play(SFXManager.Instance.uiBackward);

        // Time.timeScale volta ANTES de trocar de cena — sem isto a tela de título
        // carregaria congelada, com o próprio menu dela parado em Time.timeScale 0.
        Time.timeScale = 1f;
        IsPaused = false;

        SceneManager.LoadScene(titleScene);
    }
}
