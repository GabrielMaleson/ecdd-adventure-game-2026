using UnityEngine;

public class FragmentFollow : MonoBehaviour
{
    [Tooltip("Quem seguir. Vazio = acha sozinho quem tem a tag Player.")]
    public Transform player;

    [Tooltip("Quanto tempo ela leva para alcancar a posicao desejada. MENOR = cola mais no " +
             "Josh. 0.25 e visivelmente atrasado; 0.10-0.12 acompanha de perto.")]
    public float smoothTime = 0.12f;

    [Tooltip("Onde ela fica em relacao ao Josh. O X e espelhado conforme o lado para onde " +
             "ele anda, entao ela nunca fica na frente. |X| e a DISTANCIA: 3.57 e longe, " +
             "1.2-1.5 e ao lado dele.")]
    public Vector2 offset = new Vector2(-1.3f, 0.5f);

    [Tooltip("Cap on how fast the ghost moves while following (world units/sec). Stops it from zipping/teleporting back when it re-attaches from far away (e.g. after being parked at a statue) — it walks back instead. Keep it a bit above the MC's speed so normal following never lags. Set very high to disable the cap.")]
    [SerializeField] float     maxFollowSpeed = 4f;

    SpriteRenderer spriteRenderer;
    Vector2        lastPlayerPos;
    Vector2        activeOffset;
    Vector2        desiredOffset;
    Vector2        positionVelocity;
    Vector2        offsetVelocity;

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        if (player == null) FindPlayer();
        if (player == null) return;
        lastPlayerPos      = player.position;
        activeOffset       = offset;
        desiredOffset      = offset;
        // Deliberately does NOT snap to the player. Snapping here made the ghost pop
        // into existence beside the MC the instant it was switched on, which reads as a
        // teleport at the end of a cutscene where a stand-in was standing somewhere
        // else. Left where it is, LateUpdate's SmoothDamp walks it in under
        // maxFollowSpeed — the same "walks back instead of zipping" the cap exists for.
        // Pair it with <<placeat TrueHaze Stand-in>> so it starts from the stand-in's spot.
        // Ensure the visual child has no stale local position offset.
        spriteRenderer.transform.localPosition = Vector3.zero;
    }

    // Re-enabling after the ghost was piloted or parked: it may be far from the
    // player. Reset the smoothing state so it eases back cleanly instead of
    // lurching from a stale velocity.
    // Liga o follow apontando para o lider, opcionalmente colando nele na hora.
    //
    // Existe porque o campo `player` vem VAZIO no prefab: a Haze e ligada no meio do jogo,
    // e ate agora dependia de alguem arrastar o Josh no Inspector de cada instancia. Sem
    // isso ela acorda e nao segue ninguem, sem dizer nada.
    public void AttachTo(Transform leader, bool snap = false)
    {
        if (leader != null) player = leader;
        if (player == null) FindPlayer();
        if (player == null) return;

        if (snap)
        {
            // Colar so quando quem chamou pediu — um pulo de teste quer a Haze ao lado do
            // Josh IMEDIATAMENTE. No jogo normal ela caminha ate ele, que e o comportamento
            // que o maxFollowSpeed existe para preservar.
            transform.position = (Vector2)player.position + offset;
        }

        lastPlayerPos    = player.position;
        activeOffset     = offset;
        desiredOffset    = offset;
        positionVelocity = Vector2.zero;
        offsetVelocity   = Vector2.zero;
        enabled          = true;
    }

    private void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    void OnEnable()
    {
        if (player == null) FindPlayer();
        if (player != null) lastPlayerPos = player.position;
        positionVelocity = Vector2.zero;
        offsetVelocity   = Vector2.zero;
    }

    void LateUpdate()
    {
        if (player == null) return;

        Vector2 playerPos = player.position;
        float   deltaX    = playerPos.x - lastPlayerPos.x;
        float   deltaY    = playerPos.y - lastPlayerPos.y;
        lastPlayerPos = playerPos;

        if (deltaX > 0.001f)
            desiredOffset.x = -Mathf.Abs(offset.x);
        else if (deltaX < -0.001f)
            desiredOffset.x =  Mathf.Abs(offset.x);

        if (deltaY > 0.001f)
            desiredOffset.y = -Mathf.Abs(offset.y);
        else if (deltaY < -0.001f)
            desiredOffset.y =  Mathf.Abs(offset.y);

        activeOffset = Vector2.SmoothDamp(activeOffset, desiredOffset, ref offsetVelocity, smoothTime * 0.25f);

        transform.position = Vector2.SmoothDamp(
            transform.position,
            (Vector2)playerPos + activeOffset,
            ref positionVelocity,
            smoothTime,
            maxFollowSpeed
        );

        spriteRenderer.flipX = (playerPos.x - transform.position.x) < 0;
    }
}
