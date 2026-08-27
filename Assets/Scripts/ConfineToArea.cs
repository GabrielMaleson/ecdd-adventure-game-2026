using UnityEngine;

// "Este personagem nao sai DAQUI."
//
// Uma cerca invisivel feita de area, e nao de paredes. Existe porque a cena do puzzle tem
// duas restricoes ao mesmo tempo e nenhuma delas e uma parede de verdade:
//
//   a Haze nao pode sair da area do puzzle enquanto e pilotada — ela atravessa colisor,
//     entao cercar com collider solido nao funciona nela;
//   o Josh nao pode entrar na area do puzzle — e ali ele TEM de poder andar quando a cena
//     for outra, entao uma parede fixa quebraria o resto do jogo.
//
// Por isso o limite e um COLLIDER DE AREA e o efeito e reposicionar, nao bloquear: liga
// quando a cena precisa, desliga quando acaba, e nada no mundo muda de forma.
//
// COMO SE MONTA
//   Um objeto com um Collider2D (Box ou Polygon) marcado Is Trigger, cobrindo a regiao
//   permitida, e este componente. Arraste em Alvo quem deve ficar preso.
//
// O reposicionamento e para o ponto MAIS PROXIMO da borda, e nao para o centro: empurrar
// para o centro teleporta, e o jogador perde a noria de onde estava. Deste jeito ele
// simplesmente nao consegue atravessar a linha — le como parede, sem ser uma.
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ConfineToArea : MonoBehaviour
{
    public enum Quem
    {
        Jogador,       // acha pela tag Player
        Fantasma,      // acha pelo FragmentFollow (a Haze)
        Alvo           // o que estiver arrastado no campo abaixo
    }

    [Header("Quem fica preso")]
    public Quem quem = Quem.Jogador;

    [Tooltip("Usado quando Quem = Alvo.")]
    public Transform alvo;

    [Header("Quando")]
    [Tooltip("So prende enquanto a Haze esta sendo PILOTADA. Serve para a cerca do puzzle: " +
             "fora desse momento a area nao existe para ninguem.")]
    public bool soEnquantoPilotando;

    [Header("Aviso")]
    [Tooltip("Falado quando ele bate na cerca — o cutucao que diz para onde ir. Vazio = " +
             "nao fala nada. Use o Bark Id do personagem: josh, haze, marcus, erika.")]
    public string barkSpeakerId = "";

    [TextArea(1, 3)]
    public string barkTexto = "";

    [Tooltip("Segundos de silencio entre um aviso e o proximo, para ele nao repetir a cada " +
             "quadro enquanto o jogador insiste na parede.")]
    public float intervaloDoAviso = 6f;

    [Tooltip("O quanto para DENTRO da borda ele e recolocado. Zero deixa ele exatamente em " +
             "cima da linha, e no quadro seguinte ele ja esta fora de novo — o resultado e " +
             "tremida. Um dedo de folga resolve.")]
    public float folga = 0.05f;

    private Collider2D area;
    private float proximoAviso;

    private void Awake()
    {
        area = GetComponent<Collider2D>();

        if (!area.isTrigger)
            Debug.LogWarning($"[ConfineToArea] '{name}': o Collider2D nao esta marcado Is " +
                             "Trigger. Assim ele vira parede de verdade, e a cerca passa a " +
                             "empurrar de fora em vez de segurar por dentro.", this);
    }

    private Transform Resolver()
    {
        switch (quem)
        {
            case Quem.Jogador:
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                return p != null ? p.transform : null;

            case Quem.Fantasma:
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
        if (string.IsNullOrEmpty(barkSpeakerId) || string.IsNullOrEmpty(barkTexto)) return;
        if (Time.time < proximoAviso) return;

        proximoAviso = Time.time + Mathf.Max(0.5f, intervaloDoAviso);
        BarkDirector.Bark(barkSpeakerId, barkTexto, BarkPriority.Scripted);
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.9f);
        Bounds b = c.bounds;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
