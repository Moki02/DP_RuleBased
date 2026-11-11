// ============================================================================
// UiBuilder.cs
// Small helpers that make uGUI objects from code: a canvas, rectangles, images, text
// and a few simple shapes (arrow, diamond). The playtest screens (map, minigame panel,
// Build and Skills menu) are built with these, so they work before the artists' UI
// prefabs exist.
//
// DESIGN RULES: none of its own.
//
// HOW TO USE: call from a UI script, for example
//     var canvas = UiBuilder.CreateCanvas("MapCanvas", transform, 50);
//     var title  = UiBuilder.Text("Title", canvas.transform, "MAP", 40, Color.white);
//     UiBuilder.Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -36), new Vector2(800, 60));
// Buttons only react to clicks if the scene has an EventSystem (GameObject > UI > Event System).
// HOW TO EDIT: the reference resolution (1920 × 1080) is ReferenceWidth/Height below; it
// matches the artists' design canvas. Fonts: TextMeshPro's default font asset is used
// (Project Settings > TextMesh Pro > Settings > Default Font Asset).
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Helpers to build uGUI from code.</summary>
    public static class UiBuilder
    {
        /// <summary>Design width in pixels (the artists' canvas).</summary>
        public const float ReferenceWidth = 1920f;
        /// <summary>Design height in pixels (the artists' canvas).</summary>
        public const float ReferenceHeight = 1080f;

        static Sprite arrowSprite, diamondSprite, dropSprite;

        /// <summary>Turns "#1B1713" into a colour. Bad text gives magenta, so mistakes are easy to spot.</summary>
        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        /// <summary>Makes a screen-overlay canvas scaled from 1920 × 1080, drawn above canvases with a lower sort order.</summary>
        public static Canvas CreateCanvas(string name, Transform parent, int sortOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f; // halfway between fitting width and height
            return canvas;
        }

        /// <summary>
        /// Makes a copy of a UI prefab and returns its view script (the component holding the
        /// links to its texts, buttons ...). Uses <paramref name="assigned"/> if one was dragged
        /// in, otherwise the prefab at Resources/<paramref name="resourcePath"/>. Returns null if
        /// there is neither (the caller then builds the default look in code).
        /// </summary>
        public static T InstantiateView<T>(GameObject assigned, string resourcePath, Transform parent) where T : Component
        {
            var prefab = assigned != null ? assigned : Resources.Load<GameObject>(resourcePath);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            var view = go.GetComponentInChildren<T>(true);
            if (view == null)
            {
                Debug.LogWarning($"[UiBuilder] The prefab \"{prefab.name}\" has no {typeof(T).Name} on it. Using the built-in look instead.");
                Object.Destroy(go);
            }
            return view;
        }

        /// <summary>An empty rectangle that fills its parent.</summary>
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Fill(rt);
            return rt;
        }

        /// <summary>Makes a rectangle fill its parent, minus a margin on each side (pixels).</summary>
        public static void Fill(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>
        /// Places a rectangle by anchor (0-1 inside the parent), pivot, offset and size (pixels).
        /// Example: anchor (0, 1) and pivot (0, 1) = measured from the parent's top-left corner.
        /// </summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        /// <summary>A coloured rectangle that fills its parent. Clicks pass through unless <paramref name="blocksClicks"/>.</summary>
        public static Image Image(string name, Transform parent, Color color, bool blocksClicks = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = blocksClicks;
            return img;
        }

        /// <summary>A TextMeshPro text that fills its parent.</summary>
        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
                                           TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        /// <summary>
        /// A clickable button with a label, filling its parent (place it with Place afterwards).
        /// <paramref name="onClick"/> runs when it's clicked.
        /// </summary>
        public static Button Button(string name, Transform parent, string label, float fontSize,
                                    Color background, Color textColor, UnityEngine.Events.UnityAction onClick)
        {
            var img = Image(name, parent, background, blocksClicks: true);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            if (onClick != null) button.onClick.AddListener(onClick);
            var text = Text("Label", img.transform, label, fontSize, textColor, TextAlignmentOptions.Center);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        /// <summary>A white triangle pointing up (+Y). Tint it with Image.color and turn it with the rect's rotation.</summary>
        public static Sprite ArrowSprite => arrowSprite != null ? arrowSprite : arrowSprite = MakeShape(32, (x, y) =>
        {
            // Inside the triangle with its tip at the top centre and its base along the bottom.
            float half = (1f - y) * 0.5f;           // half-width shrinks towards the tip
            return Mathf.Abs(x - 0.5f) <= half;
        });

        /// <summary>A white water drop (round bottom, point at the top). Used next to prices.</summary>
        public static Sprite DropSprite => dropSprite != null ? dropSprite : dropSprite = MakeShape(32, (x, y) =>
        {
            // A circle in the lower part, plus a triangle that narrows to a point at the top.
            float dx = x - 0.5f, dy = y - 0.38f;
            if (dx * dx + dy * dy <= 0.30f * 0.30f) return true;
            return y >= 0.38f && y <= 0.95f && Mathf.Abs(dx) <= 0.30f * (0.95f - y) / 0.57f;
        });

        /// <summary>A white diamond (a square turned 45 degrees). Used for map markers.</summary>
        public static Sprite DiamondSprite => diamondSprite != null ? diamondSprite : diamondSprite = MakeShape(32, (x, y) =>
            Mathf.Abs(x - 0.5f) + Mathf.Abs(y - 0.5f) <= 0.5f);

        // Draws a white shape on a transparent square texture. inside(x, y) gets 0-1 coordinates.
        static Sprite MakeShape(int size, System.Func<float, float, bool> inside)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = inside((x + 0.5f) / size, (y + 0.5f) / size)
                        ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
