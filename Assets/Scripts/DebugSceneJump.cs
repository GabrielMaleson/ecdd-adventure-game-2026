using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Pular direto para um beat da historia, para testar sem rejogar tudo que vem antes.
//
// Isto existe porque testar a casa do Elder custava atravessar tres cenas de dialogo toda
// vez. O que faz um beat ser "aquele beat" nao e so a posicao do Josh — e o conjunto:
// quais progressos ja foram gravados, quem esta ligado na cena, quem esta seguindo quem,
// quais variaveis de Yarn ja mudaram. Pular so o corpo do Josh para o lugar certo produz
// uma cena que PARECE certa e se comporta errado, porque as condicoes nao existem.
//
// Por isso cada pulo declara o ESTADO inteiro, e nao um teleporte.
//
// ------------------------------------------------------------------ nao vai para a build
//
// O corpo do Update e compilado so no editor e em build de desenvolvimento. Numa build
// final o componente pode continuar na cena sem fazer nada e sem ler teclado.
//
// ------------------------------------------------------------------ como usar
//
// Um objeto na cena com este componente. Cada Pulo tem uma tecla. Entrou em Play, apertou
// a tecla, esta no beat. A ordem em que as coisas acontecem e fixa e importa:
//
//   1. progressos          (as condicoes precisam existir antes de qualquer no rodar)
//   2. variaveis de Yarn
//   3. ligar/desligar objetos
//   4. posicionar o Josh
//   5. formacao dos NPCs   (depois de ligados, senao nao sao achados pelo nome)
//   6. quem segue quem
//   7. o no de dialogo     (por ultimo, ja com o mundo montado)
public class DebugSceneJump : MonoBehaviour
{
    [System.Serializable]
    public class YarnBool
    {
        [Tooltip("Nome da variavel COM o cifrao, do jeito que aparece no .yarn: $YarnTalkedElder")]
        public string name;
        public bool value = true;
    }

    [System.Serializable]
    public class Jump
    {
        [Tooltip("So para voce se achar na lista.")]
        public string label = "Elder's House — fora";

        [Tooltip("Tecla que dispara este pulo.")]
        public Key key = Key.F1;

        [Header("1. Progresso (SaveManager)")]
        [Tooltip("Gravados antes de tudo. Para a casa do Elder: TalkedFriends, HeardNoise, " +
                 "MetHaze, HazeRanOff, FriendsAtEldersHouse.")]
        public string[] addProgress;

        [Tooltip("Apagados antes de tudo — para desfazer um progresso que a sessao de teste " +
                 "ja tenha gravado e que este beat nao deveria ter.")]
        public string[] removeProgress;

        [Header("2. Variaveis de Yarn")]
        public YarnBool[] yarnBools;

        [Header("3. Objetos")]
        [Tooltip("Ligados ANTES da formacao. E aqui que vao os personagens: uma formacao " +
                 "procura os NPCs pelo nome e nao acha quem esta desligado.")]
        public GameObject[] enable;

        public GameObject[] disable;

        [Tooltip("Ligados DEPOIS da formacao. E aqui que vao os gatilhos de conversa.\n\n" +
                 "A ordem nao e capricho: o DialogueStarter recalcula a caixa dele no Start() " +
                 "a partir de ONDE OS PERSONAGENS ESTAO. Ligado antes da formacao, ele mede " +
                 "os amigos no lugar velho e o E aparece longe deles — ou nao aparece.")]
        public GameObject[] enableAfterFormation;

        [Header("4. Onde o Josh aparece")]
        public Transform playerSpot;

        [Header("5. NPCs")]
        [Tooltip("Nome de uma NpcFormation: <<formation EsteNome>>. Vazio = nenhuma.")]
        public string formation;

        [Tooltip("Nomes de NpcFollow que passam a seguir. Ex: Marcus, Erika. NAO serve para "
               + "a Haze: ela usa FragmentFollow, que e outro sistema. Para ela, o campo abaixo.")]
        public string[] follow;

        [Tooltip("Nomes de objetos com FragmentFollow (a Haze) que devem estar ao lado do "
               + "Josh e ja seguindo quando o pulo acontece. Faz as tres coisas que faltavam: "
               + "LIGA o objeto (todos os Haze da cena comecam desativados), aponta o follow "
               + "para o Josh (o campo player vem vazio no prefab) e cola ela ao lado dele, em "
               + "vez de deixa-la atravessar o mapa caminhando.")]
        public string[] ghostFollow;

        [Tooltip("Nomes de NpcFollow que PARAM de seguir.")]
        public string[] unfollow;

        [Header("5b. Puzzle")]
        [Tooltip("Resolve os puzzles de caixa da cena: poe uma caixa em cima de cada alvo e " +
                 "avisa o sistema. E o que faz o arbusto sumir e a recompensa aparecer, " +
                 "porque dispara o mesmo caminho do jogo — nao ha atalho separado que " +
                 "pudesse ficar diferente do de verdade.")]
        public bool resolverPuzzles;

        [Header("6. Dialogo")]
        [Tooltip("No do .yarn disparado ao final. Vazio = nao dispara nada.")]
        public string startNode;

        [Tooltip("Espera antes de disparar o no, para os objetos ligados terminarem o Awake.")]
        public float nodeDelay = 0.15f;
    }

    [Tooltip("Precisa estar ligado para as teclas funcionarem.")]
    public bool enableHotkeys = true;

    public List<Jump> jumps = new List<Jump>();

    [Header("Player")]
    [Tooltip("Vazio = acha por tag Player.")]
    public Transform player;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        if (!enableHotkeys) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        foreach (Jump j in jumps)
        {
            if (j == null || j.key == Key.None) continue;
            if (kb[j.key].wasPressedThisFrame) { Go(j); return; }
        }
    }
#endif

    // Publico para tambem dar para chamar de um botao de debug, ou de outro UnityEvent.
    public void Go(Jump j)
    {
        if (j == null) return;
        StartCoroutine(Run(j));
    }

    // Pelo indice, para dar para ligar num Button do Canvas.
    public void GoByIndex(int index)
    {
        if (index < 0 || index >= jumps.Count) return;
        Go(jumps[index]);
    }

    private IEnumerator Run(Jump j)
    {
        Debug.Log($"[DebugSceneJump] Pulando para: {j.label}");

        // 1. Progresso primeiro: um no de Yarn disparado depois pode consultar hasprogress()
        //    logo na primeira linha, e a condicao tem que ja valer nesse momento.
        if (SaveManager.Instance != null)
        {
            foreach (string p in j.removeProgress)
                if (!string.IsNullOrEmpty(p)) SaveManager.Instance.RemoveProgress(p);

            foreach (string p in j.addProgress)
                if (!string.IsNullOrEmpty(p)) SaveManager.Instance.AddProgress(p);
        }
        else if ((j.addProgress != null && j.addProgress.Length > 0) ||
                 (j.removeProgress != null && j.removeProgress.Length > 0))
        {
            Debug.LogWarning("[DebugSceneJump] Sem SaveManager na cena — nenhum progresso foi " +
                             "gravado, e as condicoes dos nos vao falhar.");
        }

        // 2. Variaveis de Yarn, pelo mesmo storage do runner principal.
        SetYarnBools(j.yarnBools);

        // 3. Objetos.
        foreach (GameObject g in j.disable) if (g != null) g.SetActive(false);
        foreach (GameObject g in j.enable) if (g != null) g.SetActive(true);

        // 4. O Josh.
        if (j.playerSpot != null)
        {
            Transform p = ResolvePlayer();
            if (p != null)
            {
                // Corpo fisico junto: com Rigidbody2D Dynamic, mexer so no transform deixa
                // o corpo para tras por um quadro e o personagem volta sozinho.
                Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                if (rb != null) rb.position = j.playerSpot.position;
                p.position = j.playerSpot.position;
            }
            else
            {
                Debug.LogWarning("[DebugSceneJump] Nao achei o player (campo vazio e nenhum " +
                                 "objeto com tag Player).");
            }
        }

        // Um quadro para os objetos recem-ligados rodarem Awake/OnEnable e se registrarem
        // por nome. Sem isso a formacao e o follow abaixo procuram alguem que ainda nao
        // existe na lista.
        yield return null;

        // 5. NPCs.
        if (!string.IsNullOrEmpty(j.formation)) NpcFormation.Trigger(j.formation);

        foreach (string who in j.unfollow)
            if (!string.IsNullOrEmpty(who)) NpcFollow.StopFollowing(who);

        foreach (string who in j.follow)
            if (!string.IsNullOrEmpty(who)) NpcFollow.StartFollowing(who);

        if (j.ghostFollow != null)
            foreach (string who in j.ghostFollow)
                AttachGhost(who);

        if (j.resolverPuzzles) yield return ResolverPuzzles();

        // Depois da formacao, e so agora: estes objetos medem os personagens ao acordar.
        if (j.enableAfterFormation != null)
            foreach (GameObject g in j.enableAfterFormation)
                if (g != null) g.SetActive(true);

        // 6. O dialogo, com o mundo ja montado.
        if (!string.IsNullOrEmpty(j.startNode))
        {
            if (j.nodeDelay > 0f) yield return new WaitForSeconds(j.nodeDelay);

            if (DialogueManager.Instance != null)
                DialogueManager.Instance.StartDialogue(j.startNode);
            else
                Debug.LogWarning($"[DebugSceneJump] Sem DialogueManager na cena — o no " +
                                 $"'{j.startNode}' nao foi disparado.");
        }
    }

    // Poe uma caixa em cima de cada alvo e manda o sistema reavaliar.
    //
    // Deliberadamente pelo MESMO caminho do jogo — RestoreTo + EvaluateWin — e nao por um
    // atalho que marcasse "resolvido" direto. Atalho e um segundo caminho para o mesmo
    // estado, e o dia em que ele divergir do verdadeiro o teste passa a mentir.
    private IEnumerator ResolverPuzzles()
    {
        CrateTarget[] alvos = FindObjectsByType<CrateTarget>(FindObjectsSortMode.None);
        var caixas = new List<PushableCrate>(
            FindObjectsByType<PushableCrate>(FindObjectsSortMode.None));

        if (alvos.Length == 0 || caixas.Count == 0)
        {
            Debug.LogWarning($"[DebugSceneJump] resolverPuzzles: {alvos.Length} alvos e " +
                             $"{caixas.Count} caixas na cena — nada a fazer.");
            yield break;
        }

        PuzzleGrid grid = PuzzleGrid.Active;
        if (grid == null || !grid.IsReady)
        {
            Debug.LogWarning("[DebugSceneJump] resolverPuzzles: nao ha PuzzleGrid pronto na " +
                             "cena — sem ele nao da para saber onde ficam as celulas.");
            yield break;
        }

        foreach (CrateTarget alvo in alvos)
        {
            if (caixas.Count == 0) break;

            // A caixa MAIS PERTO de cada alvo, para o resultado parecer uma solucao e nao um
            // embaralhamento — importa quando o pulo e usado para tirar print.
            int melhor = 0;
            float menor = float.MaxValue;

            for (int i = 0; i < caixas.Count; i++)
            {
                float d = (caixas[i].transform.position - alvo.transform.position).sqrMagnitude;
                if (d < menor) { menor = d; melhor = i; }
            }

            PushableCrate c = caixas[melhor];
            caixas.RemoveAt(melhor);

            // A CELULA do tapete e a posicao da RAIZ da caixa para cair nela.
            //
            // Os dois passam pelo centro VISUAL e nao pela raiz, que e a regra do grid
            // inteiro. Usar transform.position direto — como estava — poe a raiz da caixa em
            // cima do tapete e o desenho dela na celula vizinha; o sistema le o desenho, nao
            // acha caixa nenhuma no tapete, e o puzzle fica desmontado sem estar resolvido.
            Vector2Int celula = grid.WorldToCell(alvo.VisualCenter);
            Vector3 raiz = grid.CellCenter(celula) - c.VisualOffset;

            c.RestoreTo(celula, raiz);
        }

        // Um quadro antes de avaliar: a varredura de caixas do CrateTarget e cacheada por
        // frame, e no quadro em que elas acabaram de se mover o cache ainda e o antigo.
        yield return null;

        CrateTarget.EvaluateWin();

        Debug.Log($"[DebugSceneJump] resolverPuzzles: {alvos.Length} alvo(s) cobertos.");
    }

    private void SetYarnBools(YarnBool[] vars)
    {
        if (vars == null || vars.Length == 0) return;

        var runner = FindFirstObjectByType<Yarn.Unity.DialogueRunner>();
        if (runner == null || runner.VariableStorage == null)
        {
            Debug.LogWarning("[DebugSceneJump] Nao achei DialogueRunner com VariableStorage — " +
                             "as variaveis de Yarn nao foram escritas.");
            return;
        }

        foreach (YarnBool v in vars)
        {
            if (v == null || string.IsNullOrEmpty(v.name)) continue;

            // O storage guarda a variavel COM o cifrao. Escrever sem ele cria uma segunda
            // variavel que nenhum no le, e o teste passa parecendo certo.
            string key = v.name.StartsWith("$") ? v.name : "$" + v.name;
            runner.VariableStorage.SetValue(key, v.value);
        }
    }

    // Liga o fantasma e cola ele no Josh, ja seguindo.
    private void AttachGhost(string who)
    {
        if (string.IsNullOrEmpty(who)) return;

        // Inclui inativos: e o caso NORMAL, nao a excecao — os quatro objetos Haze da cena
        // comecam desativados. Preferir o ativo importa porque "Haze" e "CutsceneHaze"
        // coexistem e so um deles esta em cena por vez.
        FragmentFollow best = null;

        foreach (FragmentFollow f in FindObjectsByType<FragmentFollow>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!f.gameObject.name.Equals(who, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (f.gameObject.activeInHierarchy) { best = f; break; }
            if (best == null) best = f;
        }

        if (best == null)
        {
            Debug.LogWarning($"[DebugSceneJump] nao achei objeto com FragmentFollow chamado " +
                             $"'{who}'.");
            return;
        }

        best.gameObject.SetActive(true);
        best.AttachTo(ResolvePlayer(), snap: true);

        Debug.Log($"[DebugSceneJump] '{who}' ligado e seguindo o Josh.", best);
    }

    private Transform ResolvePlayer()
    {
        if (player != null) return player;

        GameObject go = GameObject.FindGameObjectWithTag("Player");
        return go != null ? go.transform : null;
    }
}
