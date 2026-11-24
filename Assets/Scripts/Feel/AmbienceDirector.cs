// ============================================================================
// AmbienceDirector.cs
// The background sound of the world, crossfaded so it never cuts:
//   base layer: hub by day / maze by day (dry wind) / night,
//   on top:     tension in the last minute before lockdown, heatwave, thunderstorm rain,
//               birds for a short while after dawn,
//   plus random thunder (with a screen shake for the close ones) during a storm, and the
//   rain / blowing-dust effects following the camera during a storm / heatwave.
// Ambience follows the player's Music volume setting. It only reads the game.
//
// DESIGN RULES: none (feel only). Reads: day/night (GameClock_Manager), the storm and
// heatwave (EventManager / WorldState), the hub area (PlayerHydration_Manager.IsInsideHub).
//
// HOW TO USE: on the "GameFeel" object in the game scene (Salok > Feel > Install Game Feel).
// HOW TO EDIT: crossfade time, tension window, dawn length and thunder timing below.
// Which clips and how loud: GameFeelLibrary.
// ============================================================================

using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Crossfaded world ambience, thunder and weather effects.</summary>
    public class AmbienceDirector : MonoBehaviour
    {
        [Tooltip("Which sounds and effects to use.")]
        public GameFeelLibrary library;
        [Tooltip("Seconds to crossfade between ambience layers.")]
        [Min(0.1f)] public float crossfade = 2.5f;
        [Tooltip("The tension layer comes in this many seconds before lockdown.")]
        [Min(0f)] public float tensionSeconds = 60f;
        [Tooltip("How long the dawn birds play after a new day starts (seconds).")]
        [Min(0f)] public float dawnSeconds = 25f;
        [Tooltip("Shortest and longest wait between thunder claps during a storm (seconds).")]
        public Vector2 thunderEvery = new Vector2(7f, 18f);

        FeelLoop hubDay, mazeDay, night, tension, heatwave, storm, rain, dawn;
        GameClock_Manager clock;
        float dawnUntil = -1f;
        float thunderTimer;
        GameObject rainFx, dustFx, sandFx;
        Transform cam;

        void Start()
        {
            if (library == null) { enabled = false; return; }
            hubDay = Loop(library.ambienceHubDay);
            mazeDay = Loop(library.ambienceMazeDay);
            night = Loop(library.ambienceNight);
            tension = Loop(library.ambienceTension);
            heatwave = Loop(library.ambienceHeatwave);
            storm = Loop(library.ambienceStorm);
            rain = Loop(library.ambienceRain);
            dawn = Loop(library.ambienceDawn);
            clock = GameClock_Manager.Instance;
            if (clock != null) clock.NewDay += OnNewDay;
            thunderTimer = Random.Range(thunderEvery.x, thunderEvery.y);
        }

        void OnDestroy()
        {
            if (clock != null) clock.NewDay -= OnNewDay;
        }

        FeelLoop Loop(FeelSound s) => new FeelLoop(gameObject, s, music: true) { fadeIn = crossfade, fadeOut = crossfade };

        void OnNewDay(int day) => dawnUntil = Time.time + dawnSeconds;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (clock == null) clock = GameClock_Manager.Instance;

            bool isNight = clock != null && clock.IsNight;
            var p = Player_Manager.instance;
            bool inHub = p != null && p.playerHydration_Manager != null && p.playerHydration_Manager.IsInsideHub();
            float left = clock != null && clock.Clock != null ? clock.Clock.SecondsLeftInPhase : 999f;

            var events = EventManager.Instance;
            var phase = events != null ? events.Current : EventManager.Phase.None;
            bool raining = phase == EventManager.Phase.StormRain || WorldState.IsRaining;
            bool stormy = raining || phase == EventManager.Phase.StormWarning;
            bool hot = phase == EventManager.Phase.Heatwave || WorldState.HeatwaveActive;

            hubDay.Update(!isNight && inHub, dt);
            mazeDay.Update(!isNight && !inHub, dt, raining ? 0.4f : 1f);
            night.Update(isNight, dt);
            tension.Update(!isNight && left <= tensionSeconds, dt);
            heatwave.Update(hot && !isNight, dt);
            storm.Update(raining, dt);
            rain.Update(raining, dt, 0.7f);
            dawn.Update(!isNight && Time.time < dawnUntil, dt);

            // Thunder: random claps while the storm builds and while it rains.
            if (stormy && Time.timeScale > 0f)
            {
                thunderTimer -= dt;
                if (thunderTimer <= 0f)
                {
                    thunderTimer = Random.Range(thunderEvery.x, thunderEvery.y);
                    float loud = Random.Range(0.55f, 1f);
                    FeelAudio.Play(library.thunder, loud);
                    if (loud > 0.85f) CameraShake.Add(FeelSettings.Active.shakeBig);
                }
            }

            // Weather effects follow the camera.
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            Vector3 at = cam != null ? cam.position : Vector3.zero;
            rainFx = Follow(rainFx, raining, library.vfxRain, at + Vector3.up * 6f);
            dustFx = Follow(dustFx, hot, library.vfxDustBlowing, at);
            sandFx = Follow(sandFx, hot, library.vfxSandStreaks, at);
        }

        static GameObject Follow(GameObject current, bool on, GameObject prefab, Vector3 at)
        {
            if (on && current == null && prefab != null) current = FeelPool.Spawn(prefab, at, Quaternion.identity);
            if (!on && current != null) { FeelPool.Release(current); return null; }
            if (current != null) current.transform.position = at;
            return current;
        }
    }
}
