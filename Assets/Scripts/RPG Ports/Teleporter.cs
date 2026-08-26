using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// A portal: walking in either teleports immediately (teleportOnTrigger) or arms an
// InteractButton prompt and waits for the player to press E.
[RequireComponent(typeof(Collider2D))]
public class TeleporterScript : MonoBehaviour
{
    [Header("Teleport Settings")]
    public Transform destination;
    public bool teleportOnTrigger = true;

    [Tooltip("Only used when Teleport On Trigger is off — shown on the Interact Button while in range; press E to teleport.")]
    public string interactLabel = "E";

    [Header("Camera Switch")]
    [Tooltip("Enabled after the transition — the camera for the destination area.")]
    public GameObject Camera;
    [Tooltip("Disabled after the transition — the camera for the area being left.")]
    public GameObject OldCamera;

    [Tooltip("Other objects that move with the player, e.g. the Fragment ghost (it follows with a speed cap and would otherwise lag behind across a teleport).")]
    public List<GameObject> additionalObjects = new List<GameObject>();

    [Header("Screen Transition")]
    public bool useScreenTransition = true;
    public float transitionDelay = 0.5f;

    [Header("Cooldown")]
    public float teleportCooldown = 2f;

    [Header("Effects")]
    public GameObject teleportEffect;
    public float effectDuration = 1f;
    public AudioClip teleportSound;

    private bool isOnCooldown;

    // Set when a teleport DROPS the player inside this portal. It is not a timer: it is
    // "you have to leave before this counts again", cleared by OnTriggerExit2D. A timer
    // expires while the player is still standing inside, and OnTriggerEnter2D only fires on
    // the way IN, so the portal would sit armed but unreachable until he walked out and
    // back — which is exactly how leaving the house broke.
    private bool suppressedUntilExit;
    private float cooldownTimer;
    private bool playerInRange;
    private GameObject player;

    private void Update()
    {
        if (!isOnCooldown)
            return;

        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f)
        {
            isOnCooldown = false;
            RefreshPrompt(); // cooldown just ended — re-arm if the player is still here
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        player = collision.gameObject;
        playerInRange = true;

        if (teleportOnTrigger && !isOnCooldown && !suppressedUntilExit)
            Teleport();
        else
            RefreshPrompt();
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        playerInRange = false;
        suppressedUntilExit = false;   // he left; the portal counts again
        RefreshPrompt();
    }

    // Walking around INSIDE a portal has to keep working after the suppression clears,
    // otherwise a player who arrives inside one and never fully leaves is stuck.
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!teleportOnTrigger || isOnCooldown || suppressedUntilExit) return;
        if (!collision.CompareTag("Player")) return;

        player = collision.gameObject;
        playerInRange = true;
        Teleport();
    }

    private void OnDisable()
    {
        playerInRange = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    private void RefreshPrompt()
    {
        if (teleportOnTrigger)
            return;

        if (playerInRange && !isOnCooldown && !suppressedUntilExit)
            InteractButton.Instance?.SetInteraction(this, interactLabel, Teleport);
        else
            InteractButton.Instance?.ClearInteraction(this);
    }

    // Public so a UnityEvent (a puzzle solve, a dialogue) can trigger a teleport directly.
    public void Teleport()
    {
        if (destination == null)
        {
            Debug.LogError($"{name}: Teleporter has no destination set.", this);
            return;
        }

        if (isOnCooldown)
            return;

        GameObject target = player;

#if UNITY_EDITOR
        // Quem disparou, de onde, e para onde. Um teleporte inesperado e impossivel de
        // diagnosticar de fora do jogo: a unica pergunta que importa e QUAL portal pegou o
        // jogador, e so ele mesmo pode responder. Teleporte e evento raro, entao isto nao
        // enche o Console.
        Vector3 from = target != null ? target.transform.position : transform.position;
        Debug.Log($"[Teleporte] '{name}' disparou. Jogador em {from} -> '{destination.name}' " +
                  $"em {destination.position}. Gatilho deste portal esta em {transform.position}, " +
                  $"modo={(teleportOnTrigger ? "automatico" : "prompt " + interactLabel)}.", this);
#endif

        if (useScreenTransition && ScreenTransition.Instance != null)
            StartCoroutine(TransitionThenTeleport(target));
        else
            ExecuteTeleport(target);
    }

    private IEnumerator TransitionThenTeleport(GameObject target)
    {
        StartCooldown();
        ScreenTransition.Instance.PlayTransition();

        yield return new WaitForSeconds(transitionDelay);

        if (Camera != null && OldCamera != null)
        {
            Camera.SetActive(true);
            OldCamera.SetActive(false);
        }

        ExecuteTeleport(target);
    }

    private void StartCooldown()
    {
        // So o portal AUTOMATICO precisa de cooldown. Ele dispara ao encostar, entao sem uma
        // janela morta ele te devolveria no instante em que voce aterrissa nele.
        //
        // No portal de E o gate ja e o aperto do jogador — o cooldown so criava dois segundos
        // em que o prompt some. Numa escada de ida e volta rapida isso aparece como "as vezes
        // funciona, as vezes nao", porque depende de voce voltar antes ou depois dos 2s.
        if (!teleportOnTrigger) return;

        isOnCooldown = true;
        cooldownTimer = teleportCooldown;
    }

    private void ExecuteTeleport(GameObject target)
    {
        if (!isOnCooldown)
            StartCooldown();

        if (teleportEffect != null)
            Destroy(Instantiate(teleportEffect, transform.position, Quaternion.identity), effectDuration);

        if (teleportSound != null)
            AudioSource.PlayClipAtPoint(teleportSound, transform.position);

        // Measured BEFORE the move: reading it afterwards asks a collider that the physics
        // world has not caught up with yet, which answers with the position the player just
        // left — the reason the first attempt at this fix silently did nothing.
        Collider2D body = target != null ? target.GetComponent<Collider2D>() : null;
        Vector3 bodyOffset = Vector3.zero;
        Vector3 bodySize = Vector3.zero;
        if (body != null)
        {
            bodyOffset = body.bounds.center - target.transform.position;
            bodySize = body.bounds.size;
        }

        if (target != null)
            target.transform.position = destination.position;
        else
            Debug.LogWarning($"{name}: no player reference available for teleport.", this);

        // Pushes the move into the physics world now instead of at the next FixedUpdate, so
        // the trigger the player landed in is found this frame — before it can fire.
        Physics2D.SyncTransforms();

        foreach (var obj in additionalObjects)
        {
            if (obj != null)
                obj.transform.position = destination.position;
        }

        // Quem SEGUE o viajante viaja com ele, sem precisar ser arrastado em lista nenhuma.
        //
        // A lista acima e manual e por teleporte: serve para um extra especifico, e falha
        // exatamente onde mais importa — a Haze e ligada e desligada ao longo do jogo, e
        // cada porta nova nasceria esquecendo dela. Aqui a regra e uma so: se o seu lider e
        // quem acabou de atravessar, voce atravessou junto.
        if (target != null)
            BringFollowers(target.transform, destination.position);

        if (teleportEffect != null)
            Destroy(Instantiate(teleportEffect, destination.position, Quaternion.identity), effectDuration);

        // Landing inside ANOTHER portal's trigger must not bounce the player straight back
        // out. The exit inside Josh's house sits ~0.06 units below where the front door
        // drops you, so walking in put the player on top of the exit and teleported him
        // outside again — while the "you're inside" dialogue, fired by the same arrival,
        // carried on playing over the outdoor scene. Putting whatever you land in on
        // cooldown means a portal only ever fires when you actually walk INTO it.
        ArmArrivalCooldown(destination.position, bodyOffset, bodySize);

        playerInRange = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    // Fantasma e companheiros, os dois sistemas de seguir que existem no projeto.
    private static void BringFollowers(Transform leader, Vector3 arrival)
    {
        foreach (FragmentFollow ghost in FindObjectsByType<FragmentFollow>(FindObjectsSortMode.None))
        {
            if (ghost == null || ghost.player != leader) continue;

            // AttachTo com snap poe a Haze ao lado dele ja no offset dela e zera a suavizacao.
            // Sem zerar, ela tentaria "caminhar" a distancia inteira do teleporte, limitada
            // pelo maxFollowSpeed — atravessando o mapa a pe depois de cada porta.
            ghost.AttachTo(leader, snap: true);
        }

        foreach (NpcFollow companion in FindObjectsByType<NpcFollow>(FindObjectsSortMode.None))
        {
            if (companion == null || !companion.enabled || companion.target != leader) continue;

            Rigidbody2D rb = companion.GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = arrival;
            companion.transform.position = arrival;

            // A trilha guardada e do outro lado da porta. Andar por ela faria o companheiro
            // refazer um caminho que nao comeca mais onde ele esta.
            companion.Reseed();
        }
    }

    private static void ArmArrivalCooldown(Vector3 arrival, Vector3 bodyOffset, Vector3 bodySize)
    {
        // Where the traveller's BODY ends up, not where its pivot does. A character's
        // collider is a small box at his feet, well below the transform, so a pivot that
        // clears a trigger by a hair still lands the feet inside it.
        bool hasBody = bodySize.sqrMagnitude > 0f;
        Bounds landed = new Bounds(arrival + bodyOffset, bodySize);

        foreach (TeleporterScript other in FindObjectsByType<TeleporterScript>(FindObjectsSortMode.None))
        {
            if (other == null) continue;

            // Portal de E nao precisa ser suprimido: ele nao dispara sozinho, so responde a
            // um aperto deliberado. Suprimir so escondia o prompt de quem acabou de chegar —
            // era preciso sair do gatilho e voltar para poder subir ou descer de novo.
            //
            // A supressao existe para o portal AUTOMATICO, que dispara ao encostar e por isso
            // devolveria o jogador na hora em que ele aterrissa dentro dele. So esse caso.
            if (!other.teleportOnTrigger) continue;

            foreach (Collider2D trigger in other.GetComponents<Collider2D>())
            {
                if (!trigger.isTrigger) continue;

                // The point covers a destination marker with no body to measure; the bounds
                // check is what catches a player whose feet clip the corner of a trigger his
                // pivot is clear of, which is exactly the 6-centimetre overlap here.
                bool landedInside = trigger.OverlapPoint(arrival)
                                 || (hasBody && trigger.bounds.Intersects(landed));

                if (!landedInside) continue;

                other.suppressedUntilExit = true;
                other.playerInRange = false;
                break;
            }
        }
    }

    // A AREA, e nao um ponto.
    //
    // Antes isto desenhava duas bolinhas de raio fixo 0.5 — um numero inventado que nao tem
    // relacao nenhuma com o collider de verdade. Posicionar um teleporte assim e adivinhar:
    // a bolinha diz uma coisa e a caixa que realmente dispara diz outra. Agora o gizmo mostra
    // o collider EXATO, sempre visivel, e a distancia ate a marca de chegada.
    private void OnDrawGizmos() => DrawGizmo(false);
    private void OnDrawGizmosSelected() => DrawGizmo(true);

    private void DrawGizmo(bool selected)
    {
        Collider2D col = GetComponent<Collider2D>();

        // Verde = pisou e vai. Amarelo = precisa apertar E. A cor diz o modo sem abrir o
        // Inspector, que e o que se quer ao olhar seis teleportes na cena de uma vez.
        Color c = teleportOnTrigger ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.85f, 0.2f);
        Gizmos.color = selected ? c : new Color(c.r, c.g, c.b, 0.35f);

        if (col != null)
        {
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

            if (selected)
            {
                Gizmos.color = new Color(c.r, c.g, c.b, 0.15f);
                Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            }
        }
        else
        {
            // Sem collider ele NUNCA dispara. Vermelho para isso saltar aos olhos na cena em
            // vez de virar meia hora de teste.
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }

        if (destination == null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.25f);
            return;
        }

        if (!selected) return;

        // A linha ate a chegada, e a marca dela. Se a marca cair DENTRO da caixa de outro
        // teleporte, e dali que vem o "me teleporta sozinho sem parar" — ver a linha e as
        // duas caixas juntas na cena e o que torna isso obvio.
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, destination.position);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(destination.position, 0.35f);
        Gizmos.DrawLine(destination.position + Vector3.left * 0.5f, destination.position + Vector3.right * 0.5f);
        Gizmos.DrawLine(destination.position + Vector3.down * 0.5f, destination.position + Vector3.up * 0.5f);
    }
}

