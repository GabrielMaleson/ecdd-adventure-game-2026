using UnityEngine;

// Owns which way a character is looking, so nothing else has to know HOW that character
// turns around. Two kinds exist in this project and they are not interchangeable:
//
//   Mirrored art  — one side drawn, the other is a horizontal flip. Josh and Erika.
//                   Directions 0..3, and the visual's X scale is negated to face left.
//
//   Two-sided art — both sides drawn by hand, because the character is NOT symmetrical.
//                   Marcus wears a watch on one wrist and his jacket buttons down one
//                   side; flipping him moves both to the wrong side. He gets a state per
//                   side and is never mirrored.
//
// Ticking "Sides Drawn Separately" is the whole difference. Everything that turns a
// character — the follower, a cutscene <<face>>, a formation — calls Set() and gets the
// right behaviour without knowing which kind it is holding.
[DisallowMultipleComponent]
public class CharacterFacing : MonoBehaviour
{
    // The Animator's Direction values. 0..3 are the original four every character has;
    // 4 and 5 only exist on two-sided characters and are simply never requested for the
    // others, so one convention covers both without a second parameter.
    public const int DIR_IDLE      = 0;   // parado, olhando p/ direita
    public const int DIR_DOWN      = 1;
    public const int DIR_UP        = 2;
    public const int DIR_SIDE      = 3;   // andando p/ direita
    public const int DIR_SIDE_LEFT = 4;   // so em arte de dois lados
    public const int DIR_IDLE_LEFT = 5;   // so em arte de dois lados

    [Tooltip("Ligado: o personagem tem os dois lados desenhados (relogio, botoes) e NUNCA " +
             "e espelhado — usa estados proprios. Desligado: espelha, como Josh e Erika.")]
    public bool sidesDrawnSeparately;

    [Tooltip("Vazio = o Animator deste objeto ou dos filhos.")]
    public Animator animator;

    [Tooltip("Objeto espelhado ao virar. Vazio = o do Animator.")]
    public Transform visual;

    private static readonly int DirectionHash = Animator.StringToHash("Direction");

    private Vector3 baseScale;
    private int currentDir = -1;


    // Remembered because standing still has a side too: a two-sided character who stops
    // walking left must keep facing left, and the idle state that shows him doing it is a
    // different state from the one facing right.
    private bool facingLeft;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (visual == null && animator != null) visual = animator.transform;
        if (visual == null) visual = transform;

        baseScale = visual.localScale;

        // Two ways of saying "mirrored" can sit on a character at once — Flip X on the
        // renderer and a negative X scale — and from here on only the scale is allowed to
        // say it. Read what the two of them TOGETHER currently mean, clear the renderer's,
        // and re-express the same look through the scale alone.
        bool mirrored = baseScale.x < 0f;

        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!sprite.flipX) continue;
            sprite.flipX = false;
            mirrored = !mirrored;
        }

        baseScale.x = Mathf.Abs(baseScale.x);

        // Whichever way the character was left facing in the scene is the way he starts.
        // Forcing everyone to face right here is what spun Erika around the instant the
        // game started, throwing away the side she was deliberately placed on.
        facingLeft = mirrored;
        ApplyIdle();
    }

    // The single entry point. Zero means "stopped" and holds the last side.
    public void Set(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            ApplyIdle();
            return;
        }

        // BYTE FOR BYTE the same test PlayerController.FaceDirection uses. It has to be:
        // fed the same direction, this must reach the same answer, or on a diagonal the MC
        // turns sideways while the party turns to face the camera. No hysteresis, no
        // margin, no cleverness — any difference here is a difference on screen.
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
        {
            facingLeft = direction.x < 0f;

            if (sidesDrawnSeparately)
            {
                SetDir(facingLeft ? DIR_SIDE_LEFT : DIR_SIDE);
                visual.localScale = baseScale;   // never mirrored
            }
            else
            {
                SetDir(DIR_SIDE);
                // baseScale.x KEEPS whatever sign it ended up with after the flipX fold —
                // that sign is what "facing right" means for this character. Taking Abs
                // here threw the fold away and sent Erika walking backwards again.
                visual.localScale = new Vector3(
                    facingLeft ? -baseScale.x : baseScale.x,
                    baseScale.y, baseScale.z);
            }
            return;
        }

        SetDir(direction.y < 0f ? DIR_DOWN : DIR_UP);

        // Front and back are single drawings for everyone, so the mirror is dropped here
        // even for a mirrored character — otherwise walking up after walking left would
        // show his back inside out.
        visual.localScale = baseScale;
    }

    public void SetIdle() => Set(Vector2.zero);

    // The standing pose for whichever side the character is currently on.
    private void ApplyIdle()
    {
        if (sidesDrawnSeparately)
        {
            SetDir(facingLeft ? DIR_IDLE_LEFT : DIR_IDLE);
            visual.localScale = baseScale;
            return;
        }

        SetDir(DIR_IDLE);
        visual.localScale = new Vector3(
            facingLeft ? -baseScale.x : baseScale.x,
            baseScale.y, baseScale.z);
    }

    // Copies a Direction that has ALREADY been decided — the MC's, straight off his
    // Animator. Nothing is recomputed here, which is the entire point: re-deriving the
    // direction from a measured position delta compares two floats that are equal on a
    // 45-degree walk, and the comparison lands differently on the last bit every frame.
    // That is the party strobing between side and front for a second whenever the player
    // sets off diagonally. Reading the answer he already reached cannot disagree with him.
    public void Copy(int direction, bool leaderFacingLeft)
    {
        if (direction == DIR_IDLE || direction == DIR_IDLE_LEFT)
        {
            ApplyIdle();
            return;
        }

        if (direction == DIR_SIDE || direction == DIR_SIDE_LEFT)
        {
            facingLeft = leaderFacingLeft;

            if (sidesDrawnSeparately)
            {
                SetDir(facingLeft ? DIR_SIDE_LEFT : DIR_SIDE);
                visual.localScale = baseScale;
            }
            else
            {
                SetDir(DIR_SIDE);
                visual.localScale = new Vector3(
                    facingLeft ? -baseScale.x : baseScale.x,
                    baseScale.y, baseScale.z);
            }
            return;
        }

        SetDir(direction);                 // up / down, single drawing for everyone
        visual.localScale = baseScale;
    }

    private void SetDir(int dir)
    {
        if (dir == currentDir || animator == null) return;
        currentDir = dir;
        animator.SetInteger(DirectionHash, dir);
    }

    // For the callers that only know "look left" / "look right" — a cutscene <<face>>, a
    // formation slot — rather than a movement vector.
    public void FaceHorizontal(bool left)
    {
        Set(left ? Vector2.left : Vector2.right);
        SetIdle();
    }
}
