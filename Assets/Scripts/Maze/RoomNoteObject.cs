// ============================================================================
// RoomNoteObject.cs
// The survivor note lying in a room (R8.7). Press E next to it to read it. The first read
// also triggers the note's payload (R8.17); reading it again just shows the text.
//
// DESIGN RULES: R8.7 (one note per room, panel doesn't pause the clock), R8.17 (payload).
//
// HOW TO USE: RoomContent puts one in every room when the game starts. Nothing to set.
// HOW TO EDIT: the note texts are on RoomContent (Notes). Prompt texts are below.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>A room's survivor note: press E to read.</summary>
    public class RoomNoteObject : Interactable_Manager
    {
        [Tooltip("The RoomContent that owns this room's note.")]
        public RoomContent owner;
        [Tooltip("This room's number (MazeGrid.Rooms index).")]
        public int roomIndex;
        [Tooltip("The player can read it from anywhere within this many metres (the interaction box is small).")]
        public float proximityRange = 2f;

        [Header("Text (what the player reads)")]
        [Tooltip("Prompt for a note not read yet.")]
        public string readPrompt = "Press [E] to read the note";
        [Tooltip("Prompt for a note already read (no payload the second time).")]
        public string rereadPrompt = "Press [E] to read the note again";

        void Awake()
        {
            interactionType = InteractionType.Press;
        }

        void Update()
        {
            FollowPlayerWhenClose();
        }

        /// <summary>True if the player is close, free to act and didn't just close a note (so E doesn't reopen it).</summary>
        public override bool CanInteract(Player_Manager player)
        {
            if (player == null || owner == null) return false;
            if (player.playerHydration_Manager != null && player.playerHydration_Manager.IsCollapsed()) return false;
            if (Game_Manager.instance != null && !Game_Manager.instance.CanControlPlayer()) return false;
            // The E that closed the note this frame (or last frame) must not open it again.
            if (Time.frameCount <= NotePanel.ClosedFrame + 1) return false;
            return IsPlayerClose(player);
        }

        /// <summary>The prompt under the crosshair.</summary>
        public override string GetInteractionText() =>
            owner != null && owner.IsNoteRead(roomIndex) ? rereadPrompt : readPrompt;

        /// <summary>A Press interaction: called once on the frame E goes down.</summary>
        public override void Interact(float deltaTime)
        {
            if (owner != null) owner.ReadNote(roomIndex);
        }

        // ------------------------------------------------------------------ Reach (same trick as LockBasin_Manager)

        bool IsPlayerClose(Player_Manager player)
        {
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude <= proximityRange;
        }

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
