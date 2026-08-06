using System.Collections.Generic;
using UnityEngine;

// A named bunch of idle lines, living as an asset instead of inside a prefab.
// One asset per character per story beat (Marcus_Act1, Marcus_MistRising, ...) so the
// village can get progressively erratic by swapping the asset, not by retyping lists.
// Sets are shared: ten generic villagers can all point at the same one.
[CreateAssetMenu(fileName = "BarkSet", menuName = "ECDD/Bark Set")]
public class BarkSet : ScriptableObject
{
    [Tooltip("Lines picked from while this set is active.")]
    [TextArea(1, 3)]
    public List<string> lines = new List<string>();

    [Header("Condition (optional)")]
    [Tooltip("If set, this set only plays once SaveManager has this progress id.")]
    public string requiresProgress;

    [Tooltip("If set, this set only plays while the Yarn variable (e.g. $YarnTalkedElder) is true. Write it WITH the $.")]
    public string requiresYarnVariable;

    [Tooltip("Inverts both conditions above — the set plays only while they are NOT met.")]
    public bool invertCondition;

    // Evaluated by BarkDirector before it hands lines out. Reuses the two flag systems
    // the project already has rather than inventing a third one.
    public bool ConditionsMet()
    {
        bool met = true;

        if (!string.IsNullOrEmpty(requiresProgress))
            met &= SaveManager.Instance != null && SaveManager.Instance.HasProgress(requiresProgress);

        if (met && !string.IsNullOrEmpty(requiresYarnVariable))
            met &= BarkDirector.GetYarnBool(requiresYarnVariable);

        return invertCondition ? !met : met;
    }
}
