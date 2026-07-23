using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

public class ProgressCheck : MonoBehaviour
{
    [System.Serializable]
    public class ConditionEntry
    {
        public string condition;
        public bool conditionMeansItDoesNotLoad = true;
    }

    [SerializeField] private List<ConditionEntry> conditions = new List<ConditionEntry>();
    [SerializeField] private string dialogueToLoad;
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private DialogueManager dialogueManager;

    private void OnEnable()
    {
        // When this object is enabled (by PreviousScene), start the check
        StartCoroutine(CheckConditionsAndLoadDialogueCoroutine());
    }

    private IEnumerator CheckConditionsAndLoadDialogueCoroutine()
    {
        // Wait one frame to ensure everything is initialized
        yield return null;

        // Find references if not assigned
        if (dialogueRunner == null)
            dialogueRunner = FindFirstObjectByType<DialogueRunner>();

        if (dialogueManager == null)
            dialogueManager = FindFirstObjectByType<DialogueManager>();

        if (string.IsNullOrEmpty(dialogueToLoad))
        {
            Debug.LogWarning("No dialogue specified to load.");
            yield break;
        }

        if (SaveManager.Instance == null)
        {
            Debug.LogError("SistemaInventario.Instance is null! Cannot check conditions.");
            yield break;
        }

        // Check all conditions
        bool shouldLoadDialogue = true;

        foreach (var conditionEntry in conditions)
        {
            bool hasCondition = SaveManager.Instance.HasProgress(conditionEntry.condition);

            if (conditionEntry.conditionMeansItDoesNotLoad && hasCondition)
            {
                shouldLoadDialogue = false;
                break;
            }
            else if (!conditionEntry.conditionMeansItDoesNotLoad && !hasCondition)
            {
                shouldLoadDialogue = false;
                break;
            }
        }

        if (shouldLoadDialogue)
        {
            // Try DialogueManager first
            if (dialogueManager != null)
            {
                dialogueManager.StartDialogue(dialogueToLoad);
                Debug.Log($"Started dialogue through DialogueManager: {dialogueToLoad}");
            }
            else if (dialogueRunner != null)
            {
                // Fallback to direct DialogueRunner
                dialogueRunner.StartDialogue(dialogueToLoad);
                Debug.LogWarning($"DialogueManager not found! Starting dialogue directly - player movement won't be restricted.");
            }
            else
            {
                Debug.LogError("No DialogueRunner or DialogueManager found!");
            }
        }
        else
        {
            Debug.Log($"Dialogue {dialogueToLoad} not loaded due to conditions not being met.");
        }
    }
}