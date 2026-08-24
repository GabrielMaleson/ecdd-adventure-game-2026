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
    [Tooltip("Prompt de interacao (o E). Esta num Canvas, entao o numero e em PIXELS.")]
    public float interactPromptSize = 60f;

    [Tooltip("Todo texto flutuante no mundo: falas de personagem (barks) E texto de " +
             "objeto examinado. Um numero so, porque os dois tem que ser iguais. " +
             "World-space, em UNIDADES — nao em pixels como o prompt.")]
    public float worldTextSize = 6f;

    [Header("Posicao — acima do TOPO DA ARTE, em unidades")]
    [Tooltip("Altura do texto FALADO (barks e examine).")]
    public float worldTextHeight = 0.35f;

    [Tooltip("Altura do PROMPT (o E). Separada porque o E costuma querer ficar mais alto " +
             "que a fala, ou o contrario.")]
    public float promptHeight = 0.35f;

    [Header("Tempo e animacao — 0 = nao mexe")]
    [Tooltip("Quanto tempo a fala fica parada antes de subir e sumir.")]
    public float holdDuration = 1.5f;

    [Tooltip("Quanto ela sobe enquanto some, em unidades.")]
    public float floatDistance = 0.6f;

    [Tooltip("Quanto tempo dura a subida + fade out.")]
    public float floatFadeDuration = 1f;

    [Tooltip("Fade de entrada. So o texto de examinar objeto usa.")]
    public float fadeInDuration = 1f;

    [Tooltip("Quanto ela sobe durante o fade de entrada. So o examine usa.")]
    public float fadeInFloatDistance = 0.6f;

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
            text.fontSize = size;
            // Auto-sizing silently overrides fontSize, so a size set here would look
            // like it did nothing at all on any object that happens to have it on.
            text.enableAutoSizing = false;
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
        if (heightAbove < 0f)
            heightAbove = Current != null ? Current.worldTextHeight : 0.35f;

        var go = new GameObject(name);
        go.transform.SetParent(owner, false);

        // World scale 1 whatever the owner is scaled to. Characters sit at scale 4, so a
        // label inheriting that would render four times too big and float four times too
        // far up as it fades.
        Vector3 s = owner.lossyScale;
        go.transform.localScale = new Vector3(
            s.x != 0f ? 1f / s.x : 1f,
            s.y != 0f ? 1f / s.y : 1f,
            s.z != 0f ? 1f / s.z : 1f);

        var text = go.AddComponent<TMPro.TextMeshPro>();
        text.text = string.Empty;
        text.alignment = TMPro.TextAlignmentOptions.Bottom;
        text.rectTransform.sizeDelta = new Vector2(20f, 5f);

        Apply(text, Role.WorldText);

        // Above the ART, not above the transform — see VisibleArt for why those are very
        // different numbers on a character sheet.
        // Falling through to the owner's own position matters: an unmeasurable object
        // would otherwise take bounds' zeroed default and put its label on the world
        // origin, nowhere near the thing it belongs to.
        Vector3 spot = VisibleArt.TryGetBounds(owner, out Bounds bounds)
            ? new Vector3(bounds.center.x, bounds.max.y, 0f)
            : new Vector3(owner.position.x, owner.position.y, 0f);

        go.transform.position = new Vector3(spot.x, spot.y + heightAbove, owner.position.z);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null && SortingLayer.NameToID("Dialogue") != 0)
        {
            renderer.sortingLayerName = "Dialogue";
            renderer.sortingOrder = 5;
        }

        return text;
    }

    // Puts an EXISTING floating label at the standard height above the owner's artwork.
    public static void PlaceWorldLabel(Transform owner, TMPro.TextMeshPro text)
    {
        if (owner == null || text == null) return;

        TextStyle style = Current;
        if (style == null) return;

        Vector3 spot = VisibleArt.TryGetBounds(owner, out Bounds bounds)
            ? new Vector3(bounds.center.x, bounds.max.y, 0f)
            : new Vector3(owner.position.x, owner.position.y, 0f);

        text.transform.position = new Vector3(spot.x, spot.y + style.worldTextHeight, text.transform.position.z);
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

        foreach (InteractDialogue examine in FindObjectsByType<InteractDialogue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            examine.ApplyTextStyle();
    }
#endif
}
