// ============================================================================
// PlayerFeel.cs
// Everything the player's own body sounds and looks like:
//   - footsteps by distance walked (sand: the walking loop + a little dust; stone: one
//     step sound every stride, pitch varied a little),
//   - breathing while sprinting,
//   - a gulp now and then while drinking (canteen or hub tap),
//   - the low-HYD vignette with heartbeat and heavy breathing, louder as HYD drops,
//   - a cough every few seconds and flies around the player while Sick,
//   - the collapse sound and a big screen shake,
//   - "-2 HYD" floating numbers when the player pours their own HYD into a lock basin.
// It only READS the player's scripts; it never changes HYD, speed or any rule.
//
// DESIGN RULES: none (feel only).
//
// HOW TO USE: on the player (Salok > Feel > Install Game Feel puts it there; GameFeelDirector
// adds one at runtime if it's missing). Sounds and effects come from the GameFeelLibrary.
// HOW TO EDIT: stride lengths and the ground words below. Volumes are in GameFeelLibrary,
// vignette numbers in FeelSettings.
// ============================================================================

using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Player footsteps, breathing, drinking, low-HYD and sickness feel.</summary>
    public class PlayerFeel : MonoBehaviour
    {
        [Tooltip("Which sounds and effects to use.")]
        public GameFeelLibrary library;
        [Tooltip("The low-HYD vignette in the scene (made from Resources/Feel/LowHydVignette if empty).")]
        public LowHydVignette vignette;

        [Header("Footsteps")]
        [Tooltip("Metres between footsteps when walking.")]
        [Min(0.1f)] public float walkStride = 0.6f;
        [Tooltip("Metres between footsteps when sprinting.")]
        [Min(0.1f)] public float sprintStride = 0.85f;
        [Tooltip("The ground counts as SAND if its object or material name contains one of these words " +
                 "(or it's a Unity Terrain). Anything else is STONE.")]
        public string[] sandWords = { "sand", "dirt", "desert", "terrain", "dune" };
        [Tooltip("Layers the ground check can hit. Nothing = every layer.")]
        public LayerMask groundLayers;

        [Header("Drinking and sickness")]
        [Tooltip("Seconds between gulps while drinking.")]
        [Min(0.2f)] public float gulpInterval = 1.1f;
        [Tooltip("Shortest and longest wait between coughs while Sick (seconds).")]
        public Vector2 coughEvery = new Vector2(4f, 8f);

        Player_Manager player;
        FeelLoop sandLoop, sprintLoop, heartLoop, breathLoop;
        float distance;
        Vector3 lastPos;
        bool onSand = true;
        float groundCheckTimer;
        float gulpTimer;
        float coughTimer;
        GameObject flies;
        float hydLostShown;
        float hydLostTimer;
        readonly RaycastHit[] hits = new RaycastHit[4];
        HubTap_Manager[] taps;
        bool subscribed;

        void Start()
        {
            player = GetComponent<Player_Manager>();
            if (player == null) player = Player_Manager.instance;
            if (library == null) { enabled = false; return; }

            if (vignette == null) vignette = FindFirstObjectByType<LowHydVignette>();
            if (vignette == null)
            {
                var prefab = Resources.Load<GameObject>("Feel/LowHydVignette");
                if (prefab != null) vignette = Instantiate(prefab).GetComponent<LowHydVignette>();
            }

            sandLoop = new FeelLoop(gameObject, library.footstepSandLoop) { fadeIn = 0.1f, fadeOut = 0.25f };
            sprintLoop = new FeelLoop(gameObject, library.sprintBreath) { fadeIn = 0.8f, fadeOut = 1.5f };
            heartLoop = new FeelLoop(gameObject, library.lowHydHeartbeat) { fadeIn = 1f, fadeOut = 1.5f };
            breathLoop = new FeelLoop(gameObject, library.lowHydBreath) { fadeIn = 1f, fadeOut = 1.5f };
            lastPos = transform.position;
            taps = FindObjectsByType<HubTap_Manager>(FindObjectsSortMode.None);
            coughTimer = Random.Range(coughEvery.x, coughEvery.y);
            Subscribe();
        }

        void Subscribe()
        {
            if (subscribed || player == null || player.playerHydration_Manager == null) return;
            player.playerHydration_Manager.OnCollapsed += OnCollapsed;
            LockBasin_Manager.PouredFromHyd += OnPouredFromHyd;
            subscribed = true;
        }

        void OnDestroy()
        {
            if (!subscribed) return;
            if (player != null && player.playerHydration_Manager != null)
                player.playerHydration_Manager.OnCollapsed -= OnCollapsed;
            LockBasin_Manager.PouredFromHyd -= OnPouredFromHyd;
        }

        void Update()
        {
            if (player == null || player.playerHydration_Manager == null) return;
            float dt = Time.unscaledDeltaTime;
            bool paused = Time.timeScale <= 0f;
            var hyd = player.playerHydration_Manager;
            var input = player.playerInput_Manager;
            var loco = player.playerLocomotion_Manager;
            bool canControl = Game_Manager.instance == null || Game_Manager.instance.CanControlPlayer();

            // ---------------------------------------------------------------- Footsteps
            Vector3 pos = transform.position;
            Vector3 step = pos - lastPos;
            step.y = 0f;
            lastPos = pos;
            float moved = step.magnitude;
            bool grounded = loco == null || loco.IsGrounded;
            // Ignore teleports (revive, zone 2 carrying the player).
            bool walking = !paused && grounded && moved > 0.0005f && moved < 1.5f && !hyd.IsCollapsed();
            bool sprinting = walking && input != null && input.sprintInput && input.moveAmount > 0f &&
                             hyd.CanSprint() && (loco == null || !loco.IsCrouching) && canControl;

            groundCheckTimer -= dt;
            if (groundCheckTimer <= 0f) { groundCheckTimer = 0.25f; onSand = CheckSand(pos); }

            if (walking)
            {
                distance += moved;
                float stride = sprinting ? sprintStride : walkStride;
                if (distance >= stride)
                {
                    distance -= stride;
                    if (onSand)
                    {
                        if (library.vfxFootstepDust != null)
                            FeelPool.Spawn(library.vfxFootstepDust, pos, Quaternion.identity);
                    }
                    else
                    {
                        FeelAudio.PlayAt(library.footstepStone, pos, Random.Range(0.92f, 1.08f), sprinting ? 1f : 0.75f);
                    }
                }
            }
            sandLoop.Update(walking && onSand, dt, sprinting ? 1f : 0.75f, sprinting ? 1.15f : 1f);
            sprintLoop.Update(sprinting, dt);

            // ---------------------------------------------------------------- Drinking
            bool tapDrinking = false;
            if (taps != null)
                for (int i = 0; i < taps.Length; i++)
                    if (taps[i] != null && !taps[i].LastUseWasRefill && Time.time - taps[i].LastUsedTime < 0.15f) tapDrinking = true;
            if (!paused && (hyd.IsDrinking || tapDrinking))
            {
                gulpTimer -= dt;
                if (gulpTimer <= 0f) { gulpTimer = gulpInterval; FeelAudio.Play(library.drink); }
            }
            else gulpTimer = 0.15f; // first gulp right after starting

            // ---------------------------------------------------------------- Low HYD
            var stats = player.playerStats_Manager;
            float frac = stats != null && stats.player_MaxHydration > 0f ? stats.player_Hydration / stats.player_MaxHydration : 1f;
            if (hyd.hydrationOff) frac = 1f;
            if (vignette != null) vignette.SetHydrationFraction(frac);
            float strength = FeelMath.VignetteStrength(frac, FeelSettings.Active.vignetteStartsAt);
            heartLoop.Update(strength > 0f && !hyd.IsCollapsed(), dt, 0.4f + 0.6f * strength);
            breathLoop.Update(strength > 0.25f && !hyd.IsCollapsed() && !sprinting, dt, strength);

            // ---------------------------------------------------------------- Sick
            bool sick = hyd.SickSecondsLeft > 0f && !hyd.IsCollapsed();
            if (sick && !paused)
            {
                coughTimer -= dt;
                if (coughTimer <= 0f)
                {
                    coughTimer = Random.Range(coughEvery.x, coughEvery.y);
                    FeelAudio.PlayAt(library.cough, pos + Vector3.up * 1.6f);
                }
                if (flies == null && library.vfxSickFlies != null)
                    flies = FeelPool.Spawn(library.vfxSickFlies, pos + Vector3.up * 1.2f, Quaternion.identity, transform);
            }
            else if (flies != null)
            {
                FeelPool.Release(flies);
                flies = null;
            }

            // ---------------------------------------------------------------- HYD poured
            if (hydLostShown > 0f)
            {
                hydLostTimer -= dt;
                if (hydLostTimer <= 0f)
                {
                    FloatingText.ShowAmount(-hydLostShown, " HYD", pos + Vector3.up * 2.1f);
                    hydLostShown = 0f;
                }
            }
        }

        // Sand or stone under the player.
        bool CheckSand(Vector3 pos)
        {
            int mask = groundLayers.value != 0 ? groundLayers.value : Physics.DefaultRaycastLayers;
            int n = Physics.RaycastNonAlloc(pos + Vector3.up * 0.5f, Vector3.down, hits, 2f, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Collider ground = null;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; ground = c; }
            }
            if (ground == null) return onSand;
            if (ground is TerrainCollider) return true;
            if (HasSandWord(ground.name)) return true;
            var r = ground.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null && HasSandWord(r.sharedMaterial.name)) return true;
            return false;
        }

        bool HasSandWord(string s)
        {
            if (string.IsNullOrEmpty(s) || sandWords == null) return false;
            for (int i = 0; i < sandWords.Length; i++)
                if (!string.IsNullOrEmpty(sandWords[i]) && s.IndexOf(sandWords[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        void OnCollapsed()
        {
            FeelAudio.PlayAt(library.collapse, transform.position + Vector3.up);
            CameraShake.Add(FeelSettings.Active.shakeBig);
            sprintLoop.Stop();
            sandLoop.Stop();
        }

        void OnPouredFromHyd(LockBasin_Manager basin, float hydLost)
        {
            // Pouring runs every frame: add it up and show one number every half second.
            if (hydLostShown <= 0f) hydLostTimer = 0.5f;
            hydLostShown += hydLost;
        }
    }
}
