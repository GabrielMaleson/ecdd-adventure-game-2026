using UnityEngine;

// "Este personagem nao sai DAQUI."
//
// Uma cerca invisivel feita de area, e nao de paredes. Existe porque a cena do puzzle tem
// duas restricoes ao mesmo tempo e nenhuma delas e uma parede de verdade:
//
//   a Haze nao pode sair da area do puzzle enquanto e pilotada — ela atravessa colisor,
//     entao cercar com collider solido nao funciona nela;
//   o Josh nao pode sair da area do puzzle — e ali ele TEM de poder andar livremente
//     quando a cena for outra, entao uma parede fixa quebraria o resto do jogo.
//
// Por isso o limite e um COLLIDER DE AREA e o efeito e reposicionar, nao bloquear: liga
// quando a cena precisa, desliga quando acaba, e nada no mundo muda de forma.
//
// O reposicionamento e para o ponto MAIS PROXIMO da borda, e nao para o centro: empurrar
// para o centro teleporta e o jogador perde a nocao de onde estava. Deste jeito ele
// simplesmente nao consegue atravessar a linha — le como parede, sem ser uma.
[DisallowMultipleComponent]
public class ConfineToArea : MonoBehaviour
{
    public enum Quem
    {
        Jogador,       // acha pela tag Player
        Fantasma,      // acha pelo FragmentFollow (a Haze)
        Alvo           // o que estiver arrastado no campo abaixo
    }

    [Header("A area")]
    [Tooltip("O collider que define a regiao permitida. Vazio = o primeiro Collider2D deste " +
             "objeto.\n\n" +
             "ARRASTE SEMPRE, mesmo sendo o deste objeto. Sem isto, um objeto com mais de um " +
             "collider entrega o errado — e um objeto SEM collider recebia do Unity um " +
             "BoxCollider2D padrao de 1x1, uma caixinha de uma unidade que prendia o jogador " +
             "num quadrado minusculo sem nada na tela explicando por que.")]
    public Collider2D area;

    [Header("Quem fica preso")]
    public Quem quem = Quem.Jogador;

    [Tooltip("Usado quando Quem = Alvo.")]
    public Transform alvo;

    [Tooltip("So prende enquanto a Haze esta sendo PILOTADA. Serve para a cerca do puzzle: " +
             "fora desse momento a area nao existe para ninguem.")]
    public bool soEnquantoPilotando;

    [Tooltip("So prende depois deste progresso ser gravado (SaveManager). Vazio = prende " +
             "desde sempre. " +
             "E o que impede a cerca de existir cedo demais. A do Josh no puzzle so vale " +
             "DEPOIS da conversa que acontece na entrada — antes dela ele ainda esta " +
             "andando pelo cemiterio e ser barrado por uma parede invisivel sem explicacao " +
             "seria um bug do ponto de vista dele.")]
    public string requiresProgress = "";

    [Tooltip("Para de prender depois deste progresso ser gravado. Vazio = nunca solta. " +
             "O par natural do de cima: a cerca do puzzle solta quando a chave e pega, e o " +
             "jogador volta a andar pelo cemiterio inteiro.")]
    public string soltaComProgresso = "";

    [Header("Aviso")]
    [Tooltip("No do .yarn tocado quando ele bate na cerca — a fala que diz para onde ir. " +
             "Vazio = nao fala nada.\n\n" +
             "E DIALOGO, com caixa e o jogo parado, e nao bark: o jogador acabou de tentar " +
             "ir para o lugar errado, e um popup que ele pode ignorar andando nao ensina " +
             "nada.\n\n" +
             "Nao ha tempo de espera entre uma vez e a proxima. A unica coisa que impede a " +
             "repeticao e o proprio dialogo estar aberto — enquanto a caixa esta na tela o " +
             "jogador nem se move, entao nao ha como bater na cerca de novo.")]
    public string dialogoAoTentarSair = "";

    [Header("Ajuste")]
    [Tooltip("O quanto para DENTRO da borda ele e recolocado. Zero deixa ele exatamente em " +
             "cima da linha, e no quadro seguinte ele ja esta fora de novo — o resultado e " +
             "tremida. Um dedo de folga resolve.")]
    public float folga = 0.05f;

    private void Awake()
    {
        if (area == null) area = GetComponent<Collider2D>();

        if (area == null)
        {
            Debug.LogError($"[ConfineToArea] '{name}': nao ha area nenhuma — nem no campo " +
                           "Area, nem um Collider2D neste objeto. A cerca nao vai fazer nada.",
                           this);
            enabled = false;
            return;
        }

        // Mais de um collider aqui e ambiguidade silenciosa: o GetComponent devolve um deles
        // e ninguem sabe qual. Se a cerca prender numa area que nao e a desenhada, e isto.
        Collider2D[] todos = GetComponents<Collider2D>();
        if (area == GetComponent<Collider2D>() && todos.Length > 1)
            Debug.LogWarning($"[ConfineToArea] '{name}': ha {todos.Length} colliders neste " +
                             "objeto e o campo Area esta vazio — usei o primeiro, que pode " +
                             "nao ser o que voce desenhou. Arraste o certo no campo Area.",
                             this);

        if (!area.isTrigger)
            Debug.LogWarning($"[ConfineToArea] '{name}': a area nao esta marcada Is Trigger. " +
                             "Assim ela vira parede de verdade e empurra de fora, em vez de " +
                             "segurar por dentro.", this);
    }

    private Transform Resolver()
    {
        switch (quem)
        {
            case Quem.Jogador:
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                return p != null ? p.transform : null;

            case Quem.Fantasma:
                // A Haze QUE ESTA SENDO PILOTADA, e nao "a primeira que aparecer".
                //
                // A cena tem varios objetos Haze (HazeOne, HazeTwo, CutsceneHaze, TrueHaze)
                // e so um vale por vez. FindFirstObjectByType devolvia qualquer um, entao a
                // cerca podia estar prendendo um fantasma que ninguem esta controlando
                // enquanto o pilotado passeava livre.
                if (GhostControl.Instance != null && GhostControl.Instance.Fantasma != null)
                    return GhostControl.Instance.Fantasma;

                FragmentFollow f = FindFirstObjectByType<FragmentFollow>();
                return f != null ? f.transform : null;

            default:
                return alvo;
        }
    }

    // LateUpdate, e nao Update: quem move o personagem — PlayerController, GhostControl,
    // NpcWalkTo — ja andou neste quadro. Corrigir antes deles seria corrigir a posicao
    // velha, e a nova passaria batido.
    private void LateUpdate()
    {
        if (area == null) return;

        if (!string.IsNullOrEmpty(requiresProgress))
        {
            if (SaveManager.Instance == null) return;
            if (!SaveManager.Instance.HasProgress(requiresProgress)) return;
        }

        if (!string.IsNullOrEmpty(soltaComProgresso) &&
            SaveManager.Instance != null &&
            SaveManager.Instance.HasProgress(soltaComProgresso)) return;

        if (soEnquantoPilotando)
        {
            if (GhostControl.Instance == null) return;
            if (GhostControl.Instance.State != GhostControl.Mode.Piloting) return;
        }

        Transform t = Resolver();
        if (t == null) return;

        Vector2 pos = t.position;
        if (area.OverlapPoint(pos)) return;

        // ClosestPoint devolve o ponto da BORDA mais perto — o caminho mais curto de volta
        // para dentro. Puxar um dedo a mais para o centro evita ele ficar exatamente na
        // linha e sair de novo no quadro seguinte.
        Vector2 borda = area.ClosestPoint(pos);
        Vector2 paraDentro = ((Vector2)area.bounds.center - borda).normalized * folga;
        Vector2 destino = borda + paraDentro;

        t.position = new Vector3(destino.x, destino.y, t.position.z);

        // O corpo fisico junto, senao ele volta sozinho no proximo passo de fisica.
        Rigidbody2D rb = t.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = destino;

        Avisar();
    }

    private void Avisar()
    {
        if (string.IsNullOrEmpty(dialogoAoTentarSair)) return;

        // Sem cooldown de propria escolha: o dialogo abrindo ja congela o jogador, e um
        // jogador congelado nao bate na cerca de novo. O tempo de espera seria um segundo
        // guardiao dizendo a mesma coisa, e daria o efeito ruim de a fala NAO sair quando o
        // jogador tenta sair de novo depois de ter lido a primeira.
        DialogueStarter.EvaluateConditionsAndStart(dialogoAoTentarSair, false, null, false);
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D c = area != null ? area : GetComponent<Collider2D>();
        if (c == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.9f);
        Bounds b = c.bounds;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
