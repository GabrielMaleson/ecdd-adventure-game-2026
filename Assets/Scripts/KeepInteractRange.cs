using UnityEngine;

// "Leave this one alone." Drop it on an interactable whose trigger was drawn by hand for a
// reason — a zone covering a stretch of road, a doorway, a puzzle that needs a wider reach —
// and InteractSettings will skip it when it standardises every other trigger on load.
[DisallowMultipleComponent]
public class KeepInteractRange : MonoBehaviour
{
}
