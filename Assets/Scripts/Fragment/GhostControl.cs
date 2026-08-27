using UnityEngine;
using UnityEngine.InputSystem;

// Coordinates WHO the player is driving right now: the MC, or the Fragment (ghost).
//
// Three states, cycled with ONE key (Q by default):
//   FOLLOW   - normal play. You drive the MC; the ghost trails him (FragmentFollow).
//   PILOTING - you drive the GHOST with WASD; the MC stands still; the camera looks
//              at the ghost. Use this to walk the ghost up to a statue.
//   PARKED   - the ghost stays put where you left it; camera + control return to the
//              MC. While parked inside a statue's range, pressing E turns that statue
//              (see StatueSwitch), so the MC can reposition between turns.
//
// Q advances FOLLOW -> PILOTING -> PARKED -> FOLLOW.
//
// The camera is handled WITHOUT hard-wiring a camera system: whenever the target
// changes, onCameraTargetChanged fires with the transform to look at (the ghost
// while piloting, the MC otherwise). Wire that event to your camera in the scene
// (a Cinemachine vcam's Follow, or a manual follow script). CameraTarget also
// exposes the same transform as a property if code needs it.
public class GhostControl : MonoBehaviour
{
    public enum Mode { Follow, Piloting, Parked }

    [Header("Refs")]
    [SerializeField] PlayerController mc;
    [SerializeField] FragmentFollow   ghostFollow;   // the follow behaviour on the ghost
    [SerializeField] Transform        ghost;         // the ghost's root transform

    [Header("Quando o Q existe")]
    [Tooltip("O Q so funciona depois deste progresso ser gravado (SaveManager). " +
             "Vazio = funciona desde o comeco do jogo.")]
    [SerializeField] string requiresProgress = "GhostControlUnlocked";

    [Header("Tuning")]
    [SerializeField] float ghostMoveSpeed = 4f;      // world units / second while piloting
    [SerializeField] Key   toggleKey      = Key.Q;

    [Tooltip("Fires whenever the camera should look at a different transform: the ghost while piloting, the MC otherwise. Wire this to your camera.")]
    public TransformEvent onCameraTargetChanged;

    public static GhostControl Instance { get; private set; }

    public Mode      State        => state;
    public bool      IsParked     => state == Mode.Parked;
    public Transform CameraTarget { get; private set; }

    // Qual objeto Haze e o desta cena. A cena tem varios e so um vale por vez, entao quem
    // precisa do fantasma pergunta aqui em vez de sair procurando por tipo.
    public Transform Fantasma => ghost;

    Mode           state = Mode.Follow;
    SpriteRenderer ghostSprite;

    void Awake()
    {
        Instance = this;
        if (ghost != null) ghostSprite = ghost.GetComponentInChildren<SpriteRenderer>();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // Apply the starting state so the camera target and follow flag are consistent
    // from frame one (rather than whatever the components happened to be set to).
    void Start() => Apply(Mode.Follow);

    // O Q so existe depois que o tutorial ensinou.
    //
    // Antes disso a Haze e companhia, nao ferramenta: apertar Q e trocar de corpo sem o
    // jogo ter explicado nada seria uma mecanica inteira descoberta por acidente, e o beat
    // que a apresenta perderia o sentido. O tutorial grava o progresso no primeiro passo,
    // entao dentro dele o Q ja vale.
    public bool QLiberado =>
        string.IsNullOrEmpty(requiresProgress) ||
        (SaveManager.Instance != null && SaveManager.Instance.HasProgress(requiresProgress));

    // Chamado pelo tutorial. Grava o progresso, entao sobrevive a save e load.
    public void LiberarQ()
    {
        if (string.IsNullOrEmpty(requiresProgress)) return;
        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("[GhostControl] LiberarQ: nao ha SaveManager na cena — o Q " +
                             "continua travado e o tutorial nao vai andar.", this);
            return;
        }
        SaveManager.Instance.AddProgress(requiresProgress);
    }

    void Update()
    {
        var kb = Keyboard.current;
        // Q travado com dialogo na tela. Trocar de corpo no meio de uma fala move a camera
        // para longe da caixa que o jogador esta lendo, e ele perde a linha sem saber por que.
        // O tutorial e a excecao: ele PEDE o Q enquanto o proprio comando segura o dialogo.
        if (kb != null && kb[toggleKey].wasPressedThisFrame && QLiberado &&
            !TutorialHint.DialogoBloqueandoInput)
            Advance();

        if (state == Mode.Piloting)
        {
            // Reafirmar o congelamento do Josh TODO QUADRO, e nao so na troca de modo.
            //
            // O Apply() escreve InputEnabled uma vez, e depois disso qualquer outro sistema
            // pode reescrever por cima sem saber que a Haze esta sendo pilotada. O caso real:
            // a cerca do puzzle dispara um dialogo quando a Haze bate no limite, e o funil de
            // dialogo destrava o jogador ao fechar a fala — de repente o jogador move os dois
            // ao mesmo tempo, e nada na tela explica por que.
            //
            // Enquanto isto e um estado e nao um evento, reafirmar e mais barato do que
            // cacar quem escreveu por ultimo.
            if (mc != null && mc.InputEnabled) mc.InputEnabled = false;

            // A Haze tambem para com dialogo na tela. Ela nao ter corpo fisico nao muda o
            // problema: o jogador andando enquanto le acaba a fala noutro lugar, e no caso
            // dela ainda por cima com a camera junto.
            if (!TutorialHint.DialogoBloqueandoInput) MoveGhost();
        }
    }

    // Forca um modo, sem passar pelo ciclo do Q.
    //
    // O Q e para o JOGADOR trocar de corpo. Uma cena scriptada precisa da outra coisa:
    // dizer exatamente em qual corpo ela quer o jogador, sem depender de quantas vezes ele
    // apertou a tecla antes. E o que deixa o tutorial devolver a camera ao Josh no instante
    // em que a estatua gira e entregar de volta a Haze quando a conversa acaba.
    public void SetMode(Mode mode) => Apply(mode);

    void Advance()
    {
        Mode next = state switch
        {
            Mode.Follow   => Mode.Piloting,
            Mode.Piloting => Mode.Parked,
            _             => Mode.Follow,
        };
        Apply(next);
    }

    void Apply(Mode mode)
    {
        state = mode;

        // FragmentFollow only runs in FOLLOW. In PILOTING we drive the ghost; in
        // PARKED it stays frozen exactly where it was left.
        if (ghostFollow != null) ghostFollow.enabled = (mode == Mode.Follow);

        // The MC is frozen only while you're piloting the ghost.
        if (mc != null) mc.InputEnabled = (mode != Mode.Piloting);

        // Camera looks at the ghost while piloting, the MC otherwise.
        Transform target = (mode == Mode.Piloting)
            ? ghost
            : (mc != null ? mc.transform : null);

        if (target != CameraTarget)
        {
            CameraTarget = target;
            onCameraTargetChanged?.Invoke(target);
        }
    }

    void MoveGhost()
    {
        if (ghost == null) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.aKey.isPressed) input.x -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (input == Vector2.zero) return;

        input = input.normalized;
        ghost.position += (Vector3)(input * (ghostMoveSpeed * Time.deltaTime));

        if (ghostSprite != null && Mathf.Abs(input.x) > 0.01f)
            ghostSprite.flipX = input.x < 0f;
    }
}

// Serializable so the target transform shows up as a dynamic argument in the
// Inspector's UnityEvent, letting you wire it straight to a camera's Follow setter.
[System.Serializable]
public class TransformEvent : UnityEngine.Events.UnityEvent<Transform> { }
