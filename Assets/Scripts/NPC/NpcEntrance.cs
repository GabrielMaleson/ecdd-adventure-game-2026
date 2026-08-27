using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// "Quando o Josh andar pra dentro, os amigos entram atras dele e vao cada um pro seu canto."
//
// Um gatilho na cena + uma lista de quem entra e para onde. Sem codigo por beat: a proxima
// casa, o proximo grupo, e outro NpcEntrance com outra lista.
//
// ------------------------------------------------------------------ o gatilho
//
// O jogador precisa ANDAR antes de qualquer coisa acontecer — nada dispara so por ele
// aparecer na sala. Isso nao e um estado extra para controlar: e so onde o gatilho fica.
// Coloque o volume um passo ADIANTE da porta, e entrar nele ja significa "ele andou pra
// frente". O gizmo verde mostra o volume; o amarelo, quem vai para onde.
//
// ------------------------------------------------------------------ o jogo nao para
//
// Ninguem congela o jogador, ninguem abre caixa de dialogo. Os NPCs andam por conta
// propria enquanto o Josh anda por conta dele. Quem quiser esperar todo mundo chegar
// escuta onAllArrived.
//
// ------------------------------------------------------------------ ficar interagivel
//
// Este script nao sabe o que e "interagivel", de proposito — do mesmo jeito que o
// CrateTarget do sokoban nao sabe o que e a recompensa. Cada entrada tem onArrived (e o
// conjunto tem onAllArrived): arraste ali o objeto da interacao e use SetActive, ou
// Behaviour.enabled. Trocar o que a chegada CAUSA nunca deve exigir mexer neste arquivo.
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class NpcEntrance : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("Quem entra. Precisa de um NpcWalkTo — se nao tiver, um e adicionado na hora.")]
        public Transform npc;

        [Tooltip("Onde ele APARECE ao comecar (a porta). Vazio = de onde ele ja estiver.")]
        public Transform entrance;

        [Tooltip("O caminho, em ordem — o ULTIMO ponto e onde ele fica. Um ponto so = anda " +
                 "reto ate la.\n\n" +
                 "Use mais de um quando houver parede ou movel entre a porta e a marca: o " +
                 "movimento e em linha reta, entao sem um ponto no vao da porta ele encosta " +
                 "e fica empurrando. Cada ponto tem um tempo de espera opcional.")]
        public List<NpcWalkTo.Stop> path = new List<NpcWalkTo.Stop>();

        [Tooltip("Segundos de espera antes DESTE personagem comecar a andar. Escalone " +
                 "(0, 0.6, 1.2...) para eles entrarem em fila e nao empilhados na porta.")]
        public float startDelay;

        [Tooltip("Velocidade. 0 = usa a que ja esta no NpcWalkTo dele.")]
        public float moveSpeed;

        [Tooltip("Ligados no momento em que ELE chega. E aqui que entra o objeto que " +
                 "torna este personagem interagivel.")]
        public GameObject[] enableOnArrival;

        [Tooltip("Disparado quando ELE chega, alem dos objetos acima.")]
        public UnityEvent onArrived;
    }

    [Header("Quem entra")]
    public List<Entry> entries = new List<Entry>();

    [Header("Gatilho")]
    [Tooltip("Tag de quem dispara. O volume deste objeto precisa ser Is Trigger.")]
    public string playerTag = "Player";

    [Tooltip("Dispara uma vez so. Desligue apenas para testar na mesma sessao de Play.")]
    public bool onceOnly = true;

    [Tooltip("Nao dispara sem este progresso (SaveManager). Vazio = sem condicao.\n\n" +
             "Para a casa do Elder o valor certo e FriendsAtEldersHouse — sem isso os " +
             "amigos entrariam tambem numa visita futura, quando eles nao deveriam estar " +
             "mais juntos do Josh.")]
    public string requiresProgress = "";

    [Tooltip("Progresso gravado quando todos chegam. Vazio = nao grava nada.")]
    public string progressOnAllArrived = "";

    [Header("Quando todos chegam")]
    public UnityEvent onAllArrived;

    private bool fired;
    private int pending;

    private void Reset()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (fired && onceOnly) return;
        if (!other.CompareTag(playerTag)) return;

        if (!string.IsNullOrEmpty(requiresProgress))
        {
            if (SaveManager.Instance == null) return;
            if (!SaveManager.Instance.HasProgress(requiresProgress)) return;
        }

        fired = true;
        Begin();
    }

    // Da para chamar pelo Inspector (de outro UnityEvent) quando a entrada tiver que ser
    // provocada por outra coisa que nao pisar no volume.
    public void Begin()
    {
        pending = 0;

        foreach (Entry e in entries)
        {
            if (e == null || e.npc == null) continue;
            pending++;
            StartCoroutine(Run(e));
        }

        if (pending == 0) AllArrived();
    }

    private IEnumerator Run(Entry e)
    {
        GameObject go = e.npc.gameObject;

        // Ligar ANTES de posicionar: um objeto desligado nao roda Awake, e o NpcWalkTo
        // precisa ter achado o Rigidbody e o CharacterFacing antes do primeiro passo.
        if (!go.activeSelf) go.SetActive(true);

        // Quem esta SEGUINDO nao pode ao mesmo tempo ir para uma marca.
        //
        // O NpcFollow puxa o personagem para a trilha do Josh e o NpcWalkTo puxa para o
        // ponto: os dois escrevem na mesma posicao a cada passo de fisica e o NPC treme
        // entre eles, sem chegar em lugar nenhum. Isso passou a importar quando o roteiro
        // ganhou <<follow Marcus>> e <<follow Erika>> no fim da casa do Elder — dali em
        // diante os dois chegam a QUALQUER entrada ja seguindo.
        //
        // Desligar aqui e nao pedir um <<unfollow>> no .yarn e deliberado: assim a entrada
        // funciona sozinha, e nao depende de alguem lembrar de escrever duas linhas no
        // roteiro toda vez que criar uma. O NpcGather resolve do mesmo jeito, pelo mesmo
        // motivo. Para voltar a seguir depois, um <<follow>> no fim do beat.
        NpcFollow follow = go.GetComponent<NpcFollow>();
        if (follow != null && follow.enabled) follow.enabled = false;

        if (e.entrance != null)
        {
            // Rigidbody2D.position e nao transform.position: com corpo Dynamic, escrever no
            // transform deixa o corpo fisico para tras por um quadro, e o NPC aparece na
            // porta mas anda a partir de onde estava antes.
            Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = e.entrance.position;
            go.transform.position = e.entrance.position;
        }

        // Um quadro de fisica ANTES de mandar andar, sempre — nao so quando ha startDelay.
        //
        // Escrever Rigidbody2D.position nao move o corpo na hora: o motor de fisica so
        // aplica no proximo passo. Mandando andar no mesmo quadro, o FixedUpdate lia a
        // posicao ANTIGA e calculava o caminho a partir do lugar errado. Quem tinha
        // startDelay > 0 escapava por acidente (a espera dava o quadro), e quem tinha 0
        // travava — era exatamente a diferenca entre a Erika (0.6) e o Marcus (0).
        yield return new WaitForFixedUpdate();

        if (e.startDelay > 0f) yield return new WaitForSeconds(e.startDelay);

        NpcWalkTo walker = go.GetComponent<NpcWalkTo>();
        if (walker == null) walker = go.AddComponent<NpcWalkTo>();

        if (e.moveSpeed > 0f) walker.moveSpeed = e.moveSpeed;

        // Para que lado o personagem fica olhando ao chegar e decidido NO NpcWalkTo dele,
        // e em nenhum outro lugar.
        //
        // Aqui existia um campo que sobrescrevia aquele. Dois lugares mandando na mesma
        // coisa e sempre ruim, e este caso foi pior: o valor gravado na cena era um inteiro,
        // e quando o enum ganhou uma opcao nova o MESMO numero passou a significar outra
        // coisa — a escolha do autor virou "Manter" sozinha, sem aviso, e apagava em silencio
        // o que ele punha no personagem.

        bool done = false;
        void OnDone() => done = true;

        walker.onArrived.AddListener(OnDone);

        // Caminho vazio aqui = usa o que ja esta no NpcWalkTo do personagem. Ter os mesmos
        // pontos em dois lugares e convite para eles desencontrarem, entao o normal e
        // montar o caminho UMA vez, no proprio NPC, e deixar este campo vazio. Preencha
        // aqui so quando o MESMO personagem precisar entrar por caminhos diferentes em
        // beats diferentes.
        if (e.path != null && e.path.Count > 0) walker.GoPath(e.path);
        else if (walker.path.Count > 0) walker.GoPath(walker.path);
        else { walker.onArrived.RemoveListener(OnDone); pending--; if (pending <= 0) AllArrived(); yield break; }

        while (!done) yield return null;

        walker.onArrived.RemoveListener(OnDone);

        foreach (GameObject g in e.enableOnArrival)
            if (g != null) g.SetActive(true);

        e.onArrived?.Invoke();

        pending--;
        if (pending <= 0) AllArrived();
    }

    private void AllArrived()
    {
        if (!string.IsNullOrEmpty(progressOnAllArrived) && SaveManager.Instance != null)
            SaveManager.Instance.AddProgress(progressOnAllArrived);

        onAllArrived?.Invoke();
    }

    // Selecionado, e nao sempre: um cubo verde solido desenhado o tempo todo tapa a arte da
    // cena inteira e nao deixa ninguem trabalhar.
    private void OnDrawGizmosSelected()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }

        Gizmos.color = Color.yellow;
        foreach (Entry e in entries)
        {
            if (e == null || e.path == null || e.path.Count == 0) continue;

            Vector3 from = e.entrance != null ? e.entrance.position
                         : e.npc != null ? e.npc.position
                         : transform.position;

            // O caminho todo, para o trecho que atravessa parede saltar aos olhos aqui na
            // cena em vez de so aparecer como um NPC empurrando movel em Play.
            foreach (NpcWalkTo.Stop st in e.path)
            {
                if (st == null || st.point == null) continue;
                Gizmos.DrawLine(from, st.point.position);
                Gizmos.DrawWireSphere(st.point.position, st.waitHere > 0f ? 0.26f : 0.18f);
                from = st.point.position;
            }
        }
    }
}
