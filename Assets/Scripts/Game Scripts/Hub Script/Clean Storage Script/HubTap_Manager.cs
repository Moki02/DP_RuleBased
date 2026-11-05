
using UnityEngine;

public class HubTap_Manager : MonoBehaviour
{
    private Player_Manager player;
    private GameResources_Manager resources;

    private void Start()
    {
        player = Player_Manager.instance;
        resources = Game_Manager.instance.gameResources_Manager;
    }

    public bool CanDrink()
    {
        if (player == null || resources == null)
            return false;

        if (player.playerHydration_Manager.IsCollapsed())
            return false;

        return resources.cleanStorage > 0f &&
               player.playerStats_Manager.player_Hydration <
               player.playerStats_Manager.player_MaxHydration;
    }

    public bool CanRefill()
    {
        if (player == null || resources == null)
            return false;

        if (player.playerHydration_Manager.IsCollapsed())
            return false;

        return resources.cleanStorage > 0f &&
               resources.canteen < resources.canteen_Max;
    }

    public void Drink(float deltaTime)
    {
        if (!CanDrink())
            return;

        player.playerHydration_Manager.DrinkFromCleanStorage(deltaTime);
    }

    public void Refill(float deltaTime)
    {
        if (!CanRefill())
            return;

        player.playerHydration_Manager.RefillFromHub(deltaTime);
    }
}
