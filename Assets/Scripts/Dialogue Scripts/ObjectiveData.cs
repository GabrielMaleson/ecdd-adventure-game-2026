using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Objective", menuName = "RPG/Objective")]
public class ObjectiveData : ScriptableObject
{
    [Header("Basic Info")]
    public string objectiveName;
    [TextArea(2, 4)]
    public string objectiveDescription;
    public Sprite objectiveIcon;

    [Header("Progress Tracking")]
    public List<string> requiredProgressTags = new List<string>(); // Progress tags needed to complete

    [Header("Completion")]
    public bool playDialogueOnComplete = true;
    public string completionDialogueNode; // Yarn node to start when completed

    [Header("Rewards")]
    public int rewardGold;
    public List<string> progressRewards = new List<string>(); // Progress tags to add when completed

    [Header("Next Objective (for quest chains)")]
    public ObjectiveData nextObjective;
}

// Simple runtime instance
[System.Serializable]
public class ObjectiveInstance
{
    public ObjectiveData data;
    public bool isActive = false;
    public bool isCompleted = false;
    public ObjectiveInstance(ObjectiveData objectiveData)
    {
        data = objectiveData;
    }
    public bool CheckCompletion(SaveManager inventory)
    {
        if (isCompleted || !isActive) return false;

        // Check if all required progress tags exist
        foreach (string requiredTag in data.requiredProgressTags)
        {
            if (!inventory.HasProgress(requiredTag))
                return false;
        }

        return true;
    }
}