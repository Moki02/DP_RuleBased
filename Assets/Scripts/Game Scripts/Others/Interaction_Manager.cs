// ============================================================================
// Interaction_Manager.cs   (the class inside is called Interactable_Manager)
// The base class for everything the player can use with E: the hand pump, the hub tap,
// lock basins, valve gates, room water and notes. Each of those scripts "inherits" from
// this one and fills in what its prompt says, when it can be used, and what using it does.
//
// Every interactable adds itself to one shared list while it's switched on;
// PlayerInteraction_Manager looks through that list for the closest one inside the
// player's interaction box.
//
// DESIGN RULES: Controls table (E hold = interact), R8.9 (hold E to pour into a lock basin).
//
// HOW TO USE: don't put this script on an object by itself. Use one of the scripts built on
// it (HubTapPoint_Manager, HubPump_Manager, LockBasin_Manager, ValveGate, ...). Optionally drag
// a child object into "Interaction Point" to say where the player must stand near.
// HOW TO EDIT: to make a new kind of interactable, write a class that ": Interactable_Manager"
// and override GetInteractionText, CanInteract, Interact and (for Hold) StopInteraction.
// If your class needs OnEnable/OnDisable, write "protected override" and call base, or it
// won't be found by the player.
// Note: the file name doesn't match the class name; that's fine for a base class, but don't
// rename either (prefabs point at the class).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>Base class for anything the player can interact with using E.</summary>
public class Interactable_Manager : MonoBehaviour
{
    /// <summary>Every interactable that is switched on right now (the player searches this list).</summary>
    public static List<Interactable_Manager> allInteractables =
        new List<Interactable_Manager>();

    /// <summary>Press = one tap of E does it once. Hold = it keeps working while E is held.</summary>
    public enum InteractionType
    {
        Press,
        Hold
    }

    [Header("Interaction Settings")]
    [Tooltip("Press: one tap of E. Hold: works every frame while E is held (pumping, pouring, drinking).")]
    public InteractionType interactionType = InteractionType.Hold;
    [Tooltip("Optional. The spot that must be inside the player's interaction box. Empty = this object's position.")]
    public Transform interactionPoint;


    // Dev note: Since some required to be hold and others need to just be pressed, I made this so we can easily set which is needed for the occasion
    /// <summary>The prompt shown on screen, e.g. "Hold [E] to Drink".</summary>
    public virtual string GetInteractionText()
    {
        if (interactionType == InteractionType.Press)
            return "Press [E] to Interact";

        return "Hold [E] to Interact";
    }

    /// <summary>False hides the prompt and stops the player using it (e.g. the tap when storage is empty).</summary>
    public virtual bool CanInteract(Player_Manager player)
    {
        return true;
    }

    /// <summary>
    /// Does the interaction. For Hold it's called every frame E is held, with that frame's
    /// time in seconds (multiply rates by it). For Press it's called once, with 0.
    /// </summary>
    public virtual void Interact(float deltaTime)
    {
    }

    /// <summary>Called when a Hold interaction ends (E released, player walked away, or a menu opened).</summary>
    public virtual void StopInteraction()
    {
    }

    /// <summary>World position used for the "is it in reach" check.</summary>
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
