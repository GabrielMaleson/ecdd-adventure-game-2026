using UnityEngine;
using TMPro;

// ONE asset that decides the font and size of every piece of text the game draws in the
// world: the interact prompt, the floating barks, the examine text on props.
//
// It lives at Assets/Resources/TextStyle.asset and is found by Resources.Load, so nothing
// has to be dragged into a field anywhere. Add a new floating text somewhere, call
// TextStyle.Apply on it, and it is already following the same rules as everything else.
//
// Sizes are per ROLE and not one number, because the units genuinely differ: the interact
// prompt is UI text measured in screen pixels on a Canvas, while a bark is world-space
// text measured in world units. One value cannot serve both. A size left at 0 means
// "leave whatever that object already had" — so turning a knob is opt-in and nothing
// silently resizes behind you.
[CreateAssetMenu(fileName = "TextStyle", menuName = "ECDD/Text Style")]
public class TextStyle : ScriptableObject
{
    [Header("Fonte")]
    [Tooltip("Fonte do texto que os personagens e objetos FALAM (barks e examine). " +
             "Vazio = cada objeto mantem a que ja tem.")]
    public TMP_FontAsset font;

    [Tooltip("Fonte so do prompt de interacao (o E). Vazio = usa a de cima. Preencha para " +
             "o E nao parecer fala.")]
    public TMP_FontAsset promptFont;

    [Header("Cor")]
    [Tooltip("Cor do prompt de interacao (o E).")]
    public Color promptColor = Color.white;

    [Tooltip("Cor do texto falado (barks e examine).")]
    public Color worldTextColor = Color.white;

    [Header("Tamanhos — 0 = nao mexe")]
    [Tooltip("Prompt de interacao (o E). Esta e a unidade de referencia: o campo abaixo usa " +
             "a MESMA escala.")]
    public float interactPromptSize = 20f;

    [Tooltip("Todo texto flutuante no mundo: falas de personagem (barks) E texto de " +
             "objeto examinado. Um numero so, porque os dois tem que ser iguais.\n\n" +
             "MESMA ESCALA do prompt acima — 20 aqui e 20 la sao o mesmo tamanho na tela.")]
    public float worldTextSize = 20f;

    [Tooltip("Quanto vale UM ponto da escala do prompt em tamanho de fonte no mundo.\n\n" +
             "Existe porque o prompt vive num Canvas e a fala vive no mundo: sao duas " +
             "unidades diferentes, e sem um fator ligando as duas os campos acima nao " +
             "poderiam usar o mesmo numero. 0.45 e a calibragem dos valores que ja estavam " +
             "certos aqui (prompt 20, fala 9).\n\n" +
             "So mexa se a fala e o prompt pararem de casar depois de trocar a fonte ou o " +
             "zoom da camera. Para mudar o tamanho da fala, mexa no campo de cima.")]
    public float worldTextScale = 0.45f;

    [Header("Posicao — a partir do TOPO DA ARTE, em unidades")]
    [Tooltip("Deslocamento HORIZONTAL do texto falado. Positivo vai para a direita. "
           + "O padrao 0 centraliza no objeto.")]
    public float worldTextOffsetX;

    [Tooltip("Altura do texto FALADO (barks e examine).")]
    public float worldTextHeight = 0.35f;

    [Tooltip("Altura do PROMPT (o E). Separada porque o E costuma querer ficar mais alto " +
             "que a fala, ou o contrario.")]
    public float promptHeight = 0.35f;

    [Header("Tempo — 0 = nao mexe")]
    [Tooltip("Quanto tempo a fala fica na tela antes de sumir.")]
    public float holdDuration = 1.5f;

    [Tooltip("Duracao do fade OUT, em segundos. A fala nao se move mais — ela so apaga no "
           + "lugar. Valores baixos (0.1-0.2) somem quase na hora.")]
    public float fadeOutDuration = 0.15f;

    private const string ResourcePath = "TextStyle";

    private static TextStyle cached;
    private static bool lookedUp;

    // Null when the asset is missing, and every caller is written to cope with that —
    // deleting Assets/Resources/TextStyle.asset reverts the whole game to whatever each
    // object had authored on it, with no code change and no errors.
    public static TextStyle Current
    {
        get
        {
            if (!lookedUp)
            {
                cached = Resources.Load<TextStyle>(ResourcePath);
                lookedUp = true;
            }
            return cached;
        }
    }

    // Two roles and not three: bark and examine text are both floating world text and are
    // meant to read as the same voice, so they share one number. The prompt is separate
    // only because it is UI on a Canvas, measured in screen pixels rather than units.
    public enum Role { InteractPrompt, WorldText }

    // The single entry point. Safe to call on null text and with no asset present.
    public static void Apply(TMP_Text text, Role role)
    {
        if (text == null) return;

        TextStyle style = Current;
        if (style == null) return;

        // The prompt is a UI symbol and the rest is speech. Sharing one look makes the E
        // read as another line of dialogue, so each side gets its own font and colour.
        TMP_FontAsset wanted = role == Role.InteractPrompt && style.promptFont != null
            ? style.promptFont
            : style.font;

        if (wanted != null && text.font != wanted)
            text.font = wanted;

        text.color = role == Role.InteractPrompt ? style.promptColor : style.worldTextColor;

        float size = style.SizeFor(role);
        if (size > 0f)
        {
            // O prompt e a referencia e vai CRU — ele ja estava certo em 20 e nada aqui
            // mexe nele. Quem converte e a fala, multiplicando pelo fator calibrado.
            if (role == Role.WorldText)
                size *= Mathf.Max(0.0001f, style.worldTextScale);

            text.fontSize = size;
            // Auto-sizing silently overrides fontSize, so a size set here would look
            // like it did nothing at all on any object that happens to have it on.
            text.enableAutoSizing = false;
        }

        if (role == Role.WorldText)
        {
            // O MESMO retangulo, pivo e alinhamento para todo texto de mundo.
            //
            // Era daqui que vinha a diferenca entre a fala do Josh e a dos amigos, e nao da
            // posicao: os balcoes estavam a 0.06 um do outro, medido. O retangulo tem 5
            // units de altura com pivo no centro, entao vai de y-2.5 a y+2.5 — e o texto e
            // desenhado no TOPO ou no FUNDO dele conforme o alinhamento. Os prefabs do
            // Marcus e da Erika tinham VerticalAlignment=Top (desenha em +2.5); o balao
            // criado em tempo de execucao para o Josh vinha com Bottom (-2.5). Cinco units
            // de diferenca com a posicao identica.
            //
            // Normalizar os tres valores aqui e o que faz "a mesma altura" significar a
            // mesma coisa na tela, seja o balao autorado no prefab ou criado na hora.
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(20f, 5f);
            text.alignment = TMPro.TextAlignmentOptions.Top;

            // Uma linha so, sem quebra. Com quebra, uma frase longa se dobra dentro da
            // caixa e passa a ocupar duas linhas menores — o mesmo tamanho de fonte
            // rendendo dois tamanhos diferentes na tela conforme o comprimento da fala.
            // Sem quebra, a fala cresce para os lados e o corpo da letra nunca muda.
            text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            text.overflowMode = TMPro.TextOverflowModes.Overflow;
        }
    }


    // Builds the floating text object an InteractDialogue / CharacterDialogue needs, so
    // nobody has to create a TextMeshPro child by hand and drag it into a field for every
    // examinable barrel in the village. Font, size and height all come from the same
    // rules everything else uses.
    //
    // A field left empty in the Inspector now means "make me one", not "error". Assigning
    // one by hand still wins — this only ever runs when the field is empty, so any label
    // that was placed deliberately is left completely alone.
    public static TMPro.TextMeshPro CreateWorldLabel(Transform owner, string name, float heightAbove = -1f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(owner, false);

        var text = go.AddComponent<TMPro.TextMeshPro>();
        text.text = string.Empty;

        Apply(text, Role.WorldText);

        // UM caminho de posicionamento para todo mundo.
        //
        // Antes esta funcao posicionava por conta propria, e o PlaceWorldLabel fazia o mesmo
        // com outro codigo. Balao autorado no prefab (Marcus, Erika) ia por um; balao criado
        // aqui (o Josh) ia pelo outro. Duas contas parecidas mas nao iguais — a daqui nunca
        // somou os offsets — e por isso a fala do Josh nao ficava na mesma altura por mais
        // que o valor global fosse o mesmo.
        //
        // O parametro heightAbove ainda existe para quem quiser uma altura avulsa, mas o
        // padrao (-1) NAO significa mais "sem valor": worldTextHeight e negativo neste
        // projeto, entao usar negativo como sentinela era uma armadilha esperando acontecer.
        PlaceWorldLabel(owner, text, 0f, heightAbove > -0.5f && heightAbove != -1f ? heightAbove : 0f);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null && SortingLayer.NameToID("Dialogue") != 0)
        {
            renderer.sortingLayerName = "Dialogue";
            renderer.sortingOrder = 5;
        }

        return text;
    }

    // Puts an EXISTING floating label at the standard height above the owner's artwork.
    public static void PlaceWorldLabel(Transform owner, TMPro.TextMeshPro text, float extraX = 0f, float extraY = 0f)
    {
        if (owner == null || text == null) return;

        TextStyle style = Current;
        if (style == null) return;

        // Escala do MUNDO igual a 1, seja qual for a escala do dono.
        //
        // CreateWorldLabel ja fazia isso, mas so roda quando o campo esta vazio. Um balao
        // arrastado a mao no prefab nunca passava por la e herdava a escala do dono — e os
        // personagens deste projeto estao em escala 4. Era por isso que duas falas com o
        // MESMO tamanho 20 apareciam com tamanhos completamente diferentes na tela: nao era
        // o tamanho da fonte, era a escala herdada.
        // Escala do mundo 1, seja qual for a escala do dono. Personagens deste projeto
        // estao em escala 4 no visual; um balao herdando isso renderiza quatro vezes maior.
        NormalizeScale(text.transform);

        Vector3 spot = VisibleArt.TryGetBounds(owner, out Bounds bounds)
            ? new Vector3(bounds.center.x, bounds.max.y, 0f)
            : new Vector3(owner.position.x, owner.position.y, 0f);

        text.transform.position = new Vector3(
            spot.x + style.worldTextOffsetX + extraX,
            spot.y + style.worldTextHeight + extraY,
            text.transform.position.z);
    }

    // Anula a escala do pai, para o texto sair sempre do mesmo tamanho na tela.
    private static void NormalizeScale(Transform t)
    {
        Transform parent = t.parent;
        Vector3 s = parent != null ? parent.lossyScale : Vector3.one;

        t.localScale = new Vector3(
            s.x != 0f ? 1f / s.x : 1f,
            s.y != 0f ? 1f / s.y : 1f,
            s.z != 0f ? 1f / s.z : 1f);
    }

    private float SizeFor(Role role)
    {
        switch (role)
        {
            case Role.InteractPrompt: return interactPromptSize;
            case Role.WorldText:      return worldTextSize;
            default:                  return 0f;
        }
    }

#if UNITY_EDITOR
    // Editing the asset re-applies it to everything already in the open scene, so the
    // change is visible immediately instead of only after entering Play.
    private void OnValidate()
    {
        cached = this;
        lookedUp = true;

        foreach (InteractButton button in FindObjectsByType<InteractButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            button.ApplyTextStyle();

        foreach (CharacterDialogue bark in FindObjectsByType<CharacterDialogue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            bark.ApplyTextStyle();

        // InteractDialogue nao aparece mais aqui: objeto examinavel nao desenha texto
        // proprio. A fala dele sai pela boca do Josh, entao quem ja foi reestilizado no
        // laco acima — o CharacterDialogue dele — cobre esse caso tambem.
    }
#endif
}
