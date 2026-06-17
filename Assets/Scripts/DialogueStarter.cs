using PixelCrushers.DialogueSystem;
using UnityEngine;

public class DialogueStarter : MonoBehaviour
{
    public string Dialogue;
    void Start()
    {
        DialogueManager.StartConversation(Dialogue);
    }

}
