using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

// ============================================================================
// DESLIGADO no momento. O componente esta nos prefabs do Marcus e da Erika com a
// caixa DESMARCADA — nada roda ate alguem marcar. Para religar: marque NpcFollow
// nos dois prefabs (ou chame enabled = true), e confira que Target aponta para o
// personagem da FRENTE: Josh -> Marcus -> Erika.
//
// O CharacterFacing ao lado dele fica LIGADO de proposito: e ele que faz o <<face>>
// das cutscenes e o NpcFormation virarem o personagem para o lado certo. Desligar
// os dois quebraria as cutscenes que ja funcionam.
// ============================================================================
//
// COMO FUNCIONA, em uma frase: o seguidor nao persegue ninguem — ele E um ponto da
// trilha que o lider deixou, e toca a animacao que o lider tocou NAQUELE pedaco de
// chao. Tudo o mais decorre disso.
//
// As quatro regras, e o que cada uma conserta:
//
//   1. TRILHA, nao perseguicao. O seguidor anda por onde o lider andou, a uma
//      distancia medida AO LONGO do caminho. Ninguem precisa desviar de nada: a
//      rota e comprovadamente andavel, o lider acabou de andar por ela. Perseguir
//      com inercia fazia o seguidor passar do ponto e voltar — andava em circulos.
//
//   2. UM PASSO SO. Cada um avanca EXATAMENTE o que o lider avancou naquele frame.
//      Nao existe "velocidade propria" nem correcao de atraso: correcao e outra
//      velocidade, e velocidades diferentes e o que desalinha a fila. O espacamento
//      do inicio (Follow Distance, igual para todos) e o espacamento para sempre —
//      por isso cada um e posicionado na marca certa no Start.
//
//   3. O MC E O RELOGIO. Se o Animator dele diz parado, ninguem anda naquele frame.
//      Sem limiar de velocidade para errar, sem terminar o passo comecado. Todos
//      param juntos, e o idle fica sincronizado.
//
//   4. A ANIMACAO E GRAVADA COM A PEGADA. Cada ponto da trilha guarda o Direction
//      que o personagem da frente tinha ALI. O seguidor toca aquilo quando chega
//      naquele ponto. Duas armadilhas que isso evita:
//        - recalcular a direcao pela posicao medida compara dois floats que EMPATAM
//          numa diagonal, e o desempate muda a cada frame: a animacao pisca;
//        - copiar o que o lider faz AGORA faz a fila virar a esquina antes de
//          chegar nela.
//      Cada elo le o elo IMEDIATAMENTE a frente (Erika le Marcus, nao Josh), senao
//      ela herda o atraso dele em vez de somar o proprio e vira adiantada.
//
// Os dois modos (Mode): Trail e a fila indiana descrita acima. Offset mantem uma
// posicao ao lado/atras do lider — parece gente andando junto, mas em porta ele
// empurra parede, porque quer um ponto que pode nao existir.
//
// Turning is delegated to CharacterFacing, which is what knows whether this character is
// mirrored (Josh, Erika) or has both sides drawn by hand (Marcus, whose watch and jacket
// buttons make a flip wrong). One is added automatically if the character has none.
//
// Chain them by pointing each follower's Target at the one in FRONT of it (Josh -> Marcus
// -> Erika) rather than all at Josh.
[DisallowMultipleComponent]
public class NpcFollow : MonoBehaviour
{
    public enum Mode
    {
        Trail,   // anda exatamente por onde o lider andou
        Offset   // mantem uma posicao ao lado/atras do lider
    }

    [Header("Identity")]
    [Tooltip("Nome usado no .yarn: <<follow EsteNome>> / <<unfollow EsteNome>>. " +
             "Vazio = o nome do objeto.")]
    public string followerName;

    [Header("Who to follow")]
    [Tooltip("Quem seguir. Vazio = acha quem tem a tag Player. Aponte para o personagem da " +
             "FRENTE (Josh -> Haze -> Marcus -> Erika) para formar fila.")]
    public Transform target;

    [Header("How")]
    public Mode mode = Mode.Trail;

    [Tooltip("Trail: quantas unidades atras do lider, medidas AO LONGO do caminho dele. " +
             "De valores diferentes a cada seguidor para eles nao se empilharem.")]
    public float followDistance = 1.6f;

    [Tooltip("Offset: posicao desejada em relacao ao lider. X e espelhado conforme o lado " +
             "para onde ele anda, entao o seguidor nunca fica na frente dele.")]
    public Vector2 offset = new Vector2(-1f, -0.6f);

    [Tooltip("Folga de chegada. Precisa ser MENOR que o passo da trilha (0.02), senao " +
             "todo passo ja conta como chegada e a animacao pisca entre andar e parar.")]
    public float arriveThreshold = 0.02f;

    [Header("Animation")]
    [Tooltip("Vazio = usa o Animator deste objeto ou dos filhos.")]
    public Animator animator;

    [Tooltip("Objeto virado ao mudar de lado. Vazio = o do Animator.")]
    public Transform visual;

    private Rigidbody2D body;
    private CharacterFacing facing;

    // Animation follows the step actually taken, not "am I near the goal": the goal moves
    // in small hops as the leader walks, so distance-to-goal flickers across any threshold
    // several times a second. The grace keeps a momentary stall from blinking to idle.
    // Below this the leader counts as standing still. Low enough that a slow walk still
    // reads as walking, high enough that physics jitter on a stopped body does not.
    private const float LeaderMovingSpeed = 0.15f;

    // The leader's recent footsteps, newest last, with how far along the path each one is.
    // Only long enough to serve this follower's distance — it is trimmed every step, so it
    // does not grow with playtime.
    private readonly List<Vector2> path = new List<Vector2>();
    private readonly List<float> pathLength = new List<float>();

    // The MC's Animator state AT each footstep, stored with it. The follower plays back
    // what he played THERE — not what he is playing now, and not something recomputed from
    // positions. Recomputing compares two floats that tie on a diagonal and flickers;
    // copying his current state makes the party turn a corner before they reach it.
    private readonly List<int> pathDir = new List<int>();
    private readonly List<bool> pathLeft = new List<bool>();

    // Every follower registers, so <<follow Marcus>> is a lookup by name and no cutscene
    // needs a reference to a specific object — the same shape Cutscener and NpcFormation
    // already use for <<enable>> and <<formation>>.
    private static readonly List<NpcFollow> allInstances = new List<NpcFollow>();

    private string Name => string.IsNullOrEmpty(followerName) ? name : followerName;

    // Yarn: <<follow Marcus>> — ele volta a seguir de onde estiver.
    [YarnCommand("follow")]
    public static void StartFollowing(string who) => SetFollowing(who, true);

    // Yarn: <<unfollow Marcus>> — ele para e fica onde esta, ate mandarem de novo.
    [YarnCommand("unfollow")]
    public static void StopFollowing(string who) => SetFollowing(who, false);

    private static void SetFollowing(string who, bool on)
    {
        int matches = 0;

        foreach (NpcFollow f in allInstances)
        {
            if (!f.Name.Equals(who, System.StringComparison.OrdinalIgnoreCase)) continue;

            matches++;

            // Re-seeded on the way back in, not on the way out: while it was off the leader
            // walked somewhere else entirely, and a stale trail would send the character
            // retracing a route that no longer starts where he is standing.
            if (on && !f.enabled) f.Reseed();

            f.enabled = on;
        }

        if (matches == 0)
            Debug.LogWarning($"<<follow>>: no NpcFollow named '{who}' in the scene.");
    }

    private void Awake()
    {
        allInstances.Add(this);
        body = GetComponent<Rigidbody2D>();

        // Added rather than required, so dropping this on a character is still one step.
        facing = GetComponent<CharacterFacing>();
        if (facing == null) facing = gameObject.AddComponent<CharacterFacing>();

        if (animator != null) facing.animator = animator;
        if (visual != null) facing.visual = visual;
    }

    private void ResolveTargetVisual()
    {
        if (target == null) return;
        targetAnimator = target.GetComponentInChildren<Animator>();
        targetVisual = targetAnimator != null ? targetAnimator.transform : target;
    }

    private void OnDestroy()
    {
        allInstances.Remove(this);
    }

    // Throws the old trail away and starts a fresh one from where these two are standing
    // right now. Called when following resumes, and by Start for the first time.
    public void Reseed()
    {
        path.Clear(); pathLength.Clear(); pathDir.Clear(); pathLeft.Clear();
        headPrevValid = false;
        leaderSpeed = 0f;

        if (target == null) return;

        path.Add(transform.position);
        pathLength.Add(0f);
        pathDir.Add(0);
        pathLeft.Add(false);

        path.Add(target.position);
        pathLength.Add(Vector2.Distance(transform.position, target.position));
        pathDir.Add(0);
        pathLeft.Add(false);
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        ResolveTargetVisual();

        // Placed at its slot NOW, on the line it already sits on. Everyone advances by the
        // leader's exact step from here on, so whatever spacing exists at this moment is
        // the spacing for the rest of the game — it has to be right before the first step,
        // because nothing corrects it later. That is the price of moving at one speed, and
        // it is the point.
        if (target != null)
        {
            Vector2 fromLeader = (Vector2)transform.position - (Vector2)target.position;
            if (fromLeader.sqrMagnitude < 0.0001f) fromLeader = Vector2.down;
            transform.position = (Vector2)target.position + fromLeader.normalized * followDistance;

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = transform.position;
        }

        // Seeded so the first frames have a real path to walk instead of the leader's
        // single starting point, which would make everyone converge on one spot.
        if (target != null)
        {
            Reseed();
        }

        // A follower walks INTO whoever it is chasing by design — that is what following
        // is. With both bodies Dynamic and the same tiny mass, the contact shoved the
        // player around, which reads as the character walking off on his own. The follower
        // still collides with the world; it just cannot push the people it is following.
        IgnoreCollisionWith(target);
        IgnoreCollisionWith(GameObject.FindGameObjectWithTag("Player")?.transform);

        foreach (NpcFollow other in FindObjectsByType<NpcFollow>(FindObjectsSortMode.None))
            if (other != this) IgnoreCollisionWith(other.transform);

        facing.SetIdle();
    }

    // The follower IS a point on the leader's path, not something chasing him. That is the
    // whole design, and it is what makes every symptom go away at once:
    //
    //   * it advances only when the leader advances, so they start and stop together;
    //   * MoveTowards cannot overshoot, so there is nothing to oscillate around — the
    //     circling was a SmoothDamp chase orbiting a goal it kept flying past;
    //   * its direction is the path's own direction, which changes only when the leader
    //     actually turned, so the animation cannot flicker between side and front.
    private void FixedUpdate()
    {
        if (target == null) return;

        MeasureLeaderSpeed();
        RecordLeaderStep();

        Vector2 here = body != null ? body.position : (Vector2)transform.position;
        int dirHere; bool leftHere;
        Vector2 goal = mode == Mode.Trail
            ? TrailGoal(here, out dirHere, out leftHere)
            : OffsetGoal(out dirHere, out leftHere);

        Vector2 toGoal = goal - here;
        float distance = toGoal.magnitude;

        // THE leader is the clock. He is not moving this frame, so nobody is: no closing of
        // gaps, no easing in, no finishing a step. Anything else means the party is still
        // walking after he stopped, which is what makes the idle look out of sync.
        // His Animator says it outright, so there is no threshold to be on the wrong side
        // of: idle is idle in the same frame for everyone.
        int headDirection = headAnimator != null ? headAnimator.GetInteger(DirectionHash) : -1;
        bool leaderMoving = headDirection > 0 || (headAnimator == null && leaderSpeed > LeaderMovingSpeed);

        // EXACTLY the leader's step. Not "his speed plus a correction" — a correction is a
        // different speed, and a follower moving at a different speed from the man in front
        // is the whole complaint. Everyone in the line advances the same distance in the
        // same frame, so the spacing set at the start is the spacing forever and all three
        // walk cycles stay in step.
        float allowance = leaderMoving ? leaderSpeed * Time.fixedDeltaTime : 0f;

        Vector2 step = allowance > 0f && distance > arriveThreshold
            ? Vector2.MoveTowards(here, goal, allowance)
            : here;

        if (step != here)
        {
            if (body != null) body.MovePosition(step);
            else transform.position = step;
        }

        // What the MC was doing WHERE THIS CHARACTER IS, not where he is now. He turns the
        // corner first; they turn it when they get there.
        Animate(dirHere, leftHere, leaderMoving);
    }

    // A follower walks INTO whoever it is chasing by design — that is what following is.
    // With both bodies Dynamic and the same tiny mass, the contact shoved the player around,
    // which reads as the character walking off on his own. Walls still stop them.
    private void IgnoreCollisionWith(Transform other)
    {
        if (other == null) return;

        foreach (Collider2D mine in GetComponentsInChildren<Collider2D>())
        {
            if (mine.isTrigger) continue;   // triggers are how things get NOTICED; leave them

            foreach (Collider2D theirs in other.GetComponentsInChildren<Collider2D>())
            {
                if (theirs.isTrigger || theirs == mine) continue;
                Physics2D.IgnoreCollision(mine, theirs, true);
            }
        }
    }

    private void Animate(int dirHere, bool leftHere, bool leaderMoving)
    {
        if (!leaderMoving || dirHere <= 0)
        {
            facing.SetIdle();
            return;
        }

        facing.Copy(dirHere, leftHere);
    }

    // ------------------------------------------------------------------ trail

    // Measured from the HEAD of the line, never from the character directly in front. In a
    // chain, whoever is in front is himself a follower: he stops a frame late, so the next
    // one stops a frame later still, and the third later again — the staggered stop.
    // Everyone reading the same transform gets the same number in the same frame.
    //
    // Raw, not smoothed. Smoothing decays over several frames, so "stopped" arrived late,
    // and that lateness compounded down the line.
    private Transform head;
    private float leaderSpeed;
    private Vector2 headPrevPos;
    private bool headPrevValid;

    // Read straight off the MC instead of being re-derived: his Animator already holds the
    // decision, and his visual's X scale already holds which way he is mirrored.
    private Animator headAnimator;
    private Transform headVisual;

    // The character DIRECTLY in front, which for the second follower is the first follower
    // and not the MC. Erika stored the MC's live direction against Marcus's footsteps, so
    // she inherited his delay instead of adding her own and turned corners with him. Each
    // link has to read the link ahead of it: Marcus plays the MC delayed once, Erika plays
    // Marcus delayed again.
    private Animator targetAnimator;
    private Transform targetVisual;

    private static readonly int DirectionHash = Animator.StringToHash("Direction");

    private Transform ResolveHead()
    {
        Transform current = target;

        // Guarded rather than while(true): a target loop would otherwise hang the editor.
        for (int i = 0; i < 16 && current != null; i++)
        {
            NpcFollow ahead = current.GetComponent<NpcFollow>();
            if (ahead == null || ahead.target == null) return current;
            current = ahead.target;
        }

        return target;
    }

    // A mirrored character says "left" with a negative scale; a two-sided one says it with
    // a dedicated Direction value and never mirrors at all. Asking only about the scale
    // meant Marcus always reported "facing right", so Erika never turned left behind him.
    private static bool FacingLeft(int dir, Transform visual)
    {
        if (dir == CharacterFacing.DIR_SIDE_LEFT || dir == CharacterFacing.DIR_IDLE_LEFT)
            return true;

        return visual != null && visual.localScale.x < 0f;
    }

    private void MeasureLeaderSpeed()
    {
        if (head == null)
        {
            head = ResolveHead();
            if (head != null)
            {
                headAnimator = head.GetComponentInChildren<Animator>();

                // The object PlayerController scales. Its own transform never flips, so
                // reading the sign there would always say "facing right".
                headVisual = headAnimator != null ? headAnimator.transform : head;
            }
        }

        if (head == null) { leaderSpeed = 0f; return; }

        Vector2 now = head.position;

        if (!headPrevValid)
        {
            headPrevPos = now;
            headPrevValid = true;
            return;
        }

        leaderSpeed = Vector2.Distance(now, headPrevPos) / Time.fixedDeltaTime;
        headPrevPos = now;
    }

    private void RecordLeaderStep()
    {
        Vector2 leader = target.position;

        int dir = targetAnimator != null ? targetAnimator.GetInteger(DirectionHash) : 0;
        bool left = FacingLeft(dir, targetVisual);

        if (path.Count == 0)
        {
            path.Add(leader);
            pathLength.Add(0f);
            pathDir.Add(dir);
            pathLeft.Add(left);
            return;
        }

        Vector2 last = path[path.Count - 1];
        float moved = Vector2.Distance(last, leader);

        // A minimum step keeps the trail from filling with thousands of near-identical
        // points while the leader stands still breathing against a wall.
        if (moved < 0.02f) return;

        path.Add(leader);
        pathLength.Add(pathLength[pathLength.Count - 1] + moved);
        pathDir.Add(dir);
        pathLeft.Add(left);

        // Drop everything older than this follower needs. Twice the distance is slack for
        // the catch-up case, where it is temporarily further back than it wants to be.
        float keepFrom = pathLength[pathLength.Count - 1] - followDistance * 2f;
        int drop = 0;
        while (drop + 1 < pathLength.Count && pathLength[drop + 1] < keepFrom) drop++;
        if (drop > 0)
        {
            path.RemoveRange(0, drop);
            pathLength.RemoveRange(0, drop);
            pathDir.RemoveRange(0, drop);
            pathLeft.RemoveRange(0, drop);
        }
    }

    // The point on the leader's recorded path that is followDistance behind him, measured
    // along the path rather than as the crow flies — so a follower rounding a corner cuts
    // the corner exactly as the leader did instead of walking through the wall.
    // Also reports the direction of the path segment it landed on. That is what the
    // follower should be FACING: the way the leader was walking when he laid this piece of
    // ground. Facing was taken from the frame's leftover movement instead, and once the
    // follower is sitting on its mark that leftover is a few thousandths of a unit of
    // numerical residue — whose direction flips between horizontal and vertical at random,
    // which is Erika snapping between her side and front animations for no reason.
    private Vector2 TrailGoal(Vector2 here, out int dirHere, out bool leftHere)
    {
        dirHere = 0; leftHere = false;
        if (path.Count == 0) return here;

        float head = pathLength[pathLength.Count - 1];
        float wantAt = head - followDistance;

        if (wantAt <= pathLength[0])
        {
            dirHere = pathDir[0]; leftHere = pathLeft[0];
            return path[0];
        }

        for (int i = pathLength.Count - 1; i > 0; i--)
        {
            if (pathLength[i - 1] > wantAt) continue;

            dirHere = pathDir[i]; leftHere = pathLeft[i];

            float span = pathLength[i] - pathLength[i - 1];
            float t = span > 0.0001f ? (wantAt - pathLength[i - 1]) / span : 0f;
            return Vector2.Lerp(path[i - 1], path[i], t);
        }

        dirHere = pathDir[path.Count - 1]; leftHere = pathLeft[path.Count - 1];
        return path[path.Count - 1];
    }

    // ----------------------------------------------------------------- offset

    // Mirrored by the leader's travel direction, so the follower stays behind him whichever
    // way he turns instead of ending up in front and being walked through.
    private Vector2 lastLeaderPos;
    private Vector2 leaderFacing = Vector2.down;

    private Vector2 OffsetGoal(out int dirHere, out bool leftHere)
    {
        dirHere = targetAnimator != null ? targetAnimator.GetInteger(DirectionHash) : 0;
        leftHere = FacingLeft(dirHere, targetVisual);

        Vector2 leader = target.position;
        Vector2 moved = leader - lastLeaderPos;
        lastLeaderPos = leader;

        if (moved.sqrMagnitude > 0.000001f)
            leaderFacing = moved.normalized;

        Vector2 back = -leaderFacing;
        Vector2 side = new Vector2(-leaderFacing.y, leaderFacing.x);

        return leader + back * Mathf.Abs(offset.y) + side * offset.x;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        for (int i = 1; i < path.Count; i++)
            Gizmos.DrawLine(path[i - 1], path[i]);

        if (target != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}
