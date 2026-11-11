// ============================================================================
// PlayerInteraction_Manager.cs
// Finds the closest usable interactable (pump, tap, lock basin, valve, note...) inside the
// box in front of the player, shows its prompt ("Hold [E] to Drink"), and runs it:
//   - Press type: once, in the frame E is pressed;
//   - Hold type: every frame while E is held; StopInteraction is called when E is released,
//     the player walks away, another one becomes closer, or the player loses control.
// Nothing works while the game is paused, in a menu or minigame, or the player is collapsed.
//
// DESIGN RULES: Controls table (E), R8.9 (hold E to pour).
//
// HOW TO USE: on the Player prefab. Drag in the "Interaction Hitbox" (a BoxCollider child in
// front of the player; only its size and position are used) and the HUD's "Interaction Text".
// HOW TO EDIT: change the reach by resizing the hitbox box.
// ============================================================================

using UnityEngine;
using TMPro;

/// <summary>Picks the interactable in reach, shows its prompt and runs it when E is pressed or held.</summary>
public class PlayerInteraction_Manager : MonoBehaviour
{
    /// <summary>The player's interaction script in this scene.</summary>
    public static PlayerInteraction_Manager instance;

    [Header("Interaction Settings")]
    [Tooltip("Box in front of the player. An interactable is in reach when its Interaction Point is inside this box.")]
    public BoxCollider interaction_Hitbox;

    [Header("Interaction UI")]
    [Tooltip("HUD text that shows the prompt, e.g. \"Hold [E] to Drink\". Optional.")]
    public TMP_Text interactionText;

    [Tooltip("Filled in automatically.")]
    public Player_Manager player;
    private PlayerInput_Manager playerInput;

    private Interactable_Manager currentInteractable; // the closest one in reach this frame (its prompt shows)
    private Interactable_Manager activeInteractable;  // the Hold one being used right now (needs StopInteraction later)

    private void Awake()
    {
        instance = this;

        player = GetComponent<Player_Manager>();
    }

    private void Start()
    {
        playerInput = player.playerInput_Manager;

        if (interactionText != null)
            interactionText.text = "";
    }

    private void Update()
    {
        if (playerInput == null ||
            interaction_Hitbox == null ||
            Game_Manager.instance == null)
        {
            StopCurrentInteraction();
            ClearInteractionText();
            return;
        }

        if (!Game_Manager.instance.CanControlPlayer() ||
            player.playerHydration_Manager.IsCollapsed())
        {
            StopCurrentInteraction();
            currentInteractable = null;
            ClearInteractionText();
            return;
        }

        FindInteractable();

        // A different one is now closest: end the hold on the old one first.
        if (activeInteractable != null &&
            activeInteractable != currentInteractable)
        {
            StopCurrentInteraction();
        }

        if (currentInteractable != null)
        {
            if (currentInteractable.interactionType ==
                Interactable_Manager.InteractionType.Press)
            {
                if (playerInput.interactPressed)
                {
                    currentInteractable.Interact(0f);
                }
            }
            else
            {
                if (playerInput.interactInput)
                {
                    activeInteractable = currentInteractable;
                    activeInteractable.Interact(Time.deltaTime);
                }
                else
                {
                    StopCurrentInteraction();
                }
            }
        }
        else
        {
            StopCurrentInteraction();
        }

        UpdateInteractionUI();
    }

    // Closest interactable that can be used now and whose point is inside the hitbox.
    private void FindInteractable()
    {
        currentInteractable = null;

        float closestDistance = float.MaxValue;

        foreach (Interactable_Manager interactable in
                 Interactable_Manager.allInteractables)
        {
            if (interactable == null ||
                !interactable.CanInteract(player))
                continue;

            Vector3 interactionPosition =
                interactable.GetInteractionPosition();

            if (!IsInsideHitbox(interactionPosition))
                continue;

            float distance =
                (interactionPosition -
                 interaction_Hitbox.transform.position).sqrMagnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                currentInteractable = interactable;
            }
        }
    }

    // Works for a rotated box: the point is turned into the box's own local space first.
    private bool IsInsideHitbox(Vector3 worldPosition)
    {
        Vector3 localPosition =
            interaction_Hitbox.transform.InverseTransformPoint(worldPosition);

        localPosition -= interaction_Hitbox.center;

        Vector3 halfSize = interaction_Hitbox.size * 0.5f;

        return Mathf.Abs(localPosition.x) <= halfSize.x &&
               Mathf.Abs(localPosition.y) <= halfSize.y &&
               Mathf.Abs(localPosition.z) <= halfSize.z;
    }

    private void StopCurrentInteraction()
    {
        if (activeInteractable != null)
        {
            activeInteractable.StopInteraction();
            activeInteractable = null;
        }
    }

    private void UpdateInteractionUI()
    {
        if (interactionText == null)
            return;

        if (currentInteractable != null)
        {
            interactionText.text =
                currentInteractable.GetInteractionText();
        }
        else
        {
            ClearInteractionText();
        }
    }

    private void ClearInteractionText()
    {
        if (interactionText != null)
            interactionText.text = "";
    }

    // Dev Note: Unity calls this when the player (or this script) is switched off. Nothing switches
    // it off yet; it's here so a hold (pumping, pouring) is ended cleanly if that ever happens.
    private void OnDisable()
    {
        StopCurrentInteraction();
        ClearInteractionText();
    }
}
