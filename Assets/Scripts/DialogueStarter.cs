using PixelCrushers.DialogueSystem;
using UnityEngine;

public class DialogueStarter : MonoBehaviour
{
    public string Dialogue;
    public bool IsClickNPC;
    public bool OnceTime;
    public bool DoneOnce = false;
    public GameObject Notification;
    public Transform transformthing;

    private void Update()
    {
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
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
            else
            {
                DialogueManager.Bark(Dialogue,transformthing);
            }
        }
    }
}