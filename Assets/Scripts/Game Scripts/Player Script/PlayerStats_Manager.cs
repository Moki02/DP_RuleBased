
using UnityEngine;

public class PlayerStats_Manager : MonoBehaviour
{
    [Header("Movement Variable")]
    public float player_RotationalSpeed = 15;
    public float player_WalkingSpeed = 4f;
    public float player_RunningSpeed = 6.5f;

    [Header("Hydration")]
    public float player_MaxHydration = 100f;
    public float player_Hydration = 100f;

    public void ChangeHydration(float amount)
    {
        player_Hydration = Mathf.Clamp(player_Hydration + amount, 0f, player_MaxHydration);
    }


}
