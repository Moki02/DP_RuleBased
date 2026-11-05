using UnityEngine;
using System;

public class PlayerHydration_Manager : MonoBehaviour
{
    public static PlayerHydration_Manager instance;
    public Player_Manager player;
    GameResources_Manager resources;

    public enum HydrationState
    {
        Hydrated,
        Thirsty,
        Dehydrated,
        Collapsed
    }

    [Header("Hydration State")]
    public HydrationState currentHydrationState = HydrationState.Hydrated;

    [Header("Hydration Drain")]
    public float drain_Idle = 0.25f;
    public float drain_Walk = 0.5f;
    public float drain_Sprint = 1.2f;
    public float drain_Pump = 0.6f;

    [Header("Drinking")]
    public float drinkRate = 0.5f;
    public float refillRate = 1f;
    public float hydrationPerLiter = 25f;

    [Header("Sickness")]
    public float sickDuration = 30f;
    public float sickDrainMultiplier = 1.5f;
    private float sickTimer;

    [Header("Hydration Modifiers")]
    public float droughtHeatMultiplier = 1f;
    public float eventMultiplier = 1f;
    public float skillMultiplier = 1f;

    [Header("Hub Settings")]
    public BoxCollider hubArea;
    public float hubDrainMultiplier = 0.5f;

    [Header("Activity")]
    public bool isPumping;
    public bool isPlayingMinigame;

    public event Action OnCollapsed;

    private bool collapseTriggered = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(this);
            return;
        }

        player = GetComponent<Player_Manager>();
    }

    private void Start()
    {
        resources = Game_Manager.instance.gameResources_Manager;

        UpdateHydrationState();
    }

    private void Update()
    {
        if (Time.timeScale == 0 || resources == null)
            return;

        UpdateHydrationState();

        if (currentHydrationState == HydrationState.Collapsed)
        {
            HandleCollapse();
            return;
        }

        HandleSickness();
        HandleHydrationDrain();

        UpdateHydrationState();
        HandleCollapse();
    }

    // Dev note: To make it easier to check the hydration status of the player, I used states instead of using plenty of if else value
    private void UpdateHydrationState()
    {
        float hydration = player.playerStats_Manager.player_Hydration;

        if (hydration <= 0)
        {
            currentHydrationState = HydrationState.Collapsed;
        }
        else if (hydration < 30)
        {
            currentHydrationState = HydrationState.Dehydrated;
        }
        else if (hydration < 60)
        {
            currentHydrationState = HydrationState.Thirsty;
        }
        else
        {
            currentHydrationState = HydrationState.Hydrated;
        }
    }

    public bool CanSprint()
    {
        return currentHydrationState != HydrationState.Dehydrated &&
               currentHydrationState != HydrationState.Collapsed;
    }

    public bool IsCollapsed()
    {
        return currentHydrationState == HydrationState.Collapsed;
    }

    private void HandleSickness()
    {
        if (sickTimer > 0)
        {
            sickTimer -= Time.deltaTime;
            sickTimer = Mathf.Max(0, sickTimer);
        }
    }

    private void HandleHydrationDrain()
    {
        float drain = drain_Idle;

        float moveAmount = player.playerInput_Manager.moveAmount;

        bool sprinting = player.playerInput_Manager.sprintInput &&
                         moveAmount > 0 &&
                         CanSprint();

        if (isPlayingMinigame)
        {
            drain = drain_Idle;
        }
        else if (isPumping)
        {
            drain = drain_Pump;
        }
        else if (sprinting)
        {
            drain = drain_Sprint;
        }
        else if (moveAmount > 0)
        {
            drain = drain_Walk;
        }

        if (currentHydrationState == HydrationState.Thirsty && sprinting)
        {
            drain *= 1.5f;
        }

        drain *= droughtHeatMultiplier;
        drain *= eventMultiplier;
        drain *= skillMultiplier;

        if (sickTimer > 0)
        {
            drain *= sickDrainMultiplier;
        }

        if (IsInsideHub())
        {
            drain *= hubDrainMultiplier;
        }

        player.playerStats_Manager.ChangeHydration(-drain * Time.deltaTime);
    }

    public bool IsInsideHub()
    {
        if (hubArea == null)
            return false;

        return hubArea.bounds.Contains(player.transform.position);
    }

    public void HandleDrinkAction()
    {
        if (resources == null)
            return;

        if (currentHydrationState == HydrationState.Collapsed)
            return;

        if (resources.canteen <= 0)
            return;

        if (player.playerStats_Manager.player_Hydration >=
            player.playerStats_Manager.player_MaxHydration)
            return;

        if (player.playerAnimator_Manager.isPerformingAction)
            return;

        player.playerAnimator_Manager.PlayTargetActionAnimation(
            "Drink",
            true,
            false,
            false,
            false
        );
    }

    // Dev Note: This is being called inside the animation ("Consume" animation will change when we get new animation).
    // I also changed the way it works so this is different from what is written inside the docs but I will make it how it is intended once we get proper animation.
    public void DrinkFromCanteen()
    {
        if (resources.canteen < 1f)
            return;

        resources.TakeCanteenWater(1f);

        player.playerStats_Manager.ChangeHydration(25f);
    }

    // Dev Note: This is being called by the end of the animation ("Consume" animation will change when we get new animation)
    public void EndDrinkAnimation()
    {
        if (currentHydrationState == HydrationState.Collapsed)
            return;

        player.playerAnimator_Manager.ResetActionAnimation();

        player.animator.CrossFade("Walking Blend Tree", 0.2f);
    }

    public void DrinkFromCleanStorage(float deltaTime)
    {
        float amountNeeded =
            (player.playerStats_Manager.player_MaxHydration - player.playerStats_Manager.player_Hydration)
            / hydrationPerLiter;

        float amount = Mathf.Min(
            drinkRate * deltaTime,
            amountNeeded
        );

        float consumed = resources.TakeCleanWater(amount);

        player.playerStats_Manager.ChangeHydration(consumed * hydrationPerLiter);
    }

    public void RefillFromHub(float deltaTime)
    {
        resources.RefillCanteen(refillRate * deltaTime);
    }

    // Dev Note: I haven't added this but I already made the function incase I forget about it
    public void DrinkRawFromBasin(float amount)
    {
        if (amount <= 0)
            return;

        float amountNeeded =
            (player.playerStats_Manager.player_MaxHydration - player.playerStats_Manager.player_Hydration)
            / hydrationPerLiter;

        float consumed = resources.TakeRawWater(
            Mathf.Min(amount, amountNeeded)
        );

        if (consumed > 0)
        {
            player.playerStats_Manager.ChangeHydration(consumed * hydrationPerLiter);
            sickTimer = sickDuration;
        }
    }

    private void HandleCollapse()
    {
        if (currentHydrationState != HydrationState.Collapsed)
        {
            collapseTriggered = false;
            return;
        }

        if (collapseTriggered)
            return;

        collapseTriggered = true;

        Debug.Log("Player Collapsed!");
        OnCollapsed?.Invoke();
    }


    public void ResetAfterRevive(float hydration)
    {

        player.playerStats_Manager.player_Hydration =
            Mathf.Clamp(
                hydration,
                1f,
                player.playerStats_Manager.player_MaxHydration
            );


        isPumping = false;
        isPlayingMinigame = false;
        sickTimer = 0f;

        collapseTriggered = false;
        UpdateHydrationState();
    }
}
