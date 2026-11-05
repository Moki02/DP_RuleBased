
using UnityEngine;

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


        if (collapseHUD != null)
            collapseHUD.SetActive(true);


        Game_Manager.instance.ChangeGameState(
            Game_Manager.GameState.Collapsed
        );
    }

    public void ReviveCharacter()
    {
        if (!isDead || respawnPoint == null)
            return;

        player.playerHydration_Manager.ResetAfterRevive(
            reviveHydration
        );


        CharacterController controller = player.characterController;

        if (controller != null)
            controller.enabled = false;

        transform.SetPositionAndRotation(
            respawnPoint.position,
            respawnPoint.rotation
        );

        if (controller != null)
            controller.enabled = true;


        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.updateMode = originalUpdateMode;
        }

        isDead = false;


        if (collapseHUD != null)
            collapseHUD.SetActive(false);

 
        Game_Manager.instance.ChangeGameState(
            Game_Manager.GameState.Exploring
        );
    }
}
