
using UnityEngine;
using TMPro;

public class HubPump_Manager : Interactable_Manager
{
    [Header("Pump Settings")]
    public float interactionDistance = 2f;
    public float strokeInterval = 0.5f;
    public float waterPerStroke = 0.2f;

    [Header("Pump UI")]
    public TMP_Text fullPopup;

    private Player_Manager player;
    private GameResources_Manager resources;

    private float strokeTimer;

    private void Start()
    {
        player = Player_Manager.instance;
        resources = Game_Manager.instance.gameResources_Manager;

        interactionType = InteractionType.Hold;

        // Dev Note: Haven't done the ui on how to show the indicator
        if (fullPopup != null)
            fullPopup.gameObject.SetActive(false);
    }

    public override bool CanInteract(Player_Manager targetPlayer)
    {
        if (targetPlayer == null)
            return false;

        if (targetPlayer.playerHydration_Manager.IsCollapsed())
            return false;

        Vector3 playerPosition = targetPlayer.transform.position;
        Vector3 pumpPosition = GetInteractionPosition();

        playerPosition.y = 0f;
        pumpPosition.y = 0f;

        return Vector3.Distance(playerPosition, pumpPosition)
               <= interactionDistance;
    }

    public override string GetInteractionText()
    {
        return "Hold [E] to Pump";
    }

    public override void Interact(float deltaTime)
    {
        if (player == null || resources == null)
            return;

        player.playerHydration_Manager.isPumping = true;

        strokeTimer += deltaTime;

        while (strokeTimer >= strokeInterval)
        {
            strokeTimer -= strokeInterval;
            PumpStroke();
        }
    }

    public override void StopInteraction()
    {
        strokeTimer = 0f;

        if (player != null)
            player.playerHydration_Manager.isPumping = false;
    }

    private void PumpStroke()
    {
        if (resources.rawBasin >= resources.rawBasin_Max)
        {
            if (fullPopup != null)
                fullPopup.gameObject.SetActive(true);

            return;
        }

        resources.AddRawWater(waterPerStroke);

        if (fullPopup != null)
            fullPopup.gameObject.SetActive(false);
    }

    // Dev note: Will connect the dought stages that's inside the GameClock_Manager to find what's the current stage and change the output of the handpump.

}
