// ============================================================================
// Compass.cs
// The compass strip at the top of the HUD. It shows which way the player's camera faces:
// N, NE, E, SE, S, SW, W, NW, with small ticks between them. The letter in the middle (under
// the centre marker) is the way you are looking; turn right and the marks slide left.
//
// NORTH IS WORLD +Z (Unity's blue forward arrow in the Scene view). East is +X. So in the
// Scene view's top-down view, North is up, the same as the map screen if it's drawn that way.
// In zone 2 the maze turns and carries the player (Player_Camera.TurnWithPlayer), so the
// compass swings round when the maze turns: the player really is facing a new direction.
//
// LOCKED BEHIND A SKILL: it only appears once the player owns Sun Reading (V6, Skills tab,
// Scout branch, 20 L), which sets the effect showCompass = true in BuildAndSkills_Effects.csv.
// Any other upgrade given showCompass = true would unlock it too.
//
// It's shown only while exploring: hidden in menus, the map, the minigame, the pause menu,
// after a collapse, during loading and behind How to Play.
//
// DESIGN RULES: none yet (pending GDD entry: HUD compass, skill V6 Sun Reading).
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems).
// HOW TO EDIT: THE LOOK is a prefab with its own Canvas: Assets/Resources/UI/Compass.prefab
// (see CompassView.cs; Tools > The Last Drop > Build UI Prefabs makes it). Spacing (degrees
// across the strip, tick spacing, edge fade) is on CompassView in that prefab.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Top-of-HUD compass strip (N = world +Z).</summary>
    public class Compass : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<Compass>();

        /// <summary>Where the default prefab lives inside a Resources folder.</summary>
        public const string PrefabResource = "UI/Compass";

        /// <summary>The effect field that turns the compass on (set by skill V6 Sun Reading).</summary>
        public const string UnlockField = "showCompass";

        CompassView view;
        Canvas canvas;
        RectTransform[] markRects;
        Graphic[] markGraphics;
        float[] markAngles;

        // ------------------------------------------------------------------ Pure maths (tested)

        /// <summary>
        /// How many degrees to the right of the player's heading a mark sits (-180..180).
        /// Negative = to the left. Heading and mark are compass degrees (0 = N, 90 = E).
        /// </summary>
        public static float DegreesFromCenter(float heading, float markAngle) => Mathf.DeltaAngle(heading, markAngle);

        /// <summary>Compass heading (0 = N = +Z, 90 = E = +X) from a world direction. Up/down is ignored.</summary>
        public static float HeadingFromDirection(Vector3 forward)
        {
            if (forward.x * forward.x + forward.z * forward.z < 1e-8f) return 0f;
            float deg = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            return deg < 0f ? deg + 360f : deg;
        }

        /// <summary>"N", "NE" ... for the 45-degree marks, null for the ticks in between.</summary>
        public static string LabelFor(float angle)
        {
            int a = Mathf.RoundToInt(angle) % 360;
            if (a < 0) a += 360;
            switch (a)
            {
                case 0: return "N";
                case 45: return "NE";
                case 90: return "E";
                case 135: return "SE";
                case 180: return "S";
                case 225: return "SW";
                case 270: return "W";
                case 315: return "NW";
                default: return null;
            }
        }

        /// <summary>1 in the middle part of the strip, fading to 0 at its ends (t = |x| / half width).</summary>
        public static float EdgeAlpha(float t, float edgeFade)
        {
            t = Mathf.Abs(t);
            if (t >= 1f) return 0f;
            if (edgeFade <= 0f) return 1f;
            float start = 1f - edgeFade;
            return t <= start ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (t - start) / edgeFade);
        }

        // ------------------------------------------------------------------ Setup

        void Awake()
        {
            view = UiBuilder.InstantiateView<CompassView>(null, PrefabResource, transform);
            if (view == null) view = CompassView.BuildDefault(transform);
            canvas = view.GetComponentInParent<Canvas>();
            BuildMarks();
            SetVisible(false); // shown on the first frame the player is exploring
        }

        // One copy of the right template for every mark: letters on the 45s, ticks between.
        void BuildMarks()
        {
            if (view.marks == null) view.marks = view.strip;
            float step = Mathf.Clamp(view.tickEvery, 5f, 45f);
            int count = Mathf.RoundToInt(360f / step);
            markRects = new RectTransform[count];
            markGraphics = new Graphic[count];
            markAngles = new float[count];

            for (int i = 0; i < count; i++)
            {
                float angle = i * step;
                string label = LabelFor(angle);
                Graphic template;
                if (label == null) template = view.tickTemplate;
                else if (label.Length == 1) template = view.cardinalTemplate;
                else template = view.intercardinalTemplate != null ? view.intercardinalTemplate : view.cardinalTemplate;
                if (template == null) continue;

                var copy = Instantiate(template, view.marks, false);
                copy.gameObject.name = label != null ? "Mark " + label : "Tick " + Mathf.RoundToInt(angle);
                copy.gameObject.SetActive(true);
                if (label != null && copy is TMP_Text text) text.text = label;
                markRects[i] = copy.rectTransform;
                markGraphics[i] = copy;
                markAngles[i] = angle;
            }
        }

        // ------------------------------------------------------------------ Every frame

        void LateUpdate()
        {
            bool show = ShouldShow();
            SetVisible(show);
            if (!show || view.strip == null) return;

            float heading = CurrentHeading();
            float halfWidth = view.strip.rect.width * 0.5f;
            float pixelsPerDegree = view.strip.rect.width / Mathf.Max(1f, view.degreesVisible);

            for (int i = 0; i < markRects.Length; i++)
            {
                var rt = markRects[i];
                if (rt == null) continue;
                float x = DegreesFromCenter(heading, markAngles[i]) * pixelsPerDegree;
                float alpha = EdgeAlpha(x / Mathf.Max(1f, halfWidth), view.edgeFade);
                bool on = alpha > 0.001f;
                if (rt.gameObject.activeSelf != on) rt.gameObject.SetActive(on);
                if (!on) continue;
                var p = rt.anchoredPosition;
                rt.anchoredPosition = new Vector2(x, p.y);
                // SetAlpha fades without touching the colour the artists picked.
                markGraphics[i].canvasRenderer.SetAlpha(alpha);
            }
        }

        // The way the camera looks, flattened onto the ground.
        static float CurrentHeading()
        {
            if (Player_Camera.instance != null)
                return HeadingFromDirection(Player_Camera.instance.transform.forward);
            var cam = Camera.main;
            return cam != null ? HeadingFromDirection(cam.transform.forward) : 0f;
        }

        static bool ShouldShow()
        {
            if (!Skills.B(UnlockField, false)) return false; // not bought yet
            if (LoadingScreen.IsLoading || HowToPlay.IsOpen || SimpleHud.PanelOpen) return false;
            var gm = Game_Manager.instance;
            return gm == null || gm.currentGameState == Game_Manager.GameState.Exploring;
        }

        void SetVisible(bool on)
        {
            if (canvas != null) { if (canvas.enabled != on) canvas.enabled = on; }
            else if (view.gameObject.activeSelf != on) view.gameObject.SetActive(on);
        }
    }
}
