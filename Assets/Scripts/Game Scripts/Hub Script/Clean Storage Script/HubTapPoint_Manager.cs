
using UnityEngine;

public class HubTapPoint_Manager : Interactable_Manager
{
    public enum TapAction
    {
        Drink,
        Refill
    }

    [Header("Tap Settings")]
    public TapAction tapAction;

    [Header("Hub Tap")]
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

    public override string GetInteractionText()
    {
        if (tapAction == TapAction.Drink)
            return "Hold [E] to Drink";

        return "Hold [E] to Refill";
    }

    public override bool CanInteract(Player_Manager player)
    {
        if (player == null || hubTap_Manager == null)
            return false;

        if (tapAction == TapAction.Drink)
            return hubTap_Manager.CanDrink();

        return hubTap_Manager.CanRefill();
    }

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
