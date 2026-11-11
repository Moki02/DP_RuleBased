// ============================================================================
// RoomWaterSource.cs
// The cistern or sealed jug (cache) in the middle of a room. Hold E next to it to fill the
// canteen at 1 L per second. A cache pours only what fits; the rest stays for a later
// trip (R8.5). A cistern refills at dawn (R8.14). RoomContent keeps the litres and saves them.
//
// DESIGN RULES: R8.5 (pour what fits, the rest stays), R8.14 (cistern), R8.15 (cache),
// R1.3 (every litre comes from somewhere: counted with RunStats.AddCollected).
//
// HOW TO USE: RoomContent puts one on each cistern / cache when the game starts. Nothing to set.
// HOW TO EDIT: the fill rate is "Room Fill Rate" in GameBalance. Prompt texts are below.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>A room's cistern or cache: hold E to fill the canteen from it.</summary>
    public class RoomWaterSource : Interactable_Manager
    {
        [Tooltip("The RoomContent that owns this room's water.")]
        public RoomContent owner;
        [Tooltip("This room's number (MazeGrid.Rooms index).")]
        public int roomIndex;
        [Tooltip("The player can use this from anywhere within this many metres (the interaction box is small).")]
        public float proximityRange = 2f;

        [Header("Text (what the player reads)")]
        [Tooltip("{0} = litres left.")]
        public string fillPrompt = "Hold [E] to fill canteen  ({0:0.0} L left)";
        public string canteenFullPrompt = "Canteen full";

        void Awake()
        {
            interactionType = InteractionType.Hold;
        }

        void Update()
        {
            FollowPlayerWhenClose();
        }

        /// <summary>True if the player is close, not collapsed and the room still has water.</summary>
        public override bool CanInteract(Player_Manager player)
        {
            if (player == null || owner == null) return false;
            if (player.playerHydration_Manager != null && player.playerHydration_Manager.IsCollapsed()) return false;
            if (!IsPlayerClose(player)) return false;
            return owner.LitresIn(roomIndex) > 0.0001f;
        }

        /// <summary>The prompt under the crosshair.</summary>
        public override string GetInteractionText()
        {
            var res = GameResources_Manager.instance;
            if (res != null && !res.infiniteCanteen && res.canteen >= res.canteen_Max - 0.0001f) return canteenFullPrompt;
            return string.Format(fillPrompt, owner != null ? owner.LitresIn(roomIndex) : 0f);
        }

        /// <summary>Called every frame while E is held.</summary>
        public override void Interact(float deltaTime)
        {
            if (owner != null && deltaTime > 0f) owner.FillCanteenFrom(roomIndex, deltaTime);
        }

        // ------------------------------------------------------------------ Reach (same trick as LockBasin_Manager)

        bool IsPlayerClose(Player_Manager player)
        {
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude <= proximityRange;
        }

        // The player's interaction box only reaches about 1 m in front of them. While the player
        // is close enough, the interaction point jumps into the middle of that box, so the
        // player can use the tank from anywhere within Proximity Range.
        void FollowPlayerWhenClose()
        {
            if (interactionPoint == null)
            {
                interactionPoint = new GameObject("InteractionPoint").transform;
                interactionPoint.SetParent(transform, false);
            }
            var player = Player_Manager.instance;
            var reach = PlayerInteraction_Manager.instance != null ? PlayerInteraction_Manager.instance.interaction_Hitbox : null;
            if (player != null && reach != null && IsPlayerClose(player))
                interactionPoint.position = reach.transform.TransformPoint(reach.center);
            else
                interactionPoint.localPosition = Vector3.zero;
        }
    }
}
