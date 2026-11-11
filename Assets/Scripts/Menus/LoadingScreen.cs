// ============================================================================
// LoadingScreen.cs
// The loading screen shown while a scene loads. It is a prefab with its own Canvas:
//     Assets/Resources/LoadingScreen.prefab
// (Tools > The Last Drop > Build Menu Scenes makes it once.) The artists can open the prefab
// and change everything in it: background, "LOADING..." text, the bar, fonts, add art. This
// script only moves the bar's fill and writes the percent.
//
// It lives across the scene change and removes itself once the new scene has run its first
// frame. If the prefab is missing, a plain black one is built in code so loading still works.
//
// DESIGN RULES: none (pending UI list: loading screen).
//
// HOW TO USE: LoadingScreen.Load("SceneName"). SceneFlow does this for every scene change.
// In the prefab: "Fill" is the bar's moving part (its right edge moves from 0 to 100%), and
// "Percent Text" shows "45%". Both may be left empty.
// HOW TO EDIT: the minimum time on screen is below.
// ============================================================================

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastDrop
{
    /// <summary>Loads a scene behind the loading screen prefab.</summary>
    public class LoadingScreen : MonoBehaviour
    {
        /// <summary>The prefab's path inside a Resources folder (without .prefab).</summary>
        public const string PrefabResource = "LoadingScreen";

        [Tooltip("The bar's fill. Its right edge (Anchor Max X) moves from 0 to 1 as the scene loads.")]
        public RectTransform fill;
        [Tooltip("Shows the percent, e.g. \"45%\". Optional.")]
        public TMP_Text percentText;
        [Tooltip("Shortest time on screen in seconds, so it doesn't just flicker on fast loads.")]
        public float minSeconds = 0.4f;

        static LoadingScreen active;

        /// <summary>True while a scene is loading.</summary>
        public static bool IsLoading => active != null;

        /// <summary>Shows the loading screen and loads <paramref name="sceneName"/>.</summary>
        public static void Load(string sceneName)
        {
            if (active != null) return; // already loading something (real time, so a frozen game is fine)

            var prefab = Resources.Load<GameObject>(PrefabResource);
            GameObject go = prefab != null ? Instantiate(prefab) : BuildFallback();
            go.name = "LoadingScreen";
            DontDestroyOnLoad(go);
            active = go.GetComponent<LoadingScreen>();
            if (active == null) active = go.AddComponent<LoadingScreen>();
            active.StartCoroutine(active.LoadRoutine(sceneName));
        }

        // Plain black screen with "LOADING...", a white bar and the percent (prefab missing).
        static GameObject BuildFallback()
        {
            var canvas = UiBuilder.CreateCanvas("LoadingScreen", null, 200);
            var screen = canvas.gameObject.AddComponent<LoadingScreen>();
            BuildDefaultLook(canvas.transform, screen);
            return canvas.gameObject;
        }

        /// <summary>Builds the default black-and-white look under <paramref name="root"/> (the menu command uses this for the prefab).</summary>
        public static void BuildDefaultLook(Transform root, LoadingScreen screen)
        {
            UiBuilder.Image("Background", root, Color.black, blocksClicks: true);

            var title = UiBuilder.Text("Loading Text", root, "LOADING...", 48f, Color.white, TextAlignmentOptions.Center);
            UiBuilder.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(800f, 70f));

            var bar = UiBuilder.Image("Bar", root, new Color(0.25f, 0.25f, 0.25f));
            UiBuilder.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 12f));
            var fill = UiBuilder.Image("Fill", bar.transform, Color.white);
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);

            var percent = UiBuilder.Text("Percent Text", root, "0%", 24f, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Center);
            UiBuilder.Place(percent.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(400f, 40f));

            screen.fill = fill.rectTransform;
            screen.percentText = percent;
        }

        IEnumerator LoadRoutine(string sceneName)
        {
            float start = Time.unscaledTime;
            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[LoadingScreen] Can't load \"{sceneName}\". Is it in File > Build Profiles > Scene List? " +
                               "(Tools > The Last Drop > Build Menu Scenes adds the menu scenes.)");
                Finish();
                yield break;
            }
            op.allowSceneActivation = false;

            // Unity stops at 0.9 until activation is allowed.
            while (op.progress < 0.9f || Time.unscaledTime - start < minSeconds)
            {
                Show(Mathf.Clamp01(op.progress / 0.9f));
                yield return null;
            }
            Show(1f);
            Time.timeScale = 1f;   // the old scene may have been frozen (pause, end): the new one starts normal
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            yield return null; // let the new scene run its first frame (Start) behind the screen
            Finish();
        }

        void Show(float t)
        {
            if (fill != null) fill.anchorMax = new Vector2(t, fill.anchorMax.y);
            if (percentText != null) percentText.text = Mathf.RoundToInt(t * 100f) + "%";
        }

        void Finish()
        {
            if (active == this) active = null;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (active == this) active = null;
        }
    }
}
