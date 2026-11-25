// ============================================================================
// HubTapPoint_Manager.cs
// The spot on the hub tap where the player holds E. The Clean Storage has ONE point set to
// "Drink Then Refill": holding E drinks from Clean Storage until HYD is full, then carries
// on filling the canteen, without letting go. "Drink" and "Refill" still work on their own
// for any other tap that wants separate points.
//
// DESIGN RULES: R2.4 (hub tap), Controls table (hold E).
//
// HOW TO USE: put it on a child of the Clean Storage object (the one with HubTap_Manager)
// and pick "Tap Action". "Hub Tap" fills itself from the parent if left empty.
// HOW TO EDIT: the prompt text is in GetInteractionText. It is always a Hold interaction
// (set in Awake), whatever the Inspector says.
// ============================================================================

using UnityEngine;

/// <summary>A drink / refill point on the hub tap.</summary>
public class HubTapPoint_Manager : Interactable_Manager
{
    /// <summary>What holding E here does.</summary>
    public enum TapAction
    {
        Drink,
        Refill,
        DrinkThenRefill
    }

    [Header("Tap Settings")]
    [Tooltip("Drink: drink from Clean Storage. Refill: fill the canteen. " +
             "Drink Then Refill: drink until HYD is full, then fill the canteen (one E for both).")]
    public TapAction tapAction = TapAction.DrinkThenRefill;

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

    [Tooltip("Drink Then Refill: HYD this close to max counts as full and the tap moves on to the canteen. " +
             "Needed because HYD drains a sliver every frame, so drinking never lands exactly on max.")]
    [Min(0f)] public float fullHydMargin = 0.5f;

    // Drink Then Refill: once this hold has moved on to the canteen it stays there until E is let go.
    private bool refillingThisHold;

    // What E does right now: drink while HYD isn't (nearly) full, otherwise refill.
    private bool WillDrink()
    {
        if (tapAction == TapAction.Drink) return true;
        if (tapAction == TapAction.Refill) return false;
        if (refillingThisHold || hubTap_Manager == null || !hubTap_Manager.CanDrink()) return false;

        var player = Player_Manager.instance;
        if (player == null) return true;
        var stats = player.playerStats_Manager;
        return stats.player_Hydration < stats.player_MaxHydration - fullHydMargin;
    }

    /// <inheritdoc/>
    public override void StopInteraction()
    {
        base.StopInteraction();
        refillingThisHold = false;
    }

    /// <inheritdoc/>
    public override string GetInteractionText()
    {
        return WillDrink() ? "Hold [E] to Drink" : "Hold [E] to Refill Canteen";
    }

    /// <inheritdoc/>
    public override bool CanInteract(Player_Manager player)
    {
        if (player == null || hubTap_Manager == null)
            return false;

        switch (tapAction)
        {
            case TapAction.Drink: return hubTap_Manager.CanDrink();
            case TapAction.Refill: return hubTap_Manager.CanRefill();
            default: return WillDrink() || hubTap_Manager.CanRefill();
        }
    }

    /// <inheritdoc/>
    public override void Interact(float deltaTime)
    {
        if (hubTap_Manager == null)
            return;

        if (WillDrink())
        {
            hubTap_Manager.Drink(deltaTime);
        }
        else
        {
            if (tapAction == TapAction.DrinkThenRefill) refillingThisHold = true;
            hubTap_Manager.Refill(deltaTime);
        }
    }
}
