// ============================================================================
// SimpleHud.cs
// Screen text that any system can use:
//
//   SimpleHud.SetLine("sick", "SICK 12 s");          // a status line, top right under the clock (null removes it)
//   SimpleHud.Toast("+3.0 L banked");                    // a short message at the top for a few seconds (max 5 at once)
//   SimpleHud.Banner("THUNDERSTORM", "Rain for 60 s");   // a big message in the middle for a few seconds
//   SimpleHud.Radio("PAGASA has issued ...", 6f);        // the radio card (stone card at the top)
//   SimpleHud.ShowPanel("THE DROUGHT WINS", "Day 14", ("Restart", Restart));  // full-screen panel with buttons
//   SimpleHud.SetFade(1f);                               // black screen (0 = none)
//
// THE LOOK IS A PREFAB WITH ITS OWN CANVAS: Assets/Resources/UI/SimpleHud.prefab (see
// SimpleHudView.cs; Tools > The Last Drop > Build UI Prefabs makes it). If it's missing, a
// plain built-in look is used. A copy is made the first time anything is shown, in whatever
// scene is open, and goes away with that scene. Timers use real time, so messages still disappear while the game is
// frozen (time scale 0). The panel's buttons need an EventSystem in the scene (the Build
// Playtest Scene menu adds one). Don't call these from OnDestroy: during a scene change that
// would make a new canvas in the scene being closed. (Clearing a line with SetLine(key, null)
// is safe anywhere: it never makes a canvas.)
// A status line can be drawn somewhere else in the HUD with HudLineText (the "stage" line sits on
// the clock's plank). The radio card's timer only runs while the player can see it (not while the
// game is frozen, How to Play is up or a full-screen panel is showing).
// Status line keys used so far: drinking, jugs, sick (SurvivalDirector), lockdown, stage, event, ... (the team HUD shows HYD, canteen, Raw Basin and Clean Storage)
// (SurvivalDirector), stage (StageManager), lockdown (NightLockdown), plus the other batches'.
//
// DESIGN RULES: none of its own. The artists can replace any of this later.
//
// HOW TO USE: call the static methods above from any script.
// HOW TO EDIT: everything you see is in the prefab. To move things on screen in a game scene,
// run Salok > UI > Put HUD text in scene: the prefab is placed in the hierarchy (still linked)
// and the game uses that copy, so its "Status" lines (top right, in the "Status Box"), toasts and banner can be
// moved there with the Rect Tool. Apply the overrides to the prefab to move them everywhere.
// ============================================================================

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Simple text HUD: status lines, toasts, banners, a full-screen panel and a fade.</summary>
    public class SimpleHud : MonoBehaviour
    {
        /// <summary>Where the default prefab lives inside a Resources folder.</summary>
        public const string PrefabResource = "UI/SimpleHud";

        static SimpleHud instance;

        const int NormalSortOrder = 9;
        const int PanelSortOrder = 90;
        Canvas canvas;
        SimpleHudView view;

        readonly List<KeyValuePair<string, string>> lines = new List<KeyValuePair<string, string>>();
        readonly List<(string text, float until)> toastList = new List<(string, float)>();
        float bannerUntil;
        float radioLeft;
        const float RadioFade = 0.35f;
        bool statusDirty;

        // ------------------------------------------------------------------ Static API

        /// <summary>Sets a status line (top right, in a dark box). Null or empty removes it. Lines keep the order they were first added.</summary>
        public static void SetLine(string key, string text)
        {
            // Removing a line never needs a canvas. Without this, a script clearing its line in
            // OnDestroy while the scene closes made a NEW SimpleHud after the old one was
            // gone ("Some objects were not cleaned up when closing the scene").
            // A HudLineText placed in the HUD for this key draws it instead of the status box.
            if (HudLineText.TryShow(key, text)) { RemoveLine(key); return; }
            if (string.IsNullOrEmpty(text) && instance == null) return;
            var h = Get();
            int i = h.lines.FindIndex(kv => kv.Key == key);
            if (string.IsNullOrEmpty(text))
            {
                if (i >= 0) { h.lines.RemoveAt(i); h.statusDirty = true; }
                return;
            }
            if (i >= 0)
            {
                if (h.lines[i].Value == text) return;
                h.lines[i] = new KeyValuePair<string, string>(key, text);
            }
            else h.lines.Add(new KeyValuePair<string, string>(key, text));
            h.statusDirty = true;
        }

        /// <summary>Takes a status line out of the status box and returns its text ("" if none). HudLineText uses it.</summary>
        public static string TakeLine(string key)
        {
            if (instance == null) return "";
            int i = instance.lines.FindIndex(kv => kv.Key == key);
            if (i < 0) return "";
            string text = instance.lines[i].Value;
            instance.lines.RemoveAt(i);
            instance.statusDirty = true;
            return text;
        }

        static void RemoveLine(string key)
        {
            if (instance == null) return;
            int i = instance.lines.FindIndex(kv => kv.Key == key);
            if (i >= 0) { instance.lines.RemoveAt(i); instance.statusDirty = true; }
        }

        /// <summary>
        /// A radio message on the radio card (stone card at the top) for <paramref name="seconds"/> of play
        /// (the time doesn't run while the game is frozen, e.g. under How to Play). Without a card: a toast.
        /// </summary>
        public static void Radio(string text, float seconds = 6f)
        {
            var h = Get();
            var v = h.view;
            if (v.radioCard == null || v.radioText == null) { Toast("RADIO: " + text, seconds); return; }
            v.radioText.text = text;
            v.radioCard.gameObject.SetActive(true);
            h.radioLeft = seconds;
        }

        /// <summary>A short message at the top of the screen for a few seconds.</summary>
        public static void Toast(string text, float seconds = 3f)
        {
            var h = Get();
            h.toastList.Add((text, Time.unscaledTime + seconds));
            if (h.toastList.Count > 5) h.toastList.RemoveAt(0);
            h.RefreshToasts();
        }

        /// <summary>A big message in the middle of the screen (event names, stage changes).</summary>
        public static void Banner(string title, string line, float seconds = 4f)
        {
            var h = Get();
            var v = h.view;
            if (v.bannerTitle != null) { v.bannerTitle.text = title; v.bannerTitle.gameObject.SetActive(true); }
            if (v.bannerLine != null) { v.bannerLine.text = line; v.bannerLine.gameObject.SetActive(true); }
            h.bannerUntil = Time.unscaledTime + seconds;
        }

        /// <summary>True if the banner title's font can draw <paramref name="c"/> (used to avoid missing-glyph boxes).</summary>
        public static bool BannerHasCharacter(char c)
        {
            var t = Get().view.bannerTitle;
            return t != null && t.font != null && t.font.HasCharacter(c, true);
        }

        /// <summary>Black over the whole screen: 0 = clear, 1 = black.</summary>
        public static void SetFade(float alpha)
        {
            var h = Get();
            var f = h.view.fade;
            if (f == null) return;
            var c = f.color;
            f.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
            f.gameObject.SetActive(alpha > 0.001f);
        }

        /// <summary>A full-screen panel with a title, body text and buttons. Frees the cursor.</summary>
        public static void ShowPanel(string title, string body, params (string label, Action onClick)[] buttons)
        {
            var h = Get();
            var v = h.view;
            if (v.panelTitle != null) v.panelTitle.text = title;
            if (v.panelBody != null) v.panelBody.text = body;
            var area = v.panelButtons != null ? v.panelButtons : (v.panel != null ? (RectTransform)v.panel.transform : (RectTransform)h.transform);
            foreach (Transform c in area)
                if (v.panelButtonTemplate == null || c != v.panelButtonTemplate.transform) Destroy(c.gameObject);

            float step = 80f;
            if (v.panelButtonTemplate != null) step = ((RectTransform)v.panelButtonTemplate.transform).sizeDelta.y + 16f;
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                Button btn;
                if (v.panelButtonTemplate != null)
                {
                    btn = Instantiate(v.panelButtonTemplate, area, false);
                    btn.gameObject.SetActive(true);
                    var label = btn.GetComponentInChildren<TMP_Text>(true);
                    if (label != null) label.text = b.label;
                }
                else btn = UiBuilder.Button("Button", area, b.label, 30f, new Color(0.16f, 0.16f, 0.16f), Color.white, null);
                btn.gameObject.name = "Button_" + b.label;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => b.onClick?.Invoke());
                var rt = (RectTransform)btn.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -i * step);
            }
            if (v.panel != null) v.panel.SetActive(true);
            if (h.canvas != null) h.canvas.sortingOrder = PanelSortOrder;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Hides the full-screen panel.</summary>
        public static void HidePanel()
        {
            if (instance == null) return;
            if (instance.view.panel != null) instance.view.panel.SetActive(false);
            if (instance.canvas != null) instance.canvas.sortingOrder = NormalSortOrder;
        }

        /// <summary>True while the full-screen panel is up.</summary>
        public static bool PanelOpen => instance != null && instance.view.panel != null && instance.view.panel.activeSelf;

        // ------------------------------------------------------------------ Setup

        static SimpleHud Get()
        {
            if (instance != null) return instance;
            // The prefab (Resources/UI/SimpleHud), else the built-in look. Sort order 9: under the
            // team's HUD and pause menu (10); the panel lifts it to 90 so it covers everything.
            // A copy already placed in the scene (Salok > UI > Put HUD text in scene) is used as it is,
            // so its placement can be edited in the editor. Otherwise the prefab is copied in.
            var v = FindAnyObjectByType<SimpleHudView>(FindObjectsInactive.Include);
            if (v != null && !v.gameObject.activeInHierarchy)
            {
                var root = v.transform.root.gameObject;
                if (!root.activeSelf) root.SetActive(true);
                if (!v.gameObject.activeSelf) v.gameObject.SetActive(true);
            }
            if (v == null) v = UiBuilder.InstantiateView<SimpleHudView>(null, PrefabResource, null);
            if (v == null) v = SimpleHudView.BuildDefault(null);
            instance = v.gameObject.AddComponent<SimpleHud>();
            instance.view = v;
            instance.canvas = v.GetComponentInParent<Canvas>();
            if (instance.canvas != null) instance.canvas.sortingOrder = NormalSortOrder;

            if (v.bannerTitle != null) v.bannerTitle.gameObject.SetActive(false);
            if (v.bannerLine != null) v.bannerLine.gameObject.SetActive(false);
            if (v.fade != null) v.fade.gameObject.SetActive(false);
            if (v.panel != null) v.panel.SetActive(false);
            if (v.panelButtonTemplate != null) v.panelButtonTemplate.gameObject.SetActive(false);
            if (v.statusText != null) v.statusText.text = "";
            ShowStatusBox(v, false);
            if (v.toastsText != null) v.toastsText.text = "";
            if (v.radioCard != null) { v.radioCard.alpha = 0f; v.radioCard.gameObject.SetActive(false); }
            return instance;
        }

        /// <summary>Name of the optional dark box around the status lines (the Status text's parent in the prefab).</summary>
        public const string StatusBoxName = "Status Box";

        // The status lines sit in a dark box (top right, under the clock). It hides when there are no lines.
        static void ShowStatusBox(SimpleHudView v, bool show)
        {
            if (v == null || v.statusText == null) return;
            var box = v.statusText.transform.parent;
            if (box != null && box.name == StatusBoxName && box.gameObject.activeSelf != show) box.gameObject.SetActive(show);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ------------------------------------------------------------------ Every frame

        void Update()
        {
            if (statusDirty)
            {
                statusDirty = false;
                var sb = new System.Text.StringBuilder();
                foreach (var kv in lines) sb.AppendLine(kv.Value);
                if (view.statusText != null)
                {
                    var text = sb.ToString().TrimEnd('\n', '\r');
                    view.statusText.text = text;
                    ShowStatusBox(view, text.Length > 0);
                }
            }

            // Toasts can have different lengths, so check them all (not only the oldest).
            if (toastList.Count > 0 && toastList.RemoveAll(t => t.until <= Time.unscaledTime) > 0)
                RefreshToasts();

            UpdateRadio();

            if (view.bannerTitle != null && view.bannerTitle.gameObject.activeSelf && Time.unscaledTime >= bannerUntil)
            {
                view.bannerTitle.gameObject.SetActive(false);
                if (view.bannerLine != null) view.bannerLine.gameObject.SetActive(false);
            }
        }

        // The radio card fades in, stays for its seconds of play (paused while the game is frozen), fades out.
        void UpdateRadio()
        {
            var card = view.radioCard;
            if (card == null || !card.gameObject.activeSelf) return;
            // Clamped: the first frame after a scene load can report seconds of load time.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            // The time only runs while the player can read it: not while the game is frozen, How to Play
            // is up or a full-screen panel is showing (the day-1 message used to run out under How to Play).
            bool covered = Time.timeScale <= 0f || HowToPlay.IsOpen || PanelOpen || MainMenu.IsFrozen;
            if (!covered) radioLeft -= dt;
            float target = radioLeft > 0f ? 1f : 0f;
            card.alpha = Mathf.MoveTowards(card.alpha, target, dt / RadioFade);
            if (target == 0f && card.alpha <= 0f) card.gameObject.SetActive(false);
        }

        void RefreshToasts()
        {
            if (view.toastsText == null) return;
            var sb = new System.Text.StringBuilder();
            foreach (var t in toastList) sb.AppendLine(t.text);
            view.toastsText.text = sb.ToString();
        }
    }
}
