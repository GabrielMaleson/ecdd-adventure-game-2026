using UnityEngine;

// The design's core "mist gradually thickens from mid-game onward" mechanic.
// Persistent across scenes (like SaveManager/DialogueManager), driven explicitly
// from the story via <<mist N>> in .yarn (see ImageScript's DialogueManager.Mist) —
// not inferred from arbitrary progress flags, so pacing stays entirely in the
// writer's hands. Persisted the same way <<progress>> flags are: as tags on
// SaveManager ("Mist1", "Mist2", ...), so it survives save/load for free with no
// changes to SaveManager itself.
//
// Visually: a handful of fog sprite layers that follow the camera and fade their
// alpha up smoothly as the stage rises, with more layers becoming visible at
// higher stages for a denser look. Reuses the existing fog.png "fog wave" art —
// assign it and its Animator Controller (Fog.controller or Mist.controller) in
// the Inspector; no new art needed.
public class MistDirector : MonoBehaviour
{
    [System.Serializable]
    public class MistStage
    {
        [Range(0f, 1f)] public float alpha = 0.3f;
        [Tooltip("How many fog layers are visible at this stage — more layers overlapping reads as thicker.")]
        [Min(0)] public int activeLayers = 1;
    }

    [Header("Stages — index 0 is stage 1, reached via <<mist 1>>, etc.")]
    [SerializeField]
    private MistStage[] stages = new MistStage[]
    {
        new MistStage { alpha = 0.15f, activeLayers = 1 },
        new MistStage { alpha = 0.30f, activeLayers = 2 },
        new MistStage { alpha = 0.50f, activeLayers = 2 },
        new MistStage { alpha = 0.70f, activeLayers = 3 },
    };

    [Header("Visual Layers")]
    [Tooltip("The fog sprite — e.g. fog.png.")]
    [SerializeField] private Sprite fogSprite;
    [Tooltip("Optional — drives the existing fog-wave sprite-swap animation. Leave empty for a static (still fully functional) fog layer.")]
    [SerializeField] private RuntimeAnimatorController fogAnimator;
    [Min(1)]
    [SerializeField] private int layerCount = 3;
    [SerializeField] private float layerScale = 30f;
    [SerializeField] private int sortingOrder = 500;
    [Tooltip("How fast alpha eases toward the target when the stage changes (per second).")]
    [SerializeField] private float fadeSpeed = 0.3f;
    [Tooltip("How far each layer gently sways back and forth, in world units.")]
    [SerializeField] private float driftRange = 2f;
    [Tooltip("How fast the sway oscillates.")]
    [SerializeField] private float driftFrequency = 0.15f;

    public static MistDirector Instance { get; private set; }

    private Camera cam;
    private SpriteRenderer[] layers;
    private Vector2[] driftDirections;
    private float currentAlpha;
    private float targetAlpha;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildLayers();
    }

    private void Start()
    {
        RefreshTargetFromSave();
        currentAlpha = targetAlpha; // don't fade in from zero on scene load — snap to wherever the story left it
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildLayers()
    {
        layers = new SpriteRenderer[layerCount];
        driftDirections = new Vector2[layerCount];

        for (int i = 0; i < layerCount; i++)
        {
            GameObject go = new GameObject($"MistLayer{i}");
            go.transform.SetParent(transform);
            go.transform.localScale = new Vector3(layerScale, layerScale, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = fogSprite;
            sr.sortingOrder = sortingOrder + i;
            Color c = Color.white;
            c.a = 0f;
            sr.color = c;

            if (fogAnimator != null)
            {
                Animator anim = go.AddComponent<Animator>();
                anim.runtimeAnimatorController = fogAnimator;
            }

            layers[i] = sr;
            driftDirections[i] = Random.insideUnitCircle.normalized;

            go.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || layers == null) return;

        // Follows the camera each frame rather than being parented to it, so this
        // keeps working across whichever camera a scene switches to (interior/exterior
        // swaps already happen via Teleporter's Camera/OldCamera).
        transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, transform.position.z);

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        int stageIndex = CurrentStageIndex();
        int visibleLayers = stageIndex > 0 && stages != null && stageIndex - 1 < stages.Length
            ? stages[stageIndex - 1].activeLayers
            : 0;

        for (int i = 0; i < layers.Length; i++)
        {
            bool shouldShow = i < visibleLayers && currentAlpha > 0.001f;
            layers[i].gameObject.SetActive(shouldShow);
            if (!shouldShow) continue;

            Color c = layers[i].color;
            c.a = currentAlpha;
            layers[i].color = c;

            float wobble = Mathf.Sin(Time.time * driftFrequency + i * 1.7f) * driftRange;
            layers[i].transform.localPosition = driftDirections[i] * wobble;
        }
    }

    private int CurrentStageIndex()
    {
        if (SaveManager.Instance == null || stages == null) return 0;

        int stage = 0;
        for (int i = 1; i <= stages.Length; i++)
        {
            if (SaveManager.Instance.HasProgress($"Mist{i}"))
                stage = i;
        }
        return stage;
    }

    private void RefreshTargetFromSave()
    {
        int stageIndex = CurrentStageIndex();
        targetAlpha = stageIndex > 0 && stages != null && stageIndex - 1 < stages.Length
            ? stages[stageIndex - 1].alpha
            : 0f;
    }

    // The external hook — <<mist N>> in any .yarn node. Marks every stage up to N as
    // reached (so <<mist 3>> after skipping 2 still works correctly) and fades the
    // visual toward the new target.
    public void SetStage(int stage)
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("MistDirector: no SaveManager in the scene — mist stage can't be persisted.", this);
            return;
        }

        for (int i = 1; i <= stage; i++)
            SaveManager.Instance.AddProgress($"Mist{i}");

        RefreshTargetFromSave();
    }

    public static void Trigger(int stage)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"MistDirector: no MistDirector in the scene to set mist stage {stage}.");
            return;
        }

        Instance.SetStage(stage);
    }
}
