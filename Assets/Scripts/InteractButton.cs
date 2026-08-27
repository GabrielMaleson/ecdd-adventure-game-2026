using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// The single interact prompt: a TMP label that shows "E" while something interactable
// is in range and is blank otherwise, driven by a centralized E keypress instead of a
// clickable UI Button. Every interactable (DialogueStarter, InteractDialogue,
// Teleporter, SceneLoadTrigger, StatueSwitch, Pickup) registers itself here instead of
// polling the key and writing the label directly — that used to mean six scripts all
// wrote the SAME label with no coordination, so one could silently stomp another's
// prompt. Now there's exactly one registration at a time, with an explicit owner.
//
// This component decides WHERE the prompt floats. What it LOOKS like — font and size —
// is not here: that lives in Assets/Resources/TextStyle.asset, one asset shared with every
// other piece of text in the game, so changing the font is one edit and not six.
//
// It deliberately moves the EXISTING Canvas label instead of spawning a world-space text
// object. An object added to the scene from outside the editor is lost the moment the
// editor saves a scene it loaded before that object existed, and the prompt then
// silently reverts with nothing to show for it. Owning no scene objects means there is
// nothing to lose.
public class InteractButton : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [Header("Onde o prompt fica")]
    [Tooltip("Ligado: o prompt paira sobre o objeto interagivel. Desligado: fica parado " +
             "onde o label estiver posicionado no Canvas.")]
    [SerializeField] private bool promptFollowsObject = true;

    [Tooltip("Folga entre o topo do sprite do objeto e o prompt, em unidades de mundo. " +
             "Um InteractPromptAnchor no objeto pode sobrepor isto caso a caso.")]
    [SerializeField] private float heightMargin = 0.35f;

    [Tooltip("Altura usada quando nao ha sprite nenhum para medir. Objetos com arte " +
             "ignoram isto e usam os proprios bounds.")]
    [SerializeField] private float fallbackHeight = 1.2f;

    [Tooltip("Opcional. Vazio = usa a camera com tag MainCamera, ou a primeira camera da " +
             "cena se nenhuma estiver com a tag.")]
    [SerializeField] private Camera cameraOverride;

    public static InteractButton Instance { get; private set; }

    // Fired on every press, AFTER the registered action (if any) already ran. Lets
    // other systems react to any interaction happening without needing to own the
    // prompt themselves (e.g. BarkDirector ducking the player's bark on interact).
    public event System.Action OnPressed;

    // Whichever caller currently owns the prompt — pass `this` from any MonoBehaviour.
    // Used only for identity checks in ClearInteraction, never dereferenced.
    private object owner;
    private System.Action onPress;

    // TODOS os interagiveis com o jogador dentro do alcance agora, e nao so o dono do
    // prompt.
    //
    // O prompt e um so, mas os alcances se sobrepoem o tempo todo: o poco fica dentro do
    // alcance do Amos, que fica dentro do alcance dos amigos. Cada um deles se registra ao
    // ENTRAR no proprio trigger — e ninguem entra duas vezes.
    //
    // Sem esta lista, quem chegou por ultimo tomava o prompt e, ao ir embora, devolvia para
    // NINGUEM: o poco continuava com o jogador parado em cima dele e sem E, ate ele sair do
    // collider e voltar. Era permanente na pratica, porque nada obriga o jogador a sair.
    //
    // Com a lista, sair de um alcance PROMOVE quem continua ao alcance. O mais recente
    // ganha, que e o que corresponde a "o que eu acabei de chegar perto".
    private class Candidato
    {
        public object dono;
        public string rotulo;
        public System.Action acao;
        public Transform ancora;
    }

    private readonly List<Candidato> candidatos = new List<Candidato>();

    private int IndiceDe(object caller)
    {
        for (int i = 0; i < candidatos.Count; i++)
            if (ReferenceEquals(candidatos[i].dono, caller)) return i;
        return -1;
    }

    // Um candidato so vale enquanto o objeto dele existir e estiver ligado. Sem esta poda,
    // desligar um NPC com o jogador ao lado deixaria um fantasma na fila, pronto para ser
    // promovido e oferecer um E que nao faz nada.
    private static bool Vivo(Candidato c)
    {
        if (c == null || c.acao == null) return false;

        // isActiveAndEnabled e de Behaviour, nao de Component: um Collider2D nao tem.
        // Por isso as duas checagens — o componente desligado no Inspector nao vale, e o
        // objeto desligado na cena tambem nao.
        if (c.dono is Behaviour b) return b != null && b.isActiveAndEnabled;
        if (c.dono is Component comp) return comp != null && comp.gameObject.activeInHierarchy;
        return c.dono != null;
    }

    // Assume o candidato mais recente que ainda estiver de pe.
    private void PromoverOutro()
    {
        for (int i = candidatos.Count - 1; i >= 0; i--)
        {
            if (!Vivo(candidatos[i])) { candidatos.RemoveAt(i); continue; }

            Candidato c = candidatos[i];
            Adotar(c.dono, c.rotulo, c.acao, c.ancora);
            return;
        }

        owner = null;
        onPress = null;
        anchor = null;
        anchorOverride = null;
        Hide();
    }

    // Resolved once per registration. The per-object override is re-asked every frame
    // instead, because "nearest of these two" changes as the player walks.
    private Transform anchor;
    private InteractPromptAnchor anchorOverride;

    private Camera cam;
    private Transform player;

    private void Awake()
    {
        Instance = this;

        ApplyTextStyle();
        Hide();
    }

    private void OnValidate()
    {
        ApplyTextStyle();
    }

    // Font and size for the prompt live in Assets/Resources/TextStyle.asset, alongside
    // every other piece of text in the game. Public so editing that asset can push the
    // change straight into the open scene.
    public void ApplyTextStyle()
    {
        TextStyle.Apply(label, TextStyle.Role.InteractPrompt);
    }

    // Fala na tela: o E some e o E nao responde. Some porque disputa a atencao com a fala;
    // nao responde porque um aperto no meio da frase dispara a interacao seguinte sem que
    // ninguem veja o que aconteceu.
    private static bool Suppressed => BarkDirector.AnyBarkShowing || BarkConversation.AnyRunning
                                      || TutorialHint.DialogoBloqueandoInput;

    private void Update()
    {
        if (onPress == null || Suppressed)
            return;

        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame)
            Press();
    }

    // After everything has moved for the frame, so the prompt can't lag behind a crate
    // being pushed or an NPC walking out from under it.
    private void LateUpdate()
    {
        if (label == null) return;

        // Apagar o ROTULO, e nao o registro. Zerar o registro obrigaria quem registrou a
        // fazer tudo de novo quando a fala acabasse — e ninguem faz. Assim o E volta
        // sozinho, sobre o mesmo objeto, no instante em que a fala sai da tela.
        if (onPress != null && Suppressed)
        {
            label.enabled = false;
            return;
        }

        if (onPress != null && !label.enabled && !string.IsNullOrEmpty(label.text))
            label.enabled = true;

        if (!promptFollowsObject || onPress == null)
            return;

        Transform target = CurrentTarget();

        // The interactable was switched off while the prompt was up — <<disable ElderAmos>>
        // with the player standing next to him is exactly this. Its OnTriggerExit never
        // runs, so without this the prompt would hang in the air over nobody, still armed.
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            int i = IndiceDe(owner);
            if (i >= 0) candidatos.RemoveAt(i);
            PromoverOutro();
            return;
        }

        PlaceOver(target);
    }

    // Registers what's currently interactable: who's registering (pass `this`), the
    // label to show, and what happens when E is pressed. Calling this again — e.g. a
    // different interactable now in range — simply replaces whatever was registered
    // before, no need to clear first.
    //
    // promptAnchor is only for an interactable whose OWN transform is not where its art
    // is: a trigger zone laid over a stretch of ground. Everything whose transform is
    // the object itself leaves it out and gets the right answer for free. An
    // InteractPromptAnchor component on the object beats both.
    public void SetInteraction(object caller, string labelText, System.Action action, Transform promptAnchor = null)
    {
        int ja = IndiceDe(caller);
        if (ja >= 0) candidatos.RemoveAt(ja);
        candidatos.Add(new Candidato
        {
            dono = caller, rotulo = labelText, acao = action, ancora = promptAnchor
        });

        Adotar(caller, labelText, action, promptAnchor);
    }

    // O registro em si, sem mexer na fila de candidatos — para a promocao poder reutiliza-lo.
    private void Adotar(object caller, string labelText, System.Action action, Transform promptAnchor)
    {
        owner = caller;
        onPress = action;

        Component callerComponent = caller as Component;

        anchorOverride = callerComponent != null
            ? callerComponent.GetComponentInParent<InteractPromptAnchor>()
            : null;

        anchor = promptAnchor != null ? promptAnchor : callerComponent?.transform;

        SetLabel(labelText);
        if (label != null) label.enabled = !string.IsNullOrEmpty(labelText);

        if (promptFollowsObject)
        {
            Transform target = CurrentTarget();
            if (target != null) PlaceOver(target);

            // Sem isto, "o E aparece no lugar errado" nao tem como ser diagnosticado: quem
            // decide a posicao sao TRES coisas em lugares diferentes (o registrante, o
            // promptAnchor que ele passou, e um InteractPromptAnchor que pode estar em
            // qualquer ancestral dele), e do lado de fora as tres sao indistinguiveis.
            Debug.Log($"[InteractButton] registrou '{labelText}' de " +
                      $"{(callerComponent != null ? callerComponent.GetType().Name : "?")} " +
                      $"em '{(callerComponent != null ? callerComponent.name : "?")}' | " +
                      $"promptAnchor={(promptAnchor != null ? promptAnchor.name : "-")} | " +
                      $"InteractPromptAnchor={(anchorOverride != null ? anchorOverride.name : "NENHUM")} | " +
                      $"ancora final='{(target != null ? target.name : "-")}' em " +
                      $"{(target != null ? target.position.ToString() : "-")}",
                      callerComponent);
        }
    }

    // Only clears if the caller is still the current owner — an old registration's
    // late exit (its trigger fires after a NEWER interactable already took over the
    // prompt) must not blank out someone else's active prompt.
    public void ClearInteraction(object caller)
    {
        int i = IndiceDe(caller);
        if (i >= 0) candidatos.RemoveAt(i);

        // Saida atrasada de um registro velho — o trigger dele dispara DEPOIS de outro ja
        // ter tomado o prompt. Tirar da fila basta; apagar o prompt de quem esta valendo
        // agora seria errado.
        if (owner != caller) return;

        PromoverOutro();
    }

    private Transform CurrentTarget()
    {
        if (anchorOverride != null)
            return anchorOverride.Resolve(Player());

        return anchor;
    }

    private void PlaceOver(Transform target)
    {
        if (cam == null) cam = ResolveCamera();
        if (cam == null || label == null) return;

        Vector3 screen = cam.WorldToScreenPoint(TopOf(target));

        // The canvas is Screen Space - Overlay, where a RectTransform's world position IS
        // a pixel coordinate. Z has to be flattened or the label is pushed off the canvas
        // plane and stops drawing.
        screen.z = 0f;
        label.rectTransform.position = screen;
    }

    // Top-centre of the object's artwork, plus the margin. Measured from the SPRITE and
    // not the transform because a transform tells you nothing about how tall a thing is:
    // the pivot of a 64px character at scale 4 sits in the middle of its own body, while
    // a key lying on the floor is a few pixels tall. Reading the bounds is what makes one
    // setting look right on a statue, an NPC and a dropped key at once.
    private Vector3 TopOf(Transform target)
    {
        // Per-object override first, then the shared asset, then this component's own
        // field as the last word — so the height lives in the same place as every other
        // text setting, without breaking a scene that has no asset.
        float margin = heightMargin;
        if (TextStyle.Current != null) margin = TextStyle.Current.promptHeight;
        if (anchorOverride != null && anchorOverride.overrideHeightMargin)
            margin = anchorOverride.heightMargin;

        Vector2 extra = anchorOverride != null ? anchorOverride.offset : Vector2.zero;
        bool useBounds = anchorOverride == null || anchorOverride.useSpriteBounds;

        if (useBounds && VisibleArt.TryGetBounds(target, out Bounds bounds))
            return new Vector3(bounds.center.x + extra.x, bounds.max.y + margin + extra.y, 0f);

        // Nothing to measure — a bare trigger with no artwork under it. Nothing better
        // than a number, which is exactly what an InteractPromptAnchor is for.
        return new Vector3(target.position.x + extra.x, target.position.y + fallbackHeight + extra.y, 0f);
    }

    // Camera.main alone is not enough here: it resolves by TAG, and this scene's camera
    // object is NAMED "MainCamera" while being tagged Untagged — so Camera.main is null
    // and the prompt silently never moves.
    private Camera ResolveCamera()
    {
        if (cameraOverride != null) return cameraOverride;

        Camera tagged = Camera.main;
        if (tagged != null) return tagged;

        return FindFirstObjectByType<Camera>();
    }

    private Transform Player()
    {
        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            player = go != null ? go.transform : null;
        }
        return player;
    }

    // Nothing in range means nothing on screen. Switching the component off rather than
    // only blanking the text also stops it laying out an empty mesh every frame.
    private void Hide()
    {
        if (label == null) return;
        label.text = string.Empty;
        label.enabled = false;
    }

    private void SetLabel(string text)
    {
        if (label != null)
            label.text = text;
    }

    private void Press()
    {
        // Blank and drop the registration immediately, so a held/spammed E can't
        // re-trigger mid-interaction. Whatever registered this is responsible for
        // calling SetInteraction again once it's ready to be interacted with again
        // (e.g. once dialogue ends and the player is still in range).
        System.Action action = onPress;

        // Sai da fila junto: senao a promocao logo abaixo devolveria o E para ele mesmo no
        // mesmo quadro, e o anti-repeticao de segurar o E deixaria de existir. Quem quiser
        // o E de volta chama SetInteraction de novo — que e o contrato que ja existia.
        int i = IndiceDe(owner);
        if (i >= 0) candidatos.RemoveAt(i);

        owner = null;
        onPress = null;
        anchor = null;
        anchorOverride = null;
        Hide();

        action?.Invoke();
        OnPressed?.Invoke();

        // Ninguem reassumiu o prompt: passa para quem ainda estiver ao alcance.
        //
        // A acao acima MUITAS vezes se re-registra sozinha (o poco, para poder passar as
        // linhas em sequencia), e nesse caso onPress ja nao e nulo e nao ha nada a fazer.
        // Mas uma interacao de uma vez so — o Amos, os amigos — nunca volta, e sem esta
        // promocao o E some da tela e nao volta mais: o poco continua na fila, com o
        // jogador parado em cima dele, e sem dono. Era exatamente esse o sintoma.
        if (onPress == null) PromoverOutro();
    }
}
