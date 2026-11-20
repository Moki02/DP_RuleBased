// ============================================================================
// FloatingText.cs
// A number or word that pops up in the world, rises and fades: "+1.5 L", "-2 HYD", "FULL".
// Copies are re-used (pooled), so showing many is cheap. Always faces the camera.
//
// DESIGN RULES: none (feel only).
//
// HOW TO USE (code):
//     FloatingText.Show("+1.5 L", tapPosition, FloatingText.Mood.Good);
//     FloatingText.ShowAmount(1.5f, " L", position);   // picks good / bad colour by sign
// The look is the prefab Assets/VFX/Resources/Feel/FloatingText.prefab (font, size,
// outline): artists can edit it. Speed, life and colours are in FeelSettings.
// HOW TO EDIT: FeelSettings > Floating numbers.
// ============================================================================

using TMPro;
using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>A pooled world-space text that rises and fades.</summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class FloatingText : MonoBehaviour
    {
        /// <summary>Which colour from FeelSettings to use.</summary>
        public enum Mood { Good, Bad, Neutral }

        /// <summary>Where the prefab is loaded from (inside a Resources folder).</summary>
        public const string ResourcePath = "Feel/FloatingText";

        static GameObject prefab;

        [Tooltip("The text component (filled in automatically).")]
        [SerializeField] TextMeshPro text;

        Vector3 startScale;
        Vector3 velocity;
        float age;
        Color color;
        Camera cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => prefab = null;

        /// <summary>Shows <paramref name="message"/> at <paramref name="worldPosition"/>. Returns the copy, or null if the prefab is missing.</summary>
        public static FloatingText Show(string message, Vector3 worldPosition, Mood mood = Mood.Neutral)
        {
            var s = FeelSettings.Active;
            Color c = mood == Mood.Good ? s.floatGoodColor : mood == Mood.Bad ? s.floatBadColor : s.floatNeutralColor;
            return Show(message, worldPosition, c);
        }

        /// <summary>Shows <paramref name="message"/> in a specific colour.</summary>
        public static FloatingText Show(string message, Vector3 worldPosition, Color color)
        {
            if (prefab == null) prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[FloatingText] No prefab at Resources/" + ResourcePath + ". Run Salok > Feel > Build Feel Prefabs.");
                return null;
            }
            var spread = FeelSettings.Active.floatSpread;
            var offset = new Vector3(Random.Range(-spread, spread), 0f, Random.Range(-spread, spread));
            var go = FeelPool.Spawn(prefab, worldPosition + offset, Quaternion.identity);
            var ft = go.GetComponent<FloatingText>();
            ft.Begin(message, color);
            return ft;
        }

        /// <summary>
        /// Shows a signed amount like "+1.5 L" or "-2 HYD". Positive is the good colour, negative
        /// the bad one. Set <paramref name="positiveIsGood"/> false for things like "+2 heat".
        /// </summary>
        public static FloatingText ShowAmount(float amount, string unit, Vector3 worldPosition, bool positiveIsGood = true)
        {
            // One string per call (only when something happens, never per frame).
            string sign = amount > 0f ? "+" : amount < 0f ? "-" : "";
            string msg = sign + Mathf.Abs(amount).ToString("0.#") + unit;
            bool good = (amount >= 0f) == positiveIsGood;
            return Show(msg, worldPosition, good ? Mood.Good : Mood.Bad);
        }

        void Awake()
        {
            if (text == null) text = GetComponent<TextMeshPro>();
            startScale = transform.localScale;
        }

        void Begin(string message, Color c)
        {
            text.text = message;
            color = c;
            text.color = c;
            age = 0f;
            velocity = Vector3.up * FeelSettings.Active.floatRiseSpeed;
            cam = Camera.main;
            Face();
        }

        void LateUpdate()
        {
            var s = FeelSettings.Active;
            age += Time.deltaTime;
            float t = s.floatLifetime > 0f ? age / s.floatLifetime : 1f;
            if (t >= 1f)
            {
                FeelPool.Release(gameObject);
                return;
            }

            transform.position += velocity * Time.deltaTime;
            transform.localScale = startScale * FeelMath.FloatScale(t, s.floatPopScale);
            color.a = FeelMath.FloatAlpha(t, s.floatFadeStart);
            text.color = color;
            Face();
        }

        // Turn to face the camera (same direction as the camera, so text never mirrors).
        void Face()
        {
            if (cam == null) cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
