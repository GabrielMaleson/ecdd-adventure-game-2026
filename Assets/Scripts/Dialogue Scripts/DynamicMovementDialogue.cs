using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yarn.Unity;

public class DynamicCutsceneScript : MonoBehaviour
{
    [Header("Character References")]
    public List<CharacterEntry> characters = new List<CharacterEntry>();

    [Header("Waypoint References")]
    public List<WaypointEntry> waypoints = new List<WaypointEntry>();

    [Header("References")]
    public DialogueRunner dialogueRunner;


    [System.Serializable]
    public class CharacterEntry
    {
        public string characterName;
        public GameObject characterObject;
    }

    [System.Serializable]
    public class WaypointEntry
    {
        public string pointName;
        public Transform pointTransform;
    }

    // Dictionaries for lookups
    private Dictionary<string, GameObject> characterDictionary;
    private Dictionary<string, Transform> pointDictionary;

    // Static instance reference for static methods
    private static DynamicCutsceneScript instance;

    private void Awake()
    {
        // Set static instance
        instance = this;
    }

    private void Start()
    {
        if (dialogueRunner == null)
            dialogueRunner = FindFirstObjectByType<DialogueRunner>();

        // Build character dictionary
        characterDictionary = new Dictionary<string, GameObject>();
        foreach (var entry in characters)
        {
            if (!string.IsNullOrEmpty(entry.characterName) && entry.characterObject != null)
            {
                characterDictionary[entry.characterName.ToLower()] = entry.characterObject;
            }
        }

        // Build point dictionary
        pointDictionary = new Dictionary<string, Transform>();
        foreach (var entry in waypoints)
        {
            if (!string.IsNullOrEmpty(entry.pointName) && entry.pointTransform != null)
            {
                pointDictionary[entry.pointName] = entry.pointTransform;
            }
        }
    }

    [YarnCommand("activate")]
    public static void ActivateGuy(string[] parameters)
    {
        if (instance == null)
        {
            Debug.LogError("No DynamicCutsceneScript instance found!");
            return;
        }
        instance.ActivateGuyInternal(parameters);
    }

    private void ActivateGuyInternal(string[] parameters)
    {
        string characterName = parameters[0].ToLower();

        if (!characterDictionary.TryGetValue(characterName, out GameObject character))
        {
            Debug.LogError($"Character '{characterName}' not found. Available: {string.Join(", ", characterDictionary.Keys)}");
            return;
        }
        character.gameObject.SetActive(true);
    }

    [YarnCommand("deactivate")]
    public static void DeactivateGuy(string[] parameters)
    {
        if (instance == null)
        {
            Debug.LogError("No DynamicCutsceneScript instance found!");
            return;
        }
        instance.DeactivateGuyInternal(parameters);
    }

    private void DeactivateGuyInternal(string[] parameters)
    {
        string characterName = parameters[0].ToLower();

        if (!characterDictionary.TryGetValue(characterName, out GameObject character))
        {
            Debug.LogError($"Character '{characterName}' not found. Available: {string.Join(", ", characterDictionary.Keys)}");
            return;
        }
        character.gameObject.SetActive(false);
    }

    [YarnCommand("move")]
    public static void MoveCharacter(string[] parameters)
    {
        if (instance == null)
        {
            Debug.LogError("No DynamicCutsceneScript instance found!");
            return;
        }
        instance.MoveCharacterInternal(parameters);
    }

    private void MoveCharacterInternal(string[] parameters)
    {
        if (parameters.Length < 2)
        {
            Debug.LogError("Move command requires 2 parameters: character name and point name");
            return;
        }

        string characterName = parameters[0].ToLower();
        string pointName = parameters[1];

        if (!characterDictionary.TryGetValue(characterName, out GameObject character))
        {
            Debug.LogError($"Character '{characterName}' not found. Available: {string.Join(", ", characterDictionary.Keys)}");
            return;
        }

        if (!pointDictionary.TryGetValue(pointName, out Transform targetPoint))
        {
            Debug.LogError($"Point '{pointName}' not found. Available: {string.Join(", ", pointDictionary.Keys)}");
            return;
        }

        Debug.Log($"Moving {characterName} to {pointName}");
        StartCoroutine(MoveToPoint(character, targetPoint.position, true));
    }

    private IEnumerator MoveToPoint(GameObject character, Vector3 target, bool useAnimator)
    {
        if (character == null) yield break;

        Animator anim = character.GetComponent<Animator>();
        SpriteRenderer sprite = character.GetComponent<SpriteRenderer>();
        float speed = 5f;

        float originalY = character.transform.position.y;
        Vector3 lockedTarget = new Vector3(target.x, originalY, target.z);

        if (useAnimator && anim != null)
        {
            anim.SetBool("Andando", true);
        }

        Vector3 startPos = character.transform.position;
        float distance = Mathf.Abs(lockedTarget.x - startPos.x);
        float duration = distance / speed;
        float elapsed = 0f;

        if (distance < 0.01f)
        {
            if (useAnimator && anim != null)
                anim.SetBool("Andando", false);
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float newX = Mathf.Lerp(startPos.x, lockedTarget.x, t);
            character.transform.position = new Vector3(newX, originalY, character.transform.position.z);

            if (sprite != null)
            {
                float direction = lockedTarget.x - character.transform.position.x;
                if (Mathf.Abs(direction) > 0.1f)
                {
                    sprite.flipX = direction < 0;
                }
            }

            yield return null;
        }

        character.transform.position = new Vector3(lockedTarget.x, originalY, character.transform.position.z);

        if (useAnimator && anim != null)
        {
            anim.SetBool("Andando", false);
        }
    }
    private void OnDestroy()
    {
        if (dialogueRunner != null)
        {
            dialogueRunner.RemoveCommandHandler("activate");
            dialogueRunner.RemoveCommandHandler("deactivate");
            dialogueRunner.RemoveCommandHandler("move");
        }

        if (instance == this)
            instance = null;
    }
}