// ============================================================================
// HubTapPoint_Manager.cs
// One spot on the hub tap the player holds E at: either "Drink" (drink from Clean Storage)
// or "Refill" (fill the canteen). The tank usually has one of each.
//
// DESIGN RULES: R2.4 (hub tap), Controls table (hold E).
//
// HOW TO USE: put it on a child of the Clean Storage object (the one with HubTap_Manager)
// and pick "Tap Action". "Hub Tap" fills itself from the parent if left empty.
// HOW TO EDIT: the prompt text is in GetInteractionText. It is always a Hold interaction
// (set in Awake), whatever the Inspector says.
// ============================================================================

using UnityEngine;

/// <summary>A drink or refill point on the hub tap.</summary>
public class HubTapPoint_Manager : Interactable_Manager
{
    /// <summary>What holding E here does.</summary>
    public enum TapAction
    {
        Drink,
        Refill
    }

    [Header("Tap Settings")]
    [Tooltip("Drink: drink from Clean Storage. Refill: fill the canteen from Clean Storage.")]
    public TapAction tapAction;

    [Header("Hub Tap")]
    [Tooltip("The tap this point belongs to. Empty = found on a parent object.")]
    public HubTap_Manager hubTap_Manager;

    private void Awake()
    {
        interactionType = InteractionType.Hold;
    }

    private void Start()
    {
        if (hubTap_Manager == null)
            hubTap_Manager = GetComponentInParent<HubTap_Manager>();
    }

    /// <inheritdoc/>
    public override string GetInteractionText()
    {
        if (tapAction == TapAction.Drink)
            return "Hold [E] to Drink";

        return "Hold [E] to Refill";
    }

    /// <inheritdoc/>
    public override bool CanInteract(Player_Manager player)
    {
        if (player == null || hubTap_Manager == null)
            return false;

        if (tapAction == TapAction.Drink)
            return hubTap_Manager.CanDrink();

        return hubTap_Manager.CanRefill();
    }

    /// <inheritdoc/>
    public override void Interact(float deltaTime)
    {
        if (hubTap_Manager == null)
            return;

        if (tapAction == TapAction.Drink)
        {
            hubTap_Manager.Drink(deltaTime);
        }
        else
        {
            hubTap_Manager.Refill(deltaTime);
        }
    }
}
