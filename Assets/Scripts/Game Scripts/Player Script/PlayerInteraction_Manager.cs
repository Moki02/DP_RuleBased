
using UnityEngine;
using TMPro;

public class PlayerInteraction_Manager : MonoBehaviour
{
    public static PlayerInteraction_Manager instance;

    [Header("Interaction Settings")]
    public BoxCollider interaction_Hitbox;

    [Header("Interaction UI")]
    public TMP_Text interactionText;

    public Player_Manager player;
    private PlayerInput_Manager playerInput;

    private Interactable_Manager currentInteractable;
    private Interactable_Manager activeInteractable;

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

    // Dev Note: Still unused but already writing this function just incase of future purposes
    private void OnDisable()
    {
        StopCurrentInteraction();
        ClearInteractionText();
    }
}
