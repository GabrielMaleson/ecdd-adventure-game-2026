using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// "Puzzle resolvido → esta parede some."
//
// NÃO roda nada por frame. Ele acorda só quando o tabuleiro muda — fim do deslize de
// uma caixa, ou passo da estátua — e aí faz a mesma pergunta que os seus olhos fazem:
// cada tapete da cena tem uma caixa em cima? Quando a resposta é sim, apaga o Hide.
//
// Não depende de Puzzle Id, de latch, nem do mapa de ocupação: ele mede a posição do
// desenho, que é o que você vê na tela.
//
// SETUP
//   1. Empty na cena → Add Component → Puzzle Gate.
//   2. Arrasta a parede pro campo Hide.
//   Pronto. Targets vazio = ele vigia TODOS os tapetes da cena sozinho.
public class PuzzleGate : MonoBehaviour
{
    [Header("O que abre")]
    [Tooltip("Desligados quando o puzzle fecha — a parede.")]
    [SerializeField] GameObject[] hide;

    [Tooltip("Ligados quando o puzzle fecha — passagem, escada, o que for.")]
    [SerializeField] GameObject[] show;

    [Header("O que ele vigia")]
    [Tooltip("VAZIO = todos os tapetes (CrateTarget) da cena. Preencha só se esta parede depender de um subconjunto específico.")]
    [SerializeField] List<CrateTarget> targets = new List<CrateTarget>();

    [Header("Comportamento")]
    [Tooltip("Ligado: uma vez aberta, fica aberta pra sempre. Desligado: fecha de novo se tirarem uma caixa do tapete.")]
    [SerializeField] bool stayOpenOnceOpened = true;

    [Tooltip("Dispara quando abre — som de pedra, poeira, câmera.")]
    public UnityEvent onOpened;

    [Tooltip("Dispara se fechar de novo (só possível com Stay Open desligado).")]
    public UnityEvent onClosed;

    public bool IsOpen { get; private set; }

    List<CrateTarget>   watched = new List<CrateTarget>();
    List<PushableCrate> crates  = new List<PushableCrate>();

    void OnEnable()  => CrateTarget.BoardChanged += CheckNow;
    void OnDisable() => CrateTarget.BoardChanged -= CheckNow;

    void Start()
    {
        Rescan();
        CheckNow();          // cobre um tabuleiro que já nasce resolvido
    }

    // Tapetes e caixas são objetos de cena que não nascem nem morrem no meio do jogo,
    // então uma varredura no Start basta. Público caso algum dia algo seja instanciado.
    public void Rescan()
    {
        watched.Clear();
        if (targets.Count > 0)
        {
            foreach (var t in targets) if (t != null) watched.Add(t);
        }
        else
        {
            watched.AddRange(FindObjectsByType<CrateTarget>(FindObjectsSortMode.None));
        }

        crates.Clear();
        crates.AddRange(FindObjectsByType<PushableCrate>(FindObjectsSortMode.None));
    }

    // Chamado quando o tabuleiro muda. Público também pra um UnityEvent poder forçar
    // uma reconferida.
    public void CheckNow()
    {
        bool solved = AllCovered();

        if (solved && !IsOpen) Open();
        else if (!solved && IsOpen && !stayOpenOnceOpened) Close();
    }

    // Todo tapete tem uma caixa na mesma célula? Célula lida AGORA, dos dois lados, a
    // partir do centro do desenho — a mesma medida que faz "parece alinhado" e "está
    // alinhado" nunca discordarem no resto do sistema.
    bool AllCovered()
    {
        var g = PuzzleGrid.Active;
        if (g == null || !g.IsReady) return false;
        if (watched.Count == 0) return false;      // sem tapete não existe puzzle

        foreach (var target in watched)
        {
            if (target == null) continue;

            Vector2Int cell = g.WorldToCell(target.VisualCenter);

            bool covered = false;
            foreach (var crate in crates)
            {
                if (crate == null || !crate.isActiveAndEnabled) continue;
                if (g.WorldToCell(crate.VisualCenter) == cell) { covered = true; break; }
            }

            if (!covered) return false;
        }
        return true;
    }

    // Público pra um UnityEvent poder abrir na marra (diálogo, cutscene, debug).
    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;

        foreach (var go in hide) if (go != null) go.SetActive(false);
        foreach (var go in show) if (go != null) go.SetActive(true);

        onOpened?.Invoke();

        // Aberta pra sempre: não há mais nada pra vigiar.
        if (stayOpenOnceOpened) enabled = false;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        foreach (var go in hide) if (go != null) go.SetActive(true);
        foreach (var go in show) if (go != null) go.SetActive(false);

        onClosed?.Invoke();
    }
}
