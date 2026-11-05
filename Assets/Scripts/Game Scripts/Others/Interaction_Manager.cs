
using System.Collections.Generic;
using UnityEngine;

public class Interactable_Manager : MonoBehaviour
{
    public static List<Interactable_Manager> allInteractables =
        new List<Interactable_Manager>();

    public enum InteractionType
    {
        Press,
        Hold
    }

    [Header("Interaction Settings")]
    public InteractionType interactionType = InteractionType.Hold;
    public Transform interactionPoint;


    // Dev note: Since some required to be hold and others need to just be pressed, I made this so we can easily set which is needed for the occasion
    public virtual string GetInteractionText()
    {
        if (interactionType == InteractionType.Press)
            return "Press [E] to Interact";

        return "Hold [E] to Interact";
    }

    public virtual bool CanInteract(Player_Manager player)
    {
        return true;
    }

    public virtual void Interact(float deltaTime)
    {
    }

    public virtual void StopInteraction()
    {
    }

    public Vector3 GetInteractionPosition()
    {
        if (interactionPoint != null)
            return interactionPoint.position;

        return transform.position;
    }

    protected virtual void OnEnable()
    {
        if (!allInteractables.Contains(this))
            allInteractables.Add(this);
    }

    protected virtual void OnDisable()
    {
        allInteractables.Remove(this);
    }
}
