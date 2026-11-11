// ============================================================================
// NotePanel.cs
// A plain text panel for reading a survivor note (R8.7) and for the journal (the list of
// notes read so far). No art: a dark box with TextMeshPro text and a Close button.
//
// Reading a note does NOT pause the clock (R8.7): the game goes into the Menu state
// (MenuState.Enter), so the player can't move or look around and the cursor is free, but
// time, hydration and the night keep going. Closing it (E, Esc or Close) puts the game back
// to Exploring, which locks and hides the cursor again.
// Keys: E or Esc close a note. Esc can't also pause the game, because the team's pause key
// (PlayerInput_Manager) is handled before this script's Update and ignores the Menu state.
// Tab / B / M can't open the Tab menu or the map over a note: MenuState.Enter() says no
// while the game is already in the Menu state.
// The journal opens on top of the map screen (J or the map's Journal button). The map
// already holds the Menu state, so the journal doesn't change it; it closes with J, its
// Close button, or when the map closes.
//
// DESIGN RULES: R8.7 (notes and journal), R9.1 (the world doesn't pause).
//
// HOW TO USE: RoomContent adds this and calls OpenNote / ToggleJournal.
// HOW TO EDIT: THE LOOK IS A PREFAB WITH ITS OWN CANVAS: Assets/Resources/UI/NotePanel.prefab
// (see NotePanelView.cs; Tools > The Last Drop > Build UI Prefabs makes it). If it's missing,
// a plain black-and-white box is built in code.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>The note and journal panel.</summary>
    public class NotePanel : MonoBehaviour
    {
        /// <summary>Where the default prefab lives inside a Resources folder.</summary>
        public const string PrefabResource = "UI/NotePanel";

        [Tooltip("The panel's look: a prefab with a Canvas and a NotePanelView on its root. Empty = Resources/UI/NotePanel.")]
        public GameObject notePrefab;

        /// <summary>The frame a note was last closed on. The note object waits a frame so the same E press doesn't reopen it.</summary>
        public static int ClosedFrame { get; private set; } = -10;

        enum Mode { Closed, Note, Journal }

        Mode mode = Mode.Closed;
        int openedFrame;
        GameObject root;
        TMP_Text titleText, bodyText, hintText;
        MapScreen map;

        /// <summary>True while a note is being read.</summary>
        public bool NoteOpen => mode == Mode.Note;

        /// <summary>True while the journal is up.</summary>
        public bool JournalOpen => mode == Mode.Journal;

        void Awake()
        {
            Build();
        }

        void Start()
        {
            map = FindAnyObjectByType<MapScreen>();
        }

        // ------------------------------------------------------------------ Open / close

        /// <summary>
        /// Shows a note (R8.7). Returns false if the player isn't free to read right now
        /// (a minigame, pause or another menu is up).
        /// </summary>
        public bool OpenNote(string title, string body)
        {
            if (mode == Mode.Note) return true;
            if (mode == Mode.Journal) Hide();
            if (!MenuState.Enter()) return false;
            Show(Mode.Note, title, body, "E / Esc   Close        (the clock keeps running)");
            return true;
        }

        /// <summary>Opens the journal over the map, or closes it if it's already up.</summary>
        public void ToggleJournal(string title, string body)
        {
            if (mode == Mode.Journal) { Hide(); return; }
            if (mode == Mode.Note) return;
            Show(Mode.Journal, title, body, "J   Close");
        }

        /// <summary>Closes whatever is showing. A note gives control back to the player.</summary>
        public void Close()
        {
            if (mode == Mode.Note)
            {
                Hide();
                ClosedFrame = Time.frameCount;
                MenuState.Exit();   // back to Exploring: cursor locked and hidden again
            }
            else if (mode == Mode.Journal) Hide();
        }

        void Show(Mode newMode, string title, string body, string hint)
        {
            mode = newMode;
            openedFrame = Time.frameCount;
            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = body;
            if (hintText != null) hintText.text = hint;
            root.SetActive(true);
        }

        void Hide()
        {
            mode = Mode.Closed;
            root.SetActive(false);
        }

        void Update()
        {
            if (mode == Mode.Closed) return;

            if (mode == Mode.Note)
            {
                // Something else took over (a collapse, the game over screen): just hide, don't touch the state.
                if (Game_Manager.instance != null && !MenuState.IsOpen) { Hide(); return; }

                var kb = Keyboard.current;
                // Ignore the frame it opened on: that E press opened it.
                if (kb != null && Time.frameCount > openedFrame &&
                    (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                    Close();
            }
            else if (mode == Mode.Journal)
            {
                // The journal belongs to the map screen: it goes when the map goes.
                if (map == null) map = FindAnyObjectByType<MapScreen>();
                if (map == null || !map.IsOpen) Hide();
            }
        }

        // ------------------------------------------------------------------ Building the UI

        void Build()
        {
            var view = UiBuilder.InstantiateView<NotePanelView>(notePrefab, PrefabResource, transform);
            if (view == null) view = NotePanelView.BuildDefault(transform);
            root = view.panel != null ? view.panel : view.gameObject;
            titleText = view.titleText;
            bodyText = view.bodyText;
            hintText = view.hintText;
            if (view.closeButton != null) view.closeButton.onClick.AddListener(Close);
            root.SetActive(false);
        }
    }
}
