using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Yarn.Unity;

// TODAS as falas de bark de UM personagem, no jogo inteiro, em um componente so.
//
// ------------------------------------------------------------------ por que existe
//
// O BarkTrigger e uma acao: um componente, uma fala. Isso e certo para um gatilho de chao
// ("ao pisar aqui, a Erika chama"), e errado para um personagem: o Marcus fala em dezenas
// de momentos ao longo do jogo, e um componente por fala deixaria trinta BarkTriggers
// empilhados nele ate o fim do roteiro.
//
// Aqui e o contrario: UM componente, uma LISTA de falas, e a lista decide sozinha qual
// serve agora.
//
// ------------------------------------------------------------------ as duas ideias
//
// CONDICAO — qual entrada vale AGORA. A busca e de cima para baixo e para na primeira
// entrada cujas condicoes batem. Entao a ordem da lista e a ordem da HISTORIA: as falas
// mais avancadas em cima, as genericas embaixo. O mesmo E na mesma porta diz uma coisa no
// comeco do jogo e outra depois que a nevoa apertou, sem nenhum gatilho novo.
//
// SEQUENCIA — dentro de uma entrada, varias linhas ditas EM ORDEM, uma por interacao.
// E o caso "Bookshelf 2" do roteiro: a primeira vez que o Josh olha a estante ele acha um
// romance safado, a segunda vez ele acha outro. Sem isso, cada "segunda vez" viraria um
// gatilho novo com uma flag de progresso so para lembrar que ja aconteceu.
//
// ------------------------------------------------------------------ como se usa
//
// No personagem (ou no objeto da conversa), junto do CharacterDialogue dele. Chamado por:
//   - UnityEvent:  InteractAction > On Interact > BarkBook > Speak ()
//   - Yarn:        <<barkbook marcus>>            (a entrada que valer agora)
//                  <<barkbook marcus mesa>>       (a entrada de id "mesa")
//
// Quem DESENHA a fala continua sendo o CharacterDialogue com o Bark Id igual ao Speaker Id
// daqui. Este componente so escolhe o que dizer.
[DisallowMultipleComponent]
public class BarkBook : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("Opcional. So serve para chamar esta entrada pelo nome: <<barkbook marcus mesa>>. " +
                 "Vazio = so e escolhida pela condicao.")]
        public string id;

        [Tooltip("Ditas EM ORDEM, uma por interacao. Uma linha so = ele repete sempre a mesma. " +
                 "Varias = a fala avanca a cada vez que voce fala com ele.")]
        [TextArea(1, 4)]
        public List<string> lines = new List<string>();

        [Tooltip("Ao acabar as linhas: ligado, repete a ultima para sempre; desligado, esta " +
                 "entrada se esgota e a proxima que servir assume.")]
        public bool repeatLastLine = true;

        [Header("Quando esta entrada vale")]
        [Tooltip("So vale com este progresso gravado (SaveManager). Vazio = sem condicao.")]
        public string requiresProgress;

        [Tooltip("So vale enquanto esta variavel de Yarn for true. Escreva COM o cifrao.")]
        public string requiresYarnVariable;

        [Tooltip("Inverte as duas condicoes acima — vale so enquanto elas NAO batem.")]
        public bool invertCondition;

        [Tooltip("Falas de roteiro ganham de conversa fiada. Baixe para Reactive quando for " +
                 "tempero de fundo que pode ser interrompido.")]
        public BarkPriority priority = BarkPriority.Scripted;

        // Nao serializados de proposito: sao estado de uma partida, nao autoria. Serializar
        // faria o teste de ontem no editor decidir o que o jogador ouve hoje.
        [System.NonSerialized] public int cursor;
        [System.NonSerialized] public bool spent;

        // Quantas falas desta entrada JA SAIRAM, sem teto.
        //
        // Separado do cursor de proposito: com repeatLastLine ligado o cursor trava na
        // ultima linha, entao ele nao sabe dizer se o personagem ja falou tudo ou se esta
        // repetindo a ultima para sempre. Esta contagem sabe, e e ela que faz o E sumir
        // quando nao ha mais nada NOVO para ouvir.
        [System.NonSerialized] public int ditas;
    }

    [Header("Quem fala")]
    [Tooltip("Tem que bater com o Bark Id do CharacterDialogue deste personagem. " +
             "Vazio = tenta achar o CharacterDialogue aqui ou no pai.")]
    public string speakerId;

    [Header("As falas, da mais avancada para a mais generica")]
    [Tooltip("A busca para na PRIMEIRA entrada cujas condicoes batem. Ponha as falas de " +
             "momentos avancados da historia EM CIMA, e as genericas embaixo.")]
    public List<Entry> entries = new List<Entry>();

    [Header("Opcional")]
    [Tooltip("Disparado toda vez que ele realmente fala.")]
    public UnityEvent onSpoke;

    private static readonly List<BarkBook> allInstances = new List<BarkBook>();

    public string SpeakerId => Id;

    // Ainda ha alguma fala que sirva AGORA?
    //
    // Falso quando toda entrada elegivel ja se esgotou. E o que permite ao prompt sumir em
    // vez de continuar oferecendo um E que so repete a ultima frase para sempre — um
    // personagem que ja disse tudo o que tinha para dizer naquele momento da historia nao
    // deveria continuar convidando o jogador a falar com ele.
    //
    // Depende de repeatLastLine estar DESLIGADO na entrada: ligado, ela nunca se esgota,
    // que e exatamente o que se quer num objeto de exploracao (o poco) e nao num beat.
    public bool TemAlgoADizer
    {
        get
        {
            foreach (Entry e in entries)
            {
                if (e.spent) continue;
                if (e.lines == null || e.lines.Count == 0) continue;
                if (!ConditionsMet(e)) continue;

                // Entrada cujas falas ja sairam todas nao conta como "tem algo a dizer",
                // mesmo com repeatLastLine ligado. Repetir a ultima frase para sempre e
                // util num objeto de exploracao; num personagem, e um E que convida para
                // uma conversa que ja acabou.
                if (e.ditas >= e.lines.Count) continue;

                return true;
            }

            return false;
        }
    }

    private string Id
    {
        get
        {
            if (!string.IsNullOrEmpty(speakerId)) return speakerId;

            CharacterDialogue cd = GetComponent<CharacterDialogue>();
            if (cd == null) cd = GetComponentInParent<CharacterDialogue>();
            return cd != null ? cd.barkId : name;
        }
    }

    private void OnEnable() => allInstances.Add(this);
    private void OnDisable() => allInstances.Remove(this);

    // ------------------------------------------------------------------ API

    // Sem argumento: a entrada que valer agora. E esta que vai no UnityEvent do E.
    public void Speak() => SpeakEntry(PickEligible());

    public void SpeakId(string entryId)
    {
        foreach (Entry e in entries)
            if (string.Equals(e.id, entryId, System.StringComparison.OrdinalIgnoreCase))
            { SpeakEntry(e); return; }

        Debug.LogWarning($"BarkBook em '{name}': nao existe entrada com id '{entryId}'.", this);
    }

    // Volta tudo ao inicio — util num reset de puzzle ou ao recarregar um save.
    public void Rearm()
    {
        foreach (Entry e in entries) { e.cursor = 0; e.spent = false; e.ditas = 0; }
    }

    // Yarn: <<barkbook marcus>> ou <<barkbook marcus mesa>>
    [YarnCommand("barkbook")]
    public static void BarkBookCommand(string who, string entryId = "")
    {
        BarkBook book = null;

        foreach (BarkBook b in allInstances)
            if (string.Equals(b.Id, who, System.StringComparison.OrdinalIgnoreCase))
            { book = b; break; }

        if (book == null)
        {
            Debug.LogWarning($"<<barkbook {who}>>: nenhum BarkBook ligado com esse id.");
            return;
        }

        if (string.IsNullOrEmpty(entryId)) book.Speak();
        else book.SpeakId(entryId);
    }

    // ------------------------------------------------------------------ escolha

    private Entry PickEligible()
    {
        foreach (Entry e in entries)
        {
            if (e.spent) continue;
            if (e.lines == null || e.lines.Count == 0) continue;
            if (!ConditionsMet(e)) continue;
            return e;
        }
        return null;
    }

    // Mesmas duas condicoes que o BarkTrigger e o BarkSet ja usam. De proposito: um terceiro
    // sistema de flags seria mais uma coisa para desencontrar.
    private bool ConditionsMet(Entry e)
    {
        bool met = true;

        if (!string.IsNullOrEmpty(e.requiresProgress))
            met &= SaveManager.Instance != null && SaveManager.Instance.HasProgress(e.requiresProgress);

        if (met && !string.IsNullOrEmpty(e.requiresYarnVariable))
            met &= BarkDirector.GetYarnBool(e.requiresYarnVariable);

        return e.invertCondition ? !met : met;
    }

    private void SpeakEntry(Entry e)
    {
        // Falhas mudas: sem entrada elegivel, ou entrada sem linha, nada acontecia e nada
        // era dito. Do lado de fora isso e identico a "o E nao funciona".
        if (e == null)
        {
            Debug.LogWarning($"BarkBook em '{name}': Speak() foi chamado, mas NENHUMA entrada " +
                             $"serve agora ({entries.Count} entrada(s) na lista). Ou a lista " +
                             "esta vazia, ou todas ja se esgotaram, ou as condicoes " +
                             "(requiresProgress / requiresYarnVariable) nao batem.", this);
            return;
        }

        if (e.lines == null || e.lines.Count == 0)
        {
            Debug.LogWarning($"BarkBook em '{name}': a entrada '{e.id}' foi escolhida mas nao " +
                             "tem nenhuma linha escrita.", this);
            return;
        }

        int i = Mathf.Clamp(e.cursor, 0, e.lines.Count - 1);
        string line = e.lines[i];

        // So avanca se a fala saiu de verdade. Uma fala recusada (personagem desligado,
        // sem balao, prioridade menor que a que esta na tela) nao pode consumir uma linha
        // da sequencia: era isso que pulava a linha 2 e ainda esgotava a entrada.
        if (BarkDirector.Bark(Id, line, e.priority) <= 0f) return;

        onSpoke?.Invoke();

        e.ditas++;
        e.cursor++;

        // Acabou a lista: ou trava na ultima, ou a entrada sai de cena e a proxima que
        // servir assume na proxima interacao.
        if (e.cursor >= e.lines.Count)
        {
            if (e.repeatLastLine) e.cursor = e.lines.Count - 1;
            else e.spent = true;
        }
    }
}
