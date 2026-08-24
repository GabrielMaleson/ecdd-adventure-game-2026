using UnityEngine;

// ONE asset that decides how CLOSE you have to be to interact with anything — a villager,
// a puzzle statue, a story prop, a door. It lives at Assets/Resources/InteractSettings.asset
// and is found by Resources.Load, so nothing has to be dragged into a field anywhere.
//
// Range is not a number any interactable owns: the reach IS the trigger collider, and six
// different scripts each having their own hand-drawn box is how "why is this one harder to
// click than that one" happens. This resizes every interactable trigger in the scene to the
// same box on load, so distance is one setting instead of dozens of drags.
//
// Deleting the asset reverts everything to whatever each collider was authored with.
[CreateAssetMenu(fileName = "InteractSettings", menuName = "ECDD/Interact Settings")]
public class InteractSettings : ScriptableObject
{
    [Tooltip("Tamanho do trigger de TODO interagivel, em unidades. E isto que define a " +
             "distancia: nao existe raio em lugar nenhum, o alcance E a caixa.")]
    public Vector2 triggerSize = new Vector2(2f, 2f);

    [Tooltip("Reaplica o tamanho padrao TODA vez que a cena carrega. Deixe DESLIGADO: " +
             "assim o padrao entra uma vez (quando voce edita este asset) e qualquer " +
             "collider que voce ajustar na mao depois fica como voce deixou.")]
    public bool applyOnLoad;

    [Tooltip("Cria um trigger no interagivel que nao tiver nenhum. Sem trigger ele nunca " +
             "dispara, entao nao ha nada a preservar em deixar assim.")]
    public bool createMissingTrigger = true;

    private const string ResourcePath = "InteractSettings";

    private static InteractSettings cached;
    private static bool lookedUp;

    public static InteractSettings Current
    {
        get
        {
            if (!lookedUp)
            {
                cached = Resources.Load<InteractSettings>(ResourcePath);
                lookedUp = true;
            }
            return cached;
        }
    }

    // Runs once after the scene finishes loading and before any Start, so DialogueStarter
    // is still free to re-centre its own collider on its marker afterwards — this sets the
    // SIZE, that sets the OFFSET, and the two never fight.
    //
    // No scene object and no bootstrap prefab: something that must exist in every scene to
    // work is something that will be missing from one of them.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyToScene()
    {
        InteractSettings settings = Current;
        if (settings == null || !settings.applyOnLoad) return;

        settings.Apply();
    }

#if UNITY_EDITOR
    // Editing the asset pushes the change into the open scene immediately. Without this the
    // colliders keep showing their hand-drawn sizes in the Inspector and only become the
    // standard size once you press Play, which reads as "the setting does nothing".
    private void OnValidate()
    {
        cached = this;
        lookedUp = true;
        // Editing this asset IS the "apply now" action — it writes the standard size into
        // the open scene. Nothing re-applies afterwards, so a collider you tune by hand
        // stays tuned.
        Apply();
    }
#endif

    public void Apply()
    {
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsInteractable(behaviour)) continue;
            if (behaviour.GetComponent<KeepInteractRange>() != null) continue;

            // A DialogueStarter that is NOT click-to-talk is a walk-in cutscene zone, not a
            // reach: home_inside_first is 16.7 x 5.6 laid across a room precisely so the
            // player cannot miss it. Squashing those to 2x2 does not tighten interaction,
            // it makes scenes silently fail to fire.
            if (behaviour is DialogueStarter starter && !starter.IsClickNPC) continue;

            bool found = false;

            foreach (BoxCollider2D box in behaviour.GetComponents<BoxCollider2D>())
            {
                // Only the trigger. The other collider on the same object is usually the
                // solid body you bump into — the well has exactly this — and resizing that
                // would change what the player can walk through.
                if (!box.isTrigger) continue;
                box.size = triggerSize;
                found = true;
            }

            // A non-box trigger (the circle some props use) is left exactly as it is:
            // it works, and forcing a shape change is not this setting's job.
            if (!found && behaviour.GetComponent<Collider2D>() is Collider2D any && any.isTrigger)
                continue;

            if (!found && createMissingTrigger)
            {
                BoxCollider2D added = behaviour.gameObject.AddComponent<BoxCollider2D>();
                added.isTrigger = true;
                added.size = triggerSize;
            }
        }
    }

    // The six components that register with InteractButton. Named rather than marked with
    // an interface because they are pre-existing scripts and an interface would mean
    // editing all six for nothing.
    private static bool IsInteractable(MonoBehaviour behaviour)
    {
        return behaviour is DialogueStarter
            || behaviour is InteractDialogue
            || behaviour is Pickup
            || behaviour is StatueSwitch
            || behaviour is TeleporterScript
            || behaviour is SceneLoadTrigger;
    }
}
