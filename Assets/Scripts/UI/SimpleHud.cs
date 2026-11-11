// ============================================================================
// SimpleHud.cs
// Plain TextMeshPro screen text that any system can use, with no prefab and no art:
//
//   SimpleHud.SetLine("canteen", "Canteen 1.5 / 2 L");   // a status line on the left (null removes it)
//   SimpleHud.Toast("+3.0 L banked");                    // a short message at the top, fades out
//   SimpleHud.Banner("THUNDERSTORM", "Rain for 60 s");   // a big message in the middle for a few seconds
//   SimpleHud.ShowPanel("THE DROUGHT WINS", "Day 14", ("Restart", Restart));  // full-screen panel with buttons
//   SimpleHud.SetFade(1f);                               // black screen (0 = none)
//
// The canvas is made the first time anything is shown, in whatever scene is open.
// Timers use real time, so messages still fade while the game is paused.
//
// DESIGN RULES: none of its own. The artists can replace any of this later.
//
// HOW TO USE: call the static methods above from any script.
// HOW TO EDIT: sizes and colours are the constants at the top of the class.
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
        const float LineSize = 26f;
        const float ToastSize = 30f;
        static readonly Color TextColor = new Color(0.92f, 0.95f, 1f);
        static readonly Color ToastColor = new Color(0.55f, 0.85f, 1f);

        static SimpleHud instance;

        TextMeshProUGUI status, toasts, bannerTitle, bannerLine;
        Image fade;
        RectTransform panel;
        TextMeshProUGUI panelTitle, panelBody;
        RectTransform panelButtons;

        readonly List<KeyValuePair<string, string>> lines = new List<KeyValuePair<string, string>>();
        readonly List<(string text, float until)> toastList = new List<(string, float)>();
        float bannerUntil;
        bool statusDirty;

        // ------------------------------------------------------------------ Static API

        /// <summary>Sets a status line (left side). Null or empty removes it. Lines keep the order they were first added.</summary>
        public static void SetLine(string key, string text)
        {
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
            h.bannerTitle.text = title;
            h.bannerLine.text = line;
            h.bannerTitle.gameObject.SetActive(true);
            h.bannerLine.gameObject.SetActive(true);
            h.bannerUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Black over the whole screen: 0 = clear, 1 = black.</summary>
        public static void SetFade(float alpha)
        {
            var h = Get();
            h.fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
            h.fade.gameObject.SetActive(alpha > 0.001f);
        }

        /// <summary>A full-screen panel with a title, body text and buttons. Frees the cursor.</summary>
        public static void ShowPanel(string title, string body, params (string label, Action onClick)[] buttons)
        {
            var h = Get();
            h.panelTitle.text = title;
            h.panelBody.text = body;
            foreach (Transform c in h.panelButtons) Destroy(c.gameObject);
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                var btn = UiBuilder.Button("Button_" + b.label, h.panelButtons, b.label, 30f,
                    new Color(0.15f, 0.35f, 0.55f), Color.white, () => b.onClick?.Invoke());
                UiBuilder.Place((RectTransform)btn.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -i * 80f), new Vector2(360f, 64f));
            }
            h.panel.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Hides the full-screen panel.</summary>
        public static void HidePanel()
        {
            if (instance != null) instance.panel.gameObject.SetActive(false);
        }

        /// <summary>True while the full-screen panel is up.</summary>
        public static bool PanelOpen => instance != null && instance.panel.gameObject.activeSelf;

        // ------------------------------------------------------------------ Setup

        static SimpleHud Get()
        {
            if (instance != null) return instance;
            var canvas = UiBuilder.CreateCanvas("SimpleHudCanvas", null, 80);
            instance = canvas.gameObject.AddComponent<SimpleHud>();
            instance.Build(canvas.transform);
            return instance;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Build(Transform root)
        {
            // Status lines: left side, a little below the top.
            status = UiBuilder.Text("Status", root, "", LineSize, TextColor, TextAlignmentOptions.TopLeft);
            UiBuilder.Place(status.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -140f), new Vector2(620f, 520f));
            status.textWrappingMode = TextWrappingModes.NoWrap;
            status.outlineWidth = 0.2f;
            status.outlineColor = Color.black;

            // Toasts: top centre.
            toasts = UiBuilder.Text("Toasts", root, "", ToastSize, ToastColor, TextAlignmentOptions.Top);
            UiBuilder.Place(toasts.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1200f, 260f));
            toasts.outlineWidth = 0.2f;
            toasts.outlineColor = Color.black;

            // Banner: middle.
            bannerTitle = UiBuilder.Text("BannerTitle", root, "", 64f, Color.white, TextAlignmentOptions.Center);
            UiBuilder.Place(bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1400f, 90f));
            bannerTitle.fontStyle = FontStyles.Bold;
            bannerTitle.outlineWidth = 0.25f;
            bannerTitle.outlineColor = Color.black;
            bannerLine = UiBuilder.Text("BannerLine", root, "", 32f, Color.white, TextAlignmentOptions.Center);
            UiBuilder.Place(bannerLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1400f, 60f));
            bannerLine.outlineWidth = 0.2f;
            bannerLine.outlineColor = Color.black;
            bannerTitle.gameObject.SetActive(false);
            bannerLine.gameObject.SetActive(false);

            // Fade: covers everything below the panel.
            fade = UiBuilder.Image("Fade", root, Color.clear);
            fade.gameObject.SetActive(false);

            // Full-screen panel.
            var bg = UiBuilder.Image("Panel", root, new Color(0f, 0f, 0f, 0.92f), blocksClicks: true);
            panel = bg.rectTransform;
            panelTitle = UiBuilder.Text("Title", panel, "", 72f, Color.white, TextAlignmentOptions.Center);
            UiBuilder.Place(panelTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1600f, 110f));
            panelTitle.fontStyle = FontStyles.Bold;
            panelBody = UiBuilder.Text("Body", panel, "", 32f, TextColor, TextAlignmentOptions.Top);
            UiBuilder.Place(panelBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 230f), new Vector2(1200f, 420f));
            panelButtons = UiBuilder.Rect("Buttons", panel);
            UiBuilder.Place(panelButtons, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(400f, 300f));
            panel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Every frame

        void Update()
        {
            if (statusDirty)
            {
                statusDirty = false;
                var sb = new System.Text.StringBuilder();
                foreach (var kv in lines) sb.AppendLine(kv.Value);
                status.text = sb.ToString();
            }

            if (toastList.Count > 0 && toastList[0].until <= Time.unscaledTime)
            {
                toastList.RemoveAll(t => t.until <= Time.unscaledTime);
                RefreshToasts();
            }

            if (bannerTitle.gameObject.activeSelf && Time.unscaledTime >= bannerUntil)
            {
                bannerTitle.gameObject.SetActive(false);
                bannerLine.gameObject.SetActive(false);
            }
        }

        void RefreshToasts()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var t in toastList) sb.AppendLine(t.text);
            toasts.text = sb.ToString();
        }
    }
}
