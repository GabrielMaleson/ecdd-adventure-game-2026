using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// O motor do Y-Sort. Nao e um componente: nao existe em objeto nenhum da cena, nao aparece
// no Inspector, e nao ha nada para adicionar em 325 arvores.
//
// ------------------------------------------------------------------ por que assim
//
// A primeira versao disto era um componente por objeto, ligado por uma ferramenta de
// migracao. Funciona, mas cobra caro: 325 componentes gravados na cena, 700 Sorting Orders
// reescritos no arquivo, e um diff gigante em cima de uma cena que duas pessoas editam.
// Pior, todo objeto novo colocado depois nasce fora do sistema ate alguem lembrar de rodar
// a ferramenta de novo.
//
// Aqui o mundo e varrido em tempo de execucao. Objeto novo entra sozinho. Cena nova
// funciona sozinha. Apagar estes arquivos devolve o jogo ao que era, porque o arquivo da
// cena nunca foi tocado.
//
// O componente YSort continua existindo, mas virou o AJUSTE, nao o motor: so quem precisa
// de uma ancora diferente do padrao (ou precisa ficar de fora) ganha um.
//
// ------------------------------------------------------------------ a regra
//
// Quem esta mais para baixo na tela desenha na frente:  Sorting Order = -Y * Precision.
//
// GRUPO. Um objeto quase nunca e um sprite so. A arvore tem tres (sombra, tronco, copa) e o
// Josh tem dois (Top e Bottom). Ordenar cada sprite pelo proprio Y destruiria o objeto: a
// copa tem Y maior que o tronco, entao seria desenhada ATRAS dele, e a cabeca do Josh atras
// das pernas. Entao o Y de UM ponto — a base do objeto — decide a ordem do grupo todo, e
// dentro do grupo cada sprite mantem a ordem relativa com que a arte foi montada. E por
// isso que os Sorting Orders que ja existem (sombra 1, tronco 2, copa 3) nao sao jogados
// fora: eles VIRAM essa ordem relativa.
//
// ANCORA. O Y que importa e onde o objeto TOCA O CHAO, nao o centro do sprite nem o pivo.
// Uma arvore de 3 metros medida pelo meio some atras de coisas que deveria cobrir. O padrao
// e a base do collider solido (a parte por onde o jogador esbarra, que e o pe do objeto);
// sem collider, a base da arte. Quando isso erra, um YSort no objeto corrige.
//
// ------------------------------------------------------------------ quem entra
//
// SO objetos com collider solido — ver Qualifies(). Tudo mais fica INTACTO.
//
// Isto nao e detalhe, e a parte que eu errei primeiro: a versao inicial ordenava tudo que
// tinha sprite, e varreu junto a estrada, a poca, a grama e os sprites de interior das
// casas. Chao ordenado por Y passa por CIMA do personagem, porque na tela o chao esta
// embaixo dele — a regra "quem esta mais embaixo desenha na frente", aplicada a uma coisa
// deitada, produz exatamente o oposto do que se quer.
//
// ------------------------------------------------------------------ o que ele NAO faz
//
// Tilemap. O chao e o penhasco sao TilemapRenderer e continuam na camada Default, abaixo de
// tudo. Se um dia o penhasco precisar se entrelacar com personagens, o caminho e o Mode
// "Individual" do Tilemap Renderer, nao isto aqui.
//
// Nevoa (Haze), balao de fala e prompt de interacao ficam de fora por nome e por camada —
// ver Skip* logo abaixo.
public static class YSortWorld
{
    // Sprites que NAO devem ser ordenados por Y, e por que:
    //  - Dialogue: balao de fala e prompt de interacao, sempre por cima de tudo;
    //  - qualquer coisa dentro de um Canvas: UI nao usa Sorting Order deste jeito;
    //  - fog/mist/nevoa/vignette: camadas de atmosfera desenhadas por cima da cena inteira.
    //
    // CUIDADO ao mexer nesta lista de nomes. "Haze" ja esteve aqui por parecer nevoa, e Haze
    // e um NPC principal — teria tirado um personagem do Y-Sort inteiro. Filtrar por nome e
    // fragil num jogo cujo tema E a nevoa: na duvida, deixe o objeto entrar e marque
    // "Nao ordenar" no YSort dele, que e explicito e nao pega ninguem de surpresa.
    public static readonly string[] SkipLayers = { "Dialogue" };
    public static readonly string[] SkipNameContains = { "fog", "mist", "nevoa", "vignette" };

    public class Entry
    {
        public Transform root;
        public SpriteRenderer[] renderers;
        public int[] relative;       // ordem de cada sprite DENTRO do objeto, menor = 0
        public float anchorOffsetY;  // do pivo ate o chao
        public YSort tuning;         // ajuste manual, quase sempre null

        // A medida da ancora so vale se foi feita com o objeto ATIVO. Renderer e collider
        // de objeto desativado devolvem bounds zerados, e Marcus e Erika comecam
        // desativados: a medida saia como (0 - posicaoY), lixo, e ficava cacheada para
        // sempre. Enquanto isto for false, a Entry remede a cada quadro ate conseguir.
        public bool anchorMeasured;

        public float lastAnchor = float.NaN;
        public int lastOrder = int.MinValue;
    }

    private static readonly Dictionary<Transform, Entry> byRoot = new Dictionary<Transform, Entry>();
    private static readonly List<Entry> live = new List<Entry>();
    private static readonly List<Transform> dead = new List<Transform>();

    // Reaproveitado entre varreduras. Alocar um HashSet novo a cada 2 segundos, para sempre,
    // e lixo de graca para o GC coletar depois — e coletor de lixo em jogo aparece como
    // engasgo, nao como FPS medio mais baixo.
    private static readonly HashSet<Transform> seen = new HashSet<Transform>();

    private static float nextScan;
    private static Driver driver;

    public static int Count => live.Count;

    // ------------------------------------------------------------------ ciclo de vida

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        Clear();

        if (!YSortSettings.SystemEnabled) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        EnsureDriver();
        Rescan();
        Apply();

        Report();
    }

    // Uma linha no Console ao entrar em Play, dizendo se o sistema esta de pe e com que
    // numeros. Existe porque "o personagem some atras do chao" tem duas causas opostas — o
    // sistema nao estar rodando, ou estar rodando com a ordem errada — e do lado de fora as
    // duas parecem iguais. Esta linha separa as duas em um segundo.
    private static void Report()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[Y-Sort] ATIVO. {live.Count} objetos ordenados. ");
        sb.Append($"orderBase={YSortSettings.Current.orderBase}, ");
        sb.Append($"precision={YSortSettings.Current.precision}, ");
        sb.Append($"camada={(YSortSettings.ForcesLayer ? YSortSettings.Current.sortingLayerName : "nao forcada")}.");

        // Amostra: o que cada um recebeu de verdade. Se um personagem aparecer aqui com
        // ordem MENOR que a do chao (chao da casa do Elder esta perto de -12000), e a ordem
        // que esta errada. Se ele nao aparecer na lista, ele nao entrou no sistema.
        int shown = 0;
        foreach (Entry e in live)
        {
            if (e.root == null || shown >= 8) continue;
            sb.Append($"\n   {e.root.name}: Y={e.root.position.y:F1} ancora={e.lastAnchor:F1} order={e.lastOrder}");
            shown++;
        }

        if (live.Count == 0)
            sb.Append("\n   NENHUM objeto entrou. Nada tem collider solido nem Rigidbody2D, " +
                      "ou a varredura rodou antes da cena existir.");

        Debug.Log(sb.ToString());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Rescan();
        Apply();
    }

    // O objeto que chama o Apply todo quadro. Nasce em tempo de execucao com HideAndDontSave:
    // nao aparece na Hierarchy e nunca e gravado em cena nenhuma.
    private static void EnsureDriver()
    {
        if (driver != null) return;

        GameObject go = new GameObject("~YSortWorld") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(go);
        driver = go.AddComponent<Driver>();
    }

    private class Driver : MonoBehaviour
    {
        // LateUpdate e nao Update: o personagem e movido no FixedUpdate/Update, e ordenar
        // antes de ele andar deixa a ordem um quadro atrasada — visivel exatamente no
        // momento em que ele cruza a frente de alguma coisa.
        private void LateUpdate()
        {
            YSortWorld.Pump(Time.unscaledTime);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // F3 fotografa a ordem de desenho em volta do jogador AGORA. O relatorio do
            // boot so mostra o mundo no carregamento — e o problema acontece dentro de uma
            // casa, depois de andar ate la. Sem isto nao ha como comparar a ordem do
            // personagem com a das coisas em que ele afunda.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb[UnityEngine.InputSystem.Key.F8].wasPressedThisFrame)
                YSortWorld.DumpAroundPlayer();
#endif
        }
    }

    // Um tique completo: varre de vez em quando (para pegar o que nasceu no meio do jogo) e
    // aplica sempre.
    public static bool Pump(float now)
    {
        if (!YSortSettings.SystemEnabled) return false;

        float interval = YSortSettings.RescanInterval;

        if (interval > 0f && now >= nextScan)
        {
            nextScan = now + interval;
            Rescan();
        }

        return Apply() > 0;
    }

    // Todo sprite perto do jogador, ORDENADO POR ORDEM DE DESENHO — de tras para a frente,
    // que e literalmente o que a tela mostra. Marca quem esta no sistema e quem nao esta.
    //
    // Ler assim responde a pergunta de uma vez: se o personagem aparece ACIMA de um objeto
    // nesta lista, ele deveria estar na frente dele na tela. Se aparece abaixo, achamos o
    // culpado e sabemos o numero exato dele.
    public static void DumpAroundPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) { Debug.LogWarning("[Y-Sort] F8: nao achei objeto com a tag Player."); return; }

        Vector2 c = p.transform.position;
        const float radius = 12f;

        var rows = new List<(int order, string line)>();

        foreach (SpriteRenderer r in Object.FindObjectsByType<SpriteRenderer>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r.sprite == null) continue;
            if (Vector2.Distance(r.bounds.center, c) > radius) continue;

            Transform root = RootOf(r.transform);
            bool inSystem = byRoot.ContainsKey(root);
            string layer = SortingLayer.IDToName(r.sortingLayerID);
            if (string.IsNullOrEmpty(layer)) layer = "<camada inexistente>";

            rows.Add((r.sortingOrder,
                $"   order={r.sortingOrder,7}  camada={layer,-10} {(inSystem ? "[Y-SORT]" : "[intacto]")}  {r.name}  (raiz: {root.name})"));
        }

        rows.Sort((a, b) => a.order.CompareTo(b.order));

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Y-Sort] F8 — {rows.Count} sprites a menos de {radius} do jogador " +
                      $"(que esta em Y={c.y:F1}). De TRAS para a FRENTE:");
        foreach (var row in rows) sb.AppendLine(row.line);

        Debug.Log(sb.ToString());
    }

    public static void Clear()
    {
        byRoot.Clear();
        live.Clear();
        nextScan = 0f;
    }

    // ------------------------------------------------------------------ o trabalho

    // Reaproveita as entradas que ja existem — so o que e novo paga o custo de montar, e a
    // ordem relativa capturada na primeira vez nunca e relida. Isso importa: reler depois
    // que o sistema ja escreveu daria uma leitura contaminada.
    public static void Rescan()
    {
        seen.Clear();

        SpriteRenderer[] all = Object.FindObjectsByType<SpriteRenderer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (SpriteRenderer r in all)
        {
            if (ShouldSkip(r)) continue;

            Transform root = RootOf(r.transform);
            if (!seen.Add(root)) continue;

            if (byRoot.ContainsKey(root)) continue;

            Entry e = Build(root);
            if (e != null) byRoot.Add(root, e);
        }

        // Sai quem morreu, quem foi excluido no meio do caminho, e quem deixou de ser raiz
        // (mudou de pai).
        dead.Clear();
        foreach (var kv in byRoot)
            if (kv.Key == null || !seen.Contains(kv.Key)) dead.Add(kv.Key);
        foreach (Transform t in dead) byRoot.Remove(t);

        live.Clear();
        foreach (var kv in byRoot) live.Add(kv.Value);
    }

    // Devolve quantos objetos realmente mudaram de ordem neste tique — o editor usa isso
    // para so repintar a Scene View quando ha o que repintar.
    public static int Apply()
    {
        bool forceLayer = YSortSettings.ForcesLayer;
        int layerId = YSortSettings.LayerId;
        int changed = 0;

        for (int n = 0; n < live.Count; n++)
        {
            Entry e = live[n];
            if (e.root == null) continue;

            // Remedir enquanto a medida nao for confiavel. So um objeto ativo tem bounds
            // de verdade; ate la, o pivo (offset 0) e usado, que erra a altura mas nunca
            // joga o personagem para o outro lado do mapa.
            if (!e.anchorMeasured && e.root.gameObject.activeInHierarchy)
            {
                e.anchorOffsetY = AnchorOffsetFor(e.root, e.tuning, out bool ok);
                if (ok)
                {
                    e.anchorMeasured = true;
                    e.lastAnchor = float.NaN;   // forcar recalculo com a medida boa
                }
            }

            float anchor = e.root.position.y + e.anchorOffsetY;

            // Nada se mexeu: sair antes de escrever. E isto que faz 325 objetos parados
            // custarem uma comparacao de float cada por quadro, e nada mais.
            if (anchor.Equals(e.lastAnchor)) continue;
            e.lastAnchor = anchor;

            int order = YSortSettings.OrderFor(anchor);
            if (order == e.lastOrder) continue;
            e.lastOrder = order;
            changed++;

            for (int i = 0; i < e.renderers.Length; i++)
            {
                SpriteRenderer r = e.renderers[i];
                if (r == null) continue;   // sprite apagado depois da varredura

                int wanted = order + e.relative[i];
                if (r.sortingOrder != wanted) r.sortingOrder = wanted;

                // Sorting Layer manda MAIS que Sorting Order: com o Josh em "Objects", os
                // NPCs em "NPCs" e as arvores em "Default", nenhum personagem consegue
                // passar atras de uma arvore por mais que o Y diga que deveria. Tudo que
                // precisa se entrelacar tem que estar na MESMA camada.
                if (forceLayer && r.sortingLayerID != layerId) r.sortingLayerID = layerId;
            }
        }

        return changed;
    }

    // ------------------------------------------------------------------ montagem

    // Quem entra no sistema. Esta e a pergunta mais importante do arquivo inteiro, e a
    // primeira resposta que eu dei estava errada: "tudo que tem sprite" varreu junto a
    // estrada, a poca, a grama e o interior das casas. Chao ordenado por Y passa por cima
    // do personagem, porque o chao esta EMBAIXO dele na tela — e a regra, aplicada a uma
    // coisa deitada, produz exatamente o oposto do que se quer.
    //
    // A regra certa tem duas portas de entrada, e as duas sao fisicas — nada de adivinhar
    // por nome:
    //
    //  1. COLLIDER SOLIDO. "Isto ocupa espaco no mundo", que e a mesma coisa que "o
    //     personagem esbarra nisto, logo passa atras disto". Arvore, casa, cerca, pedra,
    //     movel, estatua.
    //
    //  2. RIGIDBODY2D. Quem se MOVE pelo mundo, mesmo sem colidir com nada. Foi isto que
    //     salvou a Haze: sendo o fantasma, ela atravessa tudo e so tem collider de trigger.
    //     Pela porta 1 sozinha, um personagem principal ficaria de fora do sistema.
    //
    // Estrada, poca, grama, sombra pintada no chao e sprite de interior nao tem nem um nem
    // outro, e ficam INTACTOS: nem a ordem nem a camada deles e tocada.
    public static bool Qualifies(Transform root, YSort tuning)
    {
        if (tuning != null && tuning.naoOrdenar) return false;
        if (tuning != null && tuning.incluirSemCollider) return true;

        // Um objeto alto demais e cenario que se atravessa por dentro (comodo, fachada),
        // nao algo que se contorna. Ordena-lo como unidade poe o comodo inteiro na frente
        // ou atras de quem esta dentro dele. Fica intacto, e os filhos se viram sozinhos.
        if (OwnHeight(root) > YSortSettings.MaxObjectHeight) return false;

        if (root.GetComponentInChildren<Rigidbody2D>(true) != null) return true;

        foreach (Collider2D c in root.GetComponentsInChildren<Collider2D>(true))
            if (!c.isTrigger) return true;

        return false;
    }

    private static Entry Build(Transform root)
    {
        YSort tuning = root.GetComponent<YSort>();
        if (!Qualifies(root, tuning)) return null;

        var found = new List<SpriteRenderer>();
        foreach (SpriteRenderer r in root.GetComponentsInChildren<SpriteRenderer>(true))
            if (!ShouldSkip(r)) found.Add(r);

        if (found.Count == 0) return null;

        // A ordem que a arte tem HOJE vira a ordem relativa dentro do objeto. O menor vira
        // zero, entao a arvore (1,2,3) e a arvore (11,12,13) viram a mesma coisa.
        var e = new Entry
        {
            root = root,
            tuning = tuning,
            renderers = found.ToArray(),
            relative = new int[found.Count],
            anchorOffsetY = AnchorOffsetFor(root, tuning)
        };

        int min = int.MaxValue;
        foreach (SpriteRenderer r in found)
            if (r.sortingOrder < min) min = r.sortingOrder;

        for (int i = 0; i < found.Count; i++)
            e.relative[i] = found[i].sortingOrder - min;

        return e;
    }

    // Onde este objeto encosta no chao, medido a partir do pivo. Medido UMA vez: depois
    // disso o objeto pode andar a vontade, que a distancia do pivo ate o pe nao muda.
    public static float AnchorOffsetFor(Transform root, YSort tuning)
    {
        return AnchorOffsetFor(root, tuning, out _);
    }

    // `reliable` sai false quando nao houve nada medivel — objeto desativado, sem collider
    // ativo e sem sprite com bounds de verdade. Quem chama deve tentar de novo depois em
    // vez de guardar a medida ruim.
    public static float AnchorOffsetFor(Transform root, YSort tuning, out bool reliable)
    {
        reliable = false;
        float nudge = tuning != null ? tuning.anchorOffsetY : 0f;

        if (tuning != null && tuning.ancora == YSort.Ancora.Pivo) { reliable = true; return nudge; }

        // 1) O collider solido e o melhor palpite: e a parte por onde o jogador esbarra.
        foreach (Collider2D c in root.GetComponentsInChildren<Collider2D>(true))
        {
            if (c.isTrigger || !c.isActiveAndEnabled) continue;
            if (c.bounds.size == Vector3.zero) continue;   // desativado: bounds nao valem
            reliable = true;
            return (c.bounds.min.y - root.position.y) + nudge;
        }

        // 2) Sem collider, a base da arte — para uma arvore isso e o pe da sombra, para um
        //    personagem sao os pes.
        bool any = false;
        float lowest = float.MaxValue;

        foreach (SpriteRenderer r in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (ShouldSkip(r) || r.sprite == null) continue;

            // Renderer de objeto desativado devolve bounds zerados. Aceitar isso produzia
            // um offset de (0 - posicaoY) — um personagem em Y=137 ganhava ancora 0 e
            // desenhava como se estivesse na origem do mundo.
            if (r.bounds.size == Vector3.zero) continue;

            lowest = Mathf.Min(lowest, r.bounds.min.y);
            any = true;
        }

        if (any)
        {
            float off = (lowest - root.position.y) + nudge;

            // Rede de seguranca: a distancia do pivo ate o pe de um objeto e da ordem de
            // alguns metros. Dezenas de unidades so acontece quando a medida foi feita com
            // bounds invalidos, e engolir isso joga o objeto para outra faixa de ordem.
            if (Mathf.Abs(off) <= 25f) { reliable = true; return off; }
        }

        // 3) Nem arte utilizavel: o pivo, que para quase tudo neste projeto ja e o pe.
        return nudge;
    }

    // O que se ordena e o OBJETO inteiro (arvore, personagem), nunca um sprite solto. Sobe
    // enquanto o pai ainda for parte do mesmo objeto; um pai que e so uma pasta de
    // organizacao ("Trees", "VillageObjects") nao tem sprite nem collider nem animator, e
    // nao deve virar um unico objeto ordenado com 200 arvores dentro.
    public static Transform RootOf(Transform t)
    {
        Transform root = t;

        while (root.parent != null && IsSameObject(root.parent))
            root = root.parent;

        return root;
    }

    private static bool IsSameObject(Transform parent)
    {
        bool part = parent.GetComponent<SpriteRenderer>() != null
                 || parent.GetComponent<Collider2D>() != null
                 || parent.GetComponent<Animator>() != null;

        if (!part) return false;

        // ...mas um pai GRANDE demais nao e "o mesmo objeto", e sim um cenario que contem
        // outros. O chao da casa do Elder e um sprite: sem este teste, o tapete, a escada,
        // o sofa e a estante subiam ate ele e viravam UM objeto so, com uma unica ordem,
        // desenhada por cima dos personagens que andam dentro do comodo.
        //
        // Arvore, movel e personagem tem poucos metros de altura. Comodo e fachada tem
        // dezenas. A diferenca e fisica, nao um palpite sobre nome.
        return OwnHeight(parent) <= YSortSettings.MaxObjectHeight;
    }

    // Altura do sprite DO PROPRIO objeto, sem os filhos. Zero quando nao da para medir
    // (objeto desativado, sem sprite) — e nesse caso o teste de tamanho nao reprova nada,
    // porque medir errado e pior que nao medir.
    private static float OwnHeight(Transform t)
    {
        SpriteRenderer r = t.GetComponent<SpriteRenderer>();
        if (r == null || r.sprite == null) return 0f;

        Vector3 size = r.bounds.size;
        return size == Vector3.zero ? 0f : size.y;
    }

    public static bool ShouldSkip(SpriteRenderer r)
    {
        string layer = SortingLayer.IDToName(r.sortingLayerID);

        foreach (string s in SkipLayers)
            if (layer == s) return true;

        if (r.GetComponentInParent<Canvas>() != null) return true;

        string name = r.gameObject.name.ToLowerInvariant();
        foreach (string s in SkipNameContains)
            if (name.Contains(s)) return true;

        return false;
    }
}
