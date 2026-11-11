// ============================================================================
// SimpleHud.cs
// Screen text that any system can use:
//
//   SimpleHud.SetLine("sick", "SICK 12 s");          // a status line, bottom right (null removes it)
//   SimpleHud.Toast("+3.0 L banked");                    // a short message at the top for a few seconds (max 5 at once)
//   SimpleHud.Banner("THUNDERSTORM", "Rain for 60 s");   // a big message in the middle for a few seconds
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
// Status line keys used so far: drinking, jugs, sick (SurvivalDirector), lockdown, stage, event, ... (the team HUD shows HYD, canteen, Raw Basin and Clean Storage)
// (SurvivalDirector), stage (StageManager), lockdown (NightLockdown), plus the other batches'.
//
// DESIGN RULES: none of its own. The artists can replace any of this later.
//
// HOW TO USE: call the static methods above from any script.
// HOW TO EDIT: everything you see is in the prefab.
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
        bool statusDirty;

        // ------------------------------------------------------------------ Static API

        /// <summary>Sets a status line (bottom right). Null or empty removes it. Lines keep the order they were first added.</summary>
        public static void SetLine(string key, string text)
        {
            // Removing a line never needs a canvas. Without this, a script clearing its line in
            // OnDestroy while the scene closes made a NEW SimpleHud after the old one was
            // gone ("Some objects were not cleaned up when closing the scene").
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
            var v = UiBuilder.InstantiateView<SimpleHudView>(null, PrefabResource, null);
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
            if (v.toastsText != null) v.toastsText.text = "";
            return instance;
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
                if (view.statusText != null) view.statusText.text = sb.ToString();
            }

            // Toasts can have different lengths, so check them all (not only the oldest).
            if (toastList.Count > 0 && toastList.RemoveAll(t => t.until <= Time.unscaledTime) > 0)
                RefreshToasts();

            if (view.bannerTitle != null && view.bannerTitle.gameObject.activeSelf && Time.unscaledTime >= bannerUntil)
            {
                view.bannerTitle.gameObject.SetActive(false);
                if (view.bannerLine != null) view.bannerLine.gameObject.SetActive(false);
            }
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
