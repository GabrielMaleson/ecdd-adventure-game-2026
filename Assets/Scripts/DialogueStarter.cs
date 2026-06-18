using PixelCrushers.DialogueSystem;
using UnityEngine;

public class DialogueStarter : MonoBehaviour
{
    public string Dialogue;
    public bool IsClickNPC;
    public bool OnceTime;
    public bool DoneOnce = false;
    public GameObject Notification;
    public DialogueEntry entry;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            entry.currentDialogueText = Dialogue;
        }
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            Debug.Log("yeah it work");
            if (OnceTime)
            {
                return;
            }
            if (Notification != null)
            {
                Notification.SetActive(true);
            }
            if (IsClickNPC)
            {
                if (Input.GetKeyDown(KeyCode.E))
                {
                    DialogueManager.StartConversation(Dialogue);
                }
            }
        }
    }
}