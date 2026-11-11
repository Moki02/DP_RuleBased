// ============================================================================
// PlayerDeath_Manager.cs
// The player's collapse animation and waking up at the hub.
//
// In the maze game, CollapseManager runs the whole collapse (R11.1 to R11.4: fade, water
// costs, game over check) and calls ReviveAt when the player wakes. In old scenes with no
// CollapseManager this script works as before: the Collapse HUD shows, and its button
// calls ReviveCharacter.
//
// HOW TO USE: on the Player prefab. "Respawn Point" is only needed in old scenes.
// ============================================================================

using UnityEngine;
using TheLastDrop;

public class PlayerDeath_Manager : MonoBehaviour
{
    [Header("Respawn Settings")]
    public Transform respawnPoint;
    public float reviveHydration = 50f;

    [Header("Collapse UI")]
    public GameObject collapseHUD;

    [Header("Animation")]
    public string collapseAnimation = "Collapse";

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

        Game_Manager.instance.ChangeGameState(
            Game_Manager.GameState.Collapsed
        );
    }

    // Old scenes: the Collapse HUD's button calls this.
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

        Game_Manager.instance.ChangeGameState(
            Game_Manager.GameState.Exploring
        );
    }
}
