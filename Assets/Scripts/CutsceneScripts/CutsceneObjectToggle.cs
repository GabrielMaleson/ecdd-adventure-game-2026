using System.Collections.Generic;
using UnityEngine;

// A reusable "on/off switch" for a fixed set of GameObjects belonging to ONE cutscene.
// Attach one instance PER cutscene, each with its own Objects list assigned in the
// Inspector — e.g. InitialCutscene gets one with just its own CutsceneHaze; Square_Haze
// gets a SEPARATE instance with its own CutsceneHaze and the Villager NPC. Because each
// cutscene owns its own instance and list, toggling one never touches another
// cutscene's actors.
public class CutsceneObjectToggle : MonoBehaviour
{
    [Tooltip("The GameObjects this instance is allowed to activate/deactivate.")]
    public List<GameObject> objects = new List<GameObject>();

    public void SetActive(bool active)
    {
        foreach (var obj in objects)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }

    public void Activate() => SetActive(true);
    public void Deactivate() => SetActive(false);
}
