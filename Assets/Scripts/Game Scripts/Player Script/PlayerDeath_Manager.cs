// ============================================================================
// PlayerDeath_Manager.cs
// The player's collapse animation and waking up at the hub.
//
// In the maze game, CollapseManager runs the whole collapse (R11.1 to R11.4: fade, water
// costs, game over check) and calls ReviveAt when the player wakes. In old scenes with no
// CollapseManager this script works as before: the Collapse HUD shows, and its button
// calls ReviveCharacter.
//
// Order of a collapse in the maze game: PlayerHydration_Manager fires OnCollapsed ->
// HandleCollapse here plays the animation (in unscaled time) and sets the game state to
// Collapsed (time scale 0) -> CollapseManager fades, takes the costs and calls ReviveAt,
// which puts the player at the hub and sets the state back to Exploring (time scale 1).
//
// DESIGN RULES: R11.1 (collapse at 0 HYD, wake at the hub), R11.3 (wake HYD comes from
// CollapseManager).
//
// HOW TO USE: on the Player prefab. "Respawn Point" is only needed in old scenes.
// HOW TO EDIT: the animation's state name is "Collapse Animation". Wake-up numbers are in
// GameBalance (Collapse and game over); "Revive Hydration" is only for old scenes.
// ============================================================================

using UnityEngine;
using TheLastDrop;

/// <summary>Plays the collapse, freezes the game, and wakes the player up again (ReviveAt).</summary>
public class PlayerDeath_Manager : MonoBehaviour
{
    [Header("Respawn Settings")]
    [Tooltip("Old scenes only: where the player wakes. The maze game uses the maze's spawn.")]
    public Transform respawnPoint;
    [Tooltip("Old scenes only: HYD after waking (the maze game uses GameBalance: 50, 60 with E3).")]
    public float reviveHydration = 50f;

    [Header("Collapse UI")]
    [Tooltip("Old scenes only: the panel with the revive button. Hidden in the maze game (CollapseManager fades instead).")]
    public GameObject collapseHUD;

    [Header("Animation")]
    [Tooltip("Animator state played when the player collapses.")]
    public string collapseAnimation = "Collapse";

    [Tooltip("True from the collapse until ReviveAt (read only).")]
    public bool isDead = false;

    private Player_Manager player;
    private Animator animator;
    private AnimatorUpdateMode originalUpdateMode;

    private void Awake()
    {
        player = GetComponent<Player_Manager>();
        animator = GetComponent<Animator>();

        if (animator != null)
            originalUpdateMode = animator.updateMode;
    }

    private void Start()
    {
        if (player != null && player.playerHydration_Manager != null)
            player.playerHydration_Manager.OnCollapsed += HandleCollapse;

        if (collapseHUD != null)
            collapseHUD.SetActive(false);
    }

    private void OnDestroy()
    {
        if (player != null && player.playerHydration_Manager != null)
            player.playerHydration_Manager.OnCollapsed -= HandleCollapse;
    }

    private void HandleCollapse()
    {
        if (isDead)
            return;

        isDead = true;

        if (animator != null)
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.Play(collapseAnimation, 0, 0f);
        }

        // The maze game: CollapseManager shows the fade and handles the rest.
        if (collapseHUD != null && CollapseManager.Instance == null)
            collapseHUD.SetActive(true);

        if (Game_Manager.instance != null)
            Game_Manager.instance.ChangeGameState(
                Game_Manager.GameState.Collapsed
            );
    }

    /// <summary>Old scenes: the Collapse HUD's button calls this (wakes at Respawn Point).</summary>
    public void ReviveCharacter()
    {
        if (!isDead || respawnPoint == null)
            return;

        ReviveAt(respawnPoint.position, respawnPoint.rotation, reviveHydration);
    }

    /// <summary>Wakes the player at a position with this much HYD and gives control back.</summary>
    public void ReviveAt(Vector3 position, Quaternion rotation, float hydration)
    {
        player.playerHydration_Manager.ResetAfterRevive(hydration);

        CharacterController controller = player.characterController;
        if (controller != null)
            controller.enabled = false;   // the CharacterController fights being moved by hand

        transform.SetPositionAndRotation(position, rotation);

        if (controller != null)
            controller.enabled = true;

        if (Player_Camera.instance != null)
            Player_Camera.instance.TurnWithPlayer(0f); // snap the camera onto the player

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.updateMode = originalUpdateMode;
        }

        if (player.playerAnimator_Manager != null)
            player.playerAnimator_Manager.ResetActionAnimation();

        isDead = false;

        if (collapseHUD != null)
            collapseHUD.SetActive(false);

        if (Game_Manager.instance != null)
            Game_Manager.instance.ChangeGameState(
                Game_Manager.GameState.Exploring
            );
    }
}
