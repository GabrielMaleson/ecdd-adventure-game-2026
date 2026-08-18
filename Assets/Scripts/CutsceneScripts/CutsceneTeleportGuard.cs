using System.Collections.Generic;
using UnityEngine;

// Cutscenes call DisableTeleporters() at the start and RestoreTeleporters() at the end
// so a live Teleporter object sitting near/at a cutscene's walk or spawn point can't
// fire mid-scene and hijack the player (walking them near one during a cutscene was
// immediately teleporting them right back out again).
//
// Requires a "Teleport" tag on every TeleporterScript object that should be paused
// during cutscenes — add that tag in Project Settings > Tags and Layers if it doesn't
// exist yet, and tag those objects with it. Until that tag exists, calls here just log
// a warning once and do nothing, so a missing tag doesn't crash every cutscene.
public static class CutsceneTeleportGuard
{
    const string TeleportTag = "Teleport";

    static List<GameObject> disabled;
    static bool warnedMissingTag;

    public static void DisableTeleporters()
    {
        if (disabled != null) return; // already held by an overlapping cutscene

        GameObject[] found;
        try
        {
            found = GameObject.FindGameObjectsWithTag(TeleportTag);
        }
        catch (UnityException)
        {
            if (!warnedMissingTag)
            {
                Debug.LogWarning($"CutsceneTeleportGuard: the \"{TeleportTag}\" tag doesn't exist yet — add it in Project Settings > Tags and Layers and tag the relevant Teleporter objects with it.");
                warnedMissingTag = true;
            }
            return;
        }

        disabled = new List<GameObject>(found);
        foreach (var obj in disabled)
            obj.SetActive(false);
    }

    public static void RestoreTeleporters()
    {
        if (disabled == null) return;

        foreach (var obj in disabled)
        {
            if (obj != null)
                obj.SetActive(true);
        }
        disabled = null;
    }
}
