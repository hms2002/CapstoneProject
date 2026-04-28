using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class DialogueChoiceInputRelay : MonoBehaviour, IPointerEnterHandler
{
    private DialogueView owner;
    private int choiceIndex = -1;

    public void Bind(DialogueView view, int index)
    {
        owner = view;
        choiceIndex = index;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.SelectChoiceFromPointer(choiceIndex);
    }
}
