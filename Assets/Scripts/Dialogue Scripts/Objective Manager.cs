using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using Yarn.Unity;

public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager Instance { get; private set; }

    [Header("Active Objectives")]
    public List<ObjectiveInstance> activeObjectives = new List<ObjectiveInstance>();
    public List<ObjectiveInstance> completedObjectives = new List<ObjectiveInstance>();

    [Header("UI References")]
    public GameObject questListPanel;           // Main panel containing the quest list
    public Transform questListContainer;        // Container for quest name buttons (Vertical Layout Group)
    public GameObject questNameButtonPrefab;    // Prefab for quest name buttons

    public GameObject questDetailsPanel;        // Panel that shows selected quest details
    public TextMeshProUGUI selectedQuestNameText;   // Displays selected quest name
    public TextMeshProUGUI selectedQuestDescText;   // Displays selected quest description
    public TextMeshProUGUI selectedQuestProgressText; // Optional: shows progress
    public Button closeDetailsButton;           // Button to close details panel

    [Header("Optional - Quest Log")]
    public Button openQuestLogButton;           // Button to open the quest log
    public Button closeQuestLogButton;          // Button to close the quest log

    [Header("Colors")]
    public Color activeQuestColor = Color.white;
    public Color completedQuestColor = Color.gray;

    [Header("Objective Database")]
    public List<ObjectiveData> allObjectives;  // All available objectives for lookups

    [Header("Events")]
    public System.Action<ObjectiveInstance> onObjectiveAdded;
    public System.Action<ObjectiveInstance> onObjectiveCompleted;

    private DialogueRunner dialogueRunner;
    private SaveManager inventory;
    private ObjectiveInstance currentSelectedObjective;

    // Dictionary for faster lookups
    private Dictionary<string, ObjectiveData> objectiveDictionary;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            gameObject.tag = "Inventory";

            // Build lookup dictionary
            BuildObjectiveDictionary();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void BuildObjectiveDictionary()
    {
        objectiveDictionary = new Dictionary<string, ObjectiveData>();

        if (allObjectives != null)
        {
            foreach (var obj in allObjectives)
            {
                if (obj != null && !objectiveDictionary.ContainsKey(obj.objectiveName))
                {
                    objectiveDictionary.Add(obj.objectiveName, obj);
                }
            }
        }
    }

    private void Start()
    {
        dialogueRunner = FindFirstObjectByType<DialogueRunner>();
        inventory = SaveManager.Instance;

        // questListPanel stays active — visibility controlled by parent CanvasGroup alpha
        // questDetailsPanel starts hidden until a quest is selected
        if (questDetailsPanel != null)
            questDetailsPanel.SetActive(false);

        // Set up button listeners
        if (openQuestLogButton != null)
            openQuestLogButton.onClick.AddListener(OpenQuestLog);

        if (closeQuestLogButton != null)
            closeQuestLogButton.onClick.AddListener(CloseQuestLog);

        if (closeDetailsButton != null)
            closeDetailsButton.onClick.AddListener(CloseDetails);
    }

    public void OpenQuestLog()
    {
        if (questListPanel != null)
        {
            RefreshQuestList();
            questListPanel.SetActive(true);
        }
    }

    public void CloseQuestLog()
    {
        if (questListPanel != null)
            questListPanel.SetActive(false);

        if (questDetailsPanel != null)
            questDetailsPanel.SetActive(false);

        currentSelectedObjective = null;
    }

    public void CloseDetails()
    {
        if (questDetailsPanel != null)
            questDetailsPanel.SetActive(false);

        currentSelectedObjective = null;
    }

    public void RefreshQuestList()
    {
        // Clear existing buttons
        foreach (Transform child in questListContainer)
        {
            Destroy(child.gameObject);
        }

        int questIndex = 1;

        // Add active objectives first
        foreach (var objective in activeObjectives)
        {
            if (objective != null && objective.data != null)
            {
                CreateQuestButton(objective, activeQuestColor, questIndex++);
            }
        }

        // Add completed objectives (grayed out)
        foreach (var objective in completedObjectives)
        {
            if (objective != null && objective.data != null)
            {
                CreateQuestButton(objective, completedQuestColor, questIndex++);
            }
        }
    }

    private void CreateQuestButton(ObjectiveInstance objective, Color textColor, int questNumber)
    {
        GameObject buttonObj = Instantiate(questNameButtonPrefab, questListContainer);
        TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        Button button = buttonObj.GetComponent<Button>();

        if (buttonText != null)
        {
            buttonText.text = $"Quest #{questNumber}";
            buttonText.color = textColor;
        }

        if (button != null)
        {
            ObjectiveInstance capturedObjective = objective;
            button.onClick.AddListener(() => ShowQuestDetails(capturedObjective));
        }
    }

    private void ShowQuestDetails(ObjectiveInstance objective)
    {
        currentSelectedObjective = objective;

        if (questDetailsPanel != null)
            questDetailsPanel.SetActive(true);

        // Update details UI
        if (selectedQuestNameText != null)
        {
            string status = objective.isCompleted ? " [COMPLETED]" : (objective.isActive ? " [ATIVA]" : "");
            selectedQuestNameText.text = objective.data.objectiveName + status;
        }

        if (selectedQuestDescText != null)
            selectedQuestDescText.text = objective.data.objectiveDescription;

        // Update progress text if available
        if (selectedQuestProgressText != null)
        {
            string progressText = GetQuestProgress(objective);
            selectedQuestProgressText.text = progressText;
        }
    }

    private string GetQuestProgress(ObjectiveInstance objective)
    {
        if (objective.isCompleted)
            return "Completed";

        if (inventory == null) return "";

        // Check progress on required tags
        int completedCount = 0;
        int totalRequired = objective.data.requiredProgressTags.Count;

        foreach (string requiredTag in objective.data.requiredProgressTags)
        {
            if (inventory.HasProgress(requiredTag))
                completedCount++;
        }

        if (totalRequired > 0)
        {
            float percent = (completedCount / (float)totalRequired) * 100;
            return $"Progress: {completedCount}/{totalRequired} ({percent:F0}%)";
        }

        return "";
    }

    public void AddObjective(ObjectiveData objectiveData)
    {
        if (objectiveData == null) return;

        // Check if already active
        foreach (var obj in activeObjectives)
        {
            if (obj.data == objectiveData) return;
        }

        // Check if already completed
        foreach (var obj in completedObjectives)
        {
            if (obj.data == objectiveData) return;
        }

        ObjectiveInstance instance = new ObjectiveInstance(objectiveData);
        instance.isActive = true;

        activeObjectives.Add(instance);

        Debug.Log($"Objective added: {objectiveData.objectiveName}");
        onObjectiveAdded?.Invoke(instance);

        RefreshQuestList();
    }

    public void AddObjectiveByName(string objectiveName)
    {
        if (objectiveDictionary == null)
            BuildObjectiveDictionary();

        if (objectiveDictionary.ContainsKey(objectiveName))
        {
            AddObjective(objectiveDictionary[objectiveName]);
        }
        else
        {
            Debug.LogError($"Objective '{objectiveName}' not found in database!");
        }
    }

    // Call this whenever progress is added (from SistemaInventario)
    public void CheckAllObjectives()
    {
        if (inventory == null) return;

        List<ObjectiveInstance> toComplete = new List<ObjectiveInstance>();

        foreach (var instance in activeObjectives)
        {
            if (instance.CheckCompletion(inventory))
            {
                toComplete.Add(instance);
            }
        }

        foreach (var instance in toComplete)
        {
            CompleteObjective(instance);
        }

        // Refresh quest list if it's open
        if (questListPanel != null && questListPanel.activeSelf)
            RefreshQuestList();

        // Update details if the completed objective is currently selected
        if (currentSelectedObjective != null && toComplete.Contains(currentSelectedObjective))
        {
            ShowQuestDetails(currentSelectedObjective);
        }
    }

    private void CompleteObjective(ObjectiveInstance instance)
    {
        instance.isActive = false;
        instance.isCompleted = true;

        activeObjectives.Remove(instance);
        completedObjectives.Add(instance);

        // Add progress rewards
        if (inventory != null)
        {
            foreach (string progressTag in instance.data.progressRewards)
            {
                inventory.AddProgress(progressTag);
            }
        }

        // Play completion dialogue
        if (instance.data.playDialogueOnComplete &&
            !string.IsNullOrEmpty(instance.data.completionDialogueNode) &&
            dialogueRunner != null)
        {
            dialogueRunner.StartDialogue(instance.data.completionDialogueNode);
        }

        Debug.Log($"Objective completed: {instance.data.objectiveName}");
        onObjectiveCompleted?.Invoke(instance);

        // Set next objective as current if it exists
        if (instance.data.nextObjective != null)
        {
            AddObjective(instance.data.nextObjective);
        }
    }

    // Yarn Command to add objective
    [YarnCommand("add_objective")]
    public static void AddObjectiveCommand(string objectiveName)
    {
        if (Instance != null)
        {
            Debug.Log($"Adding objective: {objectiveName}");
            Instance.AddObjectiveByName(objectiveName);
        }
        else
        {
            Debug.LogError("ObjectiveManager instance not found!");
        }
    }

    // Yarn Command to check if objective is completed
    [YarnFunction("objective_completed")]
    public static bool ObjectiveCompleted(string objectiveName)
    {
        if (Instance == null) return false;

        foreach (var obj in Instance.completedObjectives)
        {
            if (obj.data.objectiveName == objectiveName)
                return true;
        }
        return false;
    }

    // Yarn Command to check if objective is active
    [YarnFunction("objective_active")]
    public static bool ObjectiveActive(string objectiveName)
    {
        if (Instance == null) return false;

        foreach (var obj in Instance.activeObjectives)
        {
            if (obj.data.objectiveName == objectiveName)
                return true;
        }
        return false;
    }

    // Yarn Command to open quest log
    [YarnCommand("open_quest_log")]
    public static void OpenQuestLogCommand()
    {
        if (Instance != null)
            Instance.OpenQuestLog();
    }

    // Yarn Command to close quest log
    [YarnCommand("close_quest_log")]
    public static void CloseQuestLogCommand()
    {
        if (Instance != null)
            Instance.CloseQuestLog();
    }

    private void OnValidate()
    {
        // Rebuild dictionary in editor if list changes
        if (Application.isPlaying)
            BuildObjectiveDictionary();
    }
}