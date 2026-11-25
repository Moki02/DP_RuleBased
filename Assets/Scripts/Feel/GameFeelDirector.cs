// ============================================================================
// GameFeelDirector.cs
// Connects the game's moments to their sounds and effects (the list in
// Docs/Agents/GameFeel_Hookups.md). It only LISTENS to the game: it subscribes to events,
// reads public values, and plays sounds / effects / screen shake / floating numbers.
// It never changes HYD, water, gates or any rule.
//
//   Hub:    pump loop + gush + splash per stroke ("FULL" when the basin is full), boiling
//           loop + steam, pouring into Clean Storage, tap valve + running water + stream,
//           water banked (+L sparkle).
//   Gates:  lock basin pouring + stream, gate opened (dust, shake), valve minigame (valve,
//           ticking, hit flash / hit-stop / burst, hit/miss/win/lose sounds), metal gates
//           sliding / slamming, room doors rising / slamming.
//   Maze:   zone 2 turning (sound, dust, shake), cistern, note, map, marker, section reward.
//   World:  dusk warnings, lockdown, drought stage sting.
//   UI:     Build / Skills menu open/close, buy, learn, buy refused.
// The player's own sounds are PlayerFeel; loops of the world are AmbienceDirector; button
// clicks / hovers are UiButtonSounds.
//
// DESIGN RULES: none (feel only).
//
// HOW TO USE: on the "GameFeel" object in the game scene (Salok > Feel > Install Game Feel).
// HOW TO EDIT: sounds / effects / volumes in the GameFeelLibrary asset; shake strength and
// floating numbers in FeelSettings. "Shake Range" below: a shake only happens if the gate
// or door is this close to the player.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop.Feel
{
    /// <summary>Plays the sounds and effects for the game's moments (listen-only).</summary>
    public class GameFeelDirector : MonoBehaviour
    {
        [Tooltip("Which sounds and effects to use.")]
        public GameFeelLibrary library;
        [Tooltip("Gates and doors further than this from the player (metres) don't shake the screen.")]
        [Min(0f)] public float shakeRange = 14f;
        [Tooltip("Seconds after the scene starts in which gate / door / zone 2 events are silent " +
                 "(loading a save sets every gate and would make a burst of sounds).")]
        [Min(0f)] public float quietStartSeconds = 2f;

        float startTime;
        Player_Manager player;

        // Hub
        readonly List<HubPump_Manager> pumps = new List<HubPump_Manager>();
        readonly List<FeelLoop> pumpLoops = new List<FeelLoop>();
        readonly List<HubPurifier_Manager> purifiers = new List<HubPurifier_Manager>();
        readonly List<FeelLoop> boilLoops = new List<FeelLoop>();
        readonly List<GameObject> steam = new List<GameObject>();
        readonly List<float> boilSeen = new List<float>();
        readonly List<HubTap_Manager> taps = new List<HubTap_Manager>();
        readonly List<FeelLoop> tapLoops = new List<FeelLoop>();
        readonly List<Stream> tapStreams = new List<Stream>();
        readonly List<bool> tapWasRunning = new List<bool>();
        readonly List<float> tapFillTimer = new List<float>();
        float fullShownUntil;

        // Lock basins
        readonly List<LockBasin_Manager> basins = new List<LockBasin_Manager>();
        readonly List<FeelLoop> basinLoops = new List<FeelLoop>();
        readonly List<Stream> basinStreams = new List<Stream>();

        // A running water stream: the effect, where it lands, and when to splash there next.
        class Stream { public GameObject fx; public Vector3 landing; public float nextSplash; }
        static readonly Quaternion Down = Quaternion.Euler(90f, 0f, 0f); // stream prefabs emit along +Z
        readonly Dictionary<Component, Bounds> modelBounds = new Dictionary<Component, Bounds>();
        System.Func<Component, Vector3> tapSpoutFn, basinPointFn; // made once (no garbage per frame)

        // Minigame
        ValveTimingGame valveGame;
        bool valveWasPlaying;
        FeelLoop tickLoop;

        // Maze
        Zone2Rotator zone2;
        MapRevealTracker markers;
        readonly List<RoomWaterSource> cisterns = new List<RoomWaterSource>();
        readonly List<float> cisternNext = new List<float>();
        float cisternRefresh;

        // Menus
        MapScreen map;
        NotePanel notes;
        GameMenu menu;
        bool mapWasOpen, noteWasOpen, menuWasOpen;
        float uiFindTimer;

        GameClock_Manager clock;

        bool Quiet => Time.time - startTime < quietStartSeconds;

        void Start()
        {
            if (library == null)
            {
                Debug.LogWarning("[GameFeel] No GameFeelLibrary assigned: no game-feel sounds or effects.", this);
                enabled = false;
                return;
            }
            startTime = Time.time;
            player = Player_Manager.instance;

            EnsurePlayerFeel();
            EnsureCameraShake();
            FindHub();
            FindBasins();
            HookGatesAndDoors();
            HookMinigame();

            zone2 = FindFirstObjectByType<Zone2Rotator>();
            if (zone2 != null) zone2.Zone2Turned += OnZone2Turned;
            markers = FindFirstObjectByType<MapRevealTracker>();
            if (markers != null) markers.MarkerToggled += OnMarker;
            clock = GameClock_Manager.Instance;
            if (clock != null) clock.NightStarted += OnNightStarted;

            HubPump_Manager.Stroked += OnPumpStroke;
            SurvivalDirector.Banked += OnBanked;
            SurvivalRules.Rewarded += OnRewarded;
            NightLockdown.LockdownWarning += OnLockdownWarning;
            StageManager.StageChanged += OnStageChanged;
            UpgradeManager.Purchased += OnPurchased;
        }

        void OnDestroy()
        {
            if (zone2 != null) zone2.Zone2Turned -= OnZone2Turned;
            if (markers != null) markers.MarkerToggled -= OnMarker;
            if (clock != null) clock.NightStarted -= OnNightStarted;
            HubPump_Manager.Stroked -= OnPumpStroke;
            SurvivalDirector.Banked -= OnBanked;
            SurvivalRules.Rewarded -= OnRewarded;
            NightLockdown.LockdownWarning -= OnLockdownWarning;
            StageManager.StageChanged -= OnStageChanged;
            UpgradeManager.Purchased -= OnPurchased;
        }

        // ================================================================ setup

        // The player needs a PlayerFeel and the camera a CameraShake. The installer adds them; this
        // is the safety net for scenes where it couldn't (e.g. the player is spawned at runtime).
        void EnsurePlayerFeel()
        {
            if (player == null || player.GetComponent<PlayerFeel>() != null) return;
            var pf = player.gameObject.AddComponent<PlayerFeel>();
            pf.library = library;
        }

        void EnsureCameraShake()
        {
            var cam = Player_Camera.instance != null && Player_Camera.instance.cameraObject != null
                ? Player_Camera.instance.cameraObject : Camera.main;
            if (cam != null && cam.GetComponent<CameraShake>() == null) cam.gameObject.AddComponent<CameraShake>();
        }

        void FindHub()
        {
            foreach (var p in FindObjectsByType<HubPump_Manager>(FindObjectsSortMode.None))
            {
                pumps.Add(p);
                pumpLoops.Add(new FeelLoop(p.gameObject, library.pumpLoop, spatial: true) { fadeIn = 0.1f, fadeOut = 0.35f });
            }
            foreach (var p in FindObjectsByType<HubPurifier_Manager>(FindObjectsSortMode.None))
            {
                purifiers.Add(p);
                boilLoops.Add(new FeelLoop(p.gameObject, library.boilLoop, spatial: true) { fadeIn = 1f, fadeOut = 2f });
                steam.Add(null);
                boilSeen.Add(-10f);
            }
            foreach (var t in FindObjectsByType<HubTap_Manager>(FindObjectsSortMode.None))
            {
                taps.Add(t);
                tapLoops.Add(new FeelLoop(t.gameObject, library.pourLoop, spatial: true) { fadeIn = 0.2f, fadeOut = 0.5f });
                tapStreams.Add(new Stream());
                tapWasRunning.Add(false);
                tapFillTimer.Add(0f);
            }
        }

        void FindBasins()
        {
            foreach (var b in FindObjectsByType<LockBasin_Manager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                basins.Add(b);
                basinLoops.Add(new FeelLoop(b.gameObject, library.pourLoop, spatial: true) { fadeIn = 0.15f, fadeOut = 0.4f });
                basinStreams.Add(new Stream());
                b.Opened += OnBasinOpened;
            }
        }

        void HookGatesAndDoors()
        {
            foreach (var g in FindObjectsByType<GateSlider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var gate = g;
                Transform at = gate.leaf != null ? gate.leaf : gate.transform;
                if (gate.onStartOpening == null) gate.onStartOpening = new UnityEngine.Events.UnityEvent();
                if (gate.onStartClosing == null) gate.onStartClosing = new UnityEngine.Events.UnityEvent();
                gate.onStartOpening.AddListener(() => WorldThud(library.gateOpen, at.position));
                gate.onStartClosing.AddListener(() => WorldThud(library.gateSlam, at.position));
            }
            foreach (var d in FindObjectsByType<RoomDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var door = d;
                if (door.onOpened == null) door.onOpened = new UnityEngine.Events.UnityEvent();
                if (door.onClosed == null) door.onClosed = new UnityEngine.Events.UnityEvent();
                door.onOpened.AddListener(() => WorldThud(library.doorOpen, door.transform.position));
                door.onClosed.AddListener(() => WorldThud(library.gateSlam, door.transform.position, 0.7f));
            }
        }

        // The valve minigame: its own sound slots get the team's picks, and every hit gets the
        // flash / hit-stop / burst (HitFeedback on the needle).
        void HookMinigame()
        {
            valveGame = ValveTimingGame.Instance != null ? ValveTimingGame.Instance : FindFirstObjectByType<ValveTimingGame>(FindObjectsInactive.Include);
            if (valveGame == null) return;

            if (valveGame.audioSource == null) valveGame.audioSource = valveGame.gameObject.AddComponent<AudioSource>();
            valveGame.audioSource.playOnAwake = false;
            valveGame.audioSource.spatialBlend = 0f;
            valveGame.audioSource.volume = FeelAudio.SfxSetting;
            if (valveGame.hitClip == null) valveGame.hitClip = library.minigameHit.clip;
            if (valveGame.missClip == null) valveGame.missClip = library.minigameMiss.clip;
            if (valveGame.winClip == null) valveGame.winClip = library.minigameWin.clip;
            if (valveGame.loseClip == null) valveGame.loseClip = library.minigameLose.clip;

            var hit = valveGame.GetComponent<HitFeedback>();
            if (hit == null) hit = valveGame.gameObject.AddComponent<HitFeedback>();
            if (hit.flashTarget == null && valveGame.needle != null) hit.flashTarget = valveGame.needle.GetComponentInChildren<Graphic>(true);
            if (hit.burstParent == null) hit.burstParent = valveGame.needle;
            if (hit.burstPrefab == null) hit.burstPrefab = Resources.Load<GameObject>("Feel/UiHitBurst");
            valveGame.onHit.AddListener(hit.Hit);
            valveGame.onMiss.AddListener(hit.Miss);

            tickLoop = new FeelLoop(gameObject, library.needleTick) { fadeIn = 0.05f, fadeOut = 0.15f };
        }

        // ================================================================ every frame

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool paused = Time.timeScale <= 0f;

            // Pumps
            for (int i = 0; i < pumps.Count; i++)
                if (pumps[i] != null) pumpLoops[i].Update(pumps[i].IsPumping && !paused, dt);

            // Boiling pot: loop + steam (kept on for a moment so one quiet frame doesn't cut it)
            bool anyBoiling = false;
            for (int i = 0; i < purifiers.Count; i++)
            {
                var p = purifiers[i];
                if (p == null) continue;
                if (p.IsBoiling) boilSeen[i] = Time.time;
                bool boiling = !paused && Time.time - boilSeen[i] < 0.5f;
                anyBoiling |= boiling;
                boilLoops[i].Update(boiling, dt);
                var pot = ModelBounds(p);
                steam[i] = Toggle(steam[i], boiling, library.vfxSteam, new Vector3(pot.center.x, pot.max.y, pot.center.z));
            }

            // Hub tap: valve twist on start, running water while used (quieter pour while the pot
            // fills Clean Storage), canteen fill sound while refilling.
            for (int i = 0; i < taps.Count; i++)
            {
                var t = taps[i];
                if (t == null) continue;
                bool running = !paused && Time.time - t.LastUsedTime < 0.15f;
                if (running && !tapWasRunning[i]) FeelAudio.PlayAt(library.tapOpen, t.transform.position);
                tapWasRunning[i] = running;
                tapLoops[i].Update(running || anyBoiling, dt, running ? 1f : 0.45f);
                UpdateStream(tapStreams[i], running, t, tapSpoutFn ??= TapSpout);

                if (running && t.LastUseWasRefill)
                {
                    tapFillTimer[i] -= dt;
                    if (tapFillTimer[i] <= 0f)
                    {
                        tapFillTimer[i] = library.canteenFill.clip != null ? library.canteenFill.clip.length : 2f;
                        FeelAudio.PlayAt(library.canteenFill, t.transform.position + Vector3.up);
                    }
                }
                else tapFillTimer[i] = 0f;
            }

            // Lock basins
            for (int i = 0; i < basins.Count; i++)
            {
                var b = basins[i];
                if (b == null) continue;
                bool pouring = !paused && Time.time - b.LastPourTime < 0.15f;
                basinLoops[i].Update(pouring, dt);
                UpdateStream(basinStreams[i], pouring, b, basinPointFn ??= BasinPourPoint);
            }

            // Valve minigame
            if (valveGame != null)
            {
                bool playing = valveGame.IsRoundRunning;
                if (playing && !valveWasPlaying) FeelAudio.Play(library.valveTurn);
                valveWasPlaying = playing;
                tickLoop?.Update(playing && !paused, dt);
                if (valveGame.audioSource != null) valveGame.audioSource.volume = FeelAudio.SfxSetting;
            }

            UpdateCisterns(dt, paused);
            UpdateMenus(dt);
        }

        // Spawns / releases a looping effect.
        static GameObject Toggle(GameObject current, bool on, GameObject prefab, Vector3 at)
        {
            if (on && current == null && prefab != null) return FeelPool.Spawn(prefab, at, Quaternion.identity);
            if (!on && current != null) { FeelPool.Release(current); return null; }
            return current;
        }

        // ================================================================ water placement

        // Starts / stops a pour stream. It starts at 'source' (asked only when it starts),
        // points straight down, ends on the first surface below, and splashes there.
        void UpdateStream(Stream s, bool on, Component owner, System.Func<Component, Vector3> source)
        {
            if (on && s.fx == null && library.vfxPourStream != null)
            {
                Vector3 from = source(owner);
                float drop = FindLanding(from, out s.landing);
                s.fx = FeelPool.Spawn(library.vfxPourStream, from, Down);
                FitFall(s.fx, drop);
                s.nextSplash = Time.time + FallTime(1.2f, drop);
            }
            else if (!on && s.fx != null)
            {
                FeelPool.Release(s.fx);
                s.fx = null;
            }
            if (s.fx != null && library.vfxDropletSplash != null && Time.time >= s.nextSplash)
            {
                s.nextSplash = Time.time + 0.2f;
                FeelPool.Spawn(library.vfxDropletSplash, s.landing, Quaternion.identity);
            }
        }

        // Distance (m) from 'from' straight down to the first surface, and the hit point.
        static float FindLanding(Vector3 from, out Vector3 landing)
        {
            const float maxDrop = 4f;
            if (Physics.Raycast(from + Vector3.down * 0.02f, Vector3.down, out var hit, maxDrop,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                landing = hit.point;
                return hit.distance + 0.02f;
            }
            landing = from + Vector3.down * 1.2f;
            return 1.2f;
        }

        // Seconds for a drop thrown down at 'speed' to fall 'drop' metres.
        static float FallTime(float speed, float drop, float gravityModifier = 1f)
        {
            float g = Physics.gravity.magnitude * gravityModifier;
            if (g <= 0.01f) return drop / Mathf.Max(speed, 0.01f);
            return (-speed + Mathf.Sqrt(speed * speed + 2f * g * drop)) / g;
        }

        // Shortens the particles' life so the water stops where it lands (not in mid-air or under the floor).
        static void FitFall(GameObject fx, float drop)
        {
            if (fx == null) return;
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                float t = FallTime(main.startSpeed.constantMax, drop, main.gravityModifier.constantMax);
                main.startLifetime = Mathf.Max(0.05f, t);
            }
        }

        Vector3 PlayerHand(Vector3 fallback)
        {
            return player != null ? player.transform.position + Vector3.up * 1f : fallback;
        }

        // The Clean Storage tap: its FeelAnchor, or a guess on the tank's side facing the player,
        // a little above their hands.
        Vector3 TapSpout(Component tap)
        {
            var anchor = FeelAnchor.Find(tap);
            if (anchor != null) return anchor.transform.position;
            var b = ModelBounds(tap);
            Vector3 p = SideFacing(b, PlayerHand(b.center), 0.06f);
            p.y = Mathf.Clamp(PlayerHand(b.center).y + 0.25f, b.min.y + 0.3f, b.max.y - 0.1f);
            return p;
        }

        // The pump spout: its FeelAnchor, or a guess on the pump's side facing the player.
        Vector3 PumpSpout(HubPump_Manager pump)
        {
            var anchor = FeelAnchor.Find(pump);
            if (anchor != null) return anchor.transform.position;
            var b = ModelBounds(pump);
            Vector3 p = SideFacing(b, PlayerHand(b.center), 0.06f);
            p.y = b.min.y + b.size.y * 0.65f;
            return p;
        }

        // Above a lock basin: its FeelAnchor, or just over the rim on the player's side, so the
        // water falls into the bowl from the player's canteen.
        Vector3 BasinPourPoint(Component basin)
        {
            var anchor = FeelAnchor.Find(basin);
            if (anchor != null) return anchor.transform.position;
            var b = ModelBounds(basin);
            Vector3 hand = PlayerHand(b.center);
            Vector3 rim = b.ClosestPoint(new Vector3(hand.x, b.max.y, hand.z));
            Vector3 centre = new Vector3(b.center.x, rim.y, b.center.z);
            rim = Vector3.MoveTowards(rim, centre, Mathf.Min(0.2f, Vector3.Distance(rim, centre) * 0.5f));
            rim.y = b.max.y + 0.3f;
            return rim;
        }

        // The point on the box's side nearest 'target', pushed 'outset' metres outwards.
        static Vector3 SideFacing(Bounds b, Vector3 target, float outset)
        {
            Vector3 p = b.ClosestPoint(target);
            Vector3 dir = p - b.center;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            return p + dir.normalized * outset;
        }

        // World box around the visible model of an object (labels and effects left out). Measured
        // once; the hub objects don't move.
        Bounds ModelBounds(Component c)
        {
            if (modelBounds.TryGetValue(c, out var b)) return b;
            b = Measure(c.transform);
            if (b.size == Vector3.zero && c.transform.parent != null) b = Measure(c.transform.parent);
            if (b.size == Vector3.zero) b = new Bounds(c.transform.position + Vector3.up * 0.5f, Vector3.one);
            modelBounds[c] = b;
            return b;
        }

        static Bounds Measure(Transform root)
        {
            bool any = false;
            var b = new Bounds(root.position, Vector3.zero);
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (r.GetComponent<TMPro.TMP_Text>() != null) continue; // floating name labels
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        void UpdateCisterns(float dt, bool paused)
        {
            // Room water sources are made when the game starts; look again now and then.
            cisternRefresh -= dt;
            if (cisternRefresh <= 0f)
            {
                cisternRefresh = 5f;
                cisterns.Clear();
                cisternNext.Clear();
                foreach (var c in FindObjectsByType<RoomWaterSource>(FindObjectsSortMode.None))
                {
                    cisterns.Add(c);
                    cisternNext.Add(0f);
                }
            }
            for (int i = 0; i < cisterns.Count; i++)
            {
                var c = cisterns[i];
                if (c == null) continue;
                bool filling = !paused && Time.time - c.LastUsedTime < 0.15f;
                if (filling && Time.time >= cisternNext[i])
                {
                    FeelAudio.PlayAt(library.cistern, c.transform.position);
                    cisternNext[i] = Time.time + (library.cistern.clip != null ? library.cistern.clip.length : 4f);
                }
                else if (!filling) cisternNext[i] = 0f;
            }
        }

        void UpdateMenus(float dt)
        {
            // The map, notes and Build menu are made from prefabs at runtime: find them when they exist.
            uiFindTimer -= dt;
            if (uiFindTimer <= 0f)
            {
                uiFindTimer = 1f;
                if (map == null) map = FindFirstObjectByType<MapScreen>();
                if (notes == null) notes = FindFirstObjectByType<NotePanel>();
                if (menu == null) menu = FindFirstObjectByType<GameMenu>();
            }

            bool mapOpen = map != null && map.IsOpen;
            if (mapOpen != mapWasOpen) FeelAudio.Play(library.mapOpen);
            mapWasOpen = mapOpen;

            bool noteOpen = notes != null && notes.NoteOpen;
            if (noteOpen && !noteWasOpen) FeelAudio.Play(library.note);
            noteWasOpen = noteOpen;

            bool menuOpen = menu != null && menu.IsOpen;
            if (menuOpen != menuWasOpen) FeelAudio.Play(library.menuOpen);
            menuWasOpen = menuOpen;
        }

        // ================================================================ events

        // A gate / door moving: 3D sound, dust, and a big shake if it's close to the player.
        void WorldThud(FeelSound sound, Vector3 at, float volume = 1f)
        {
            if (Quiet) return;
            FeelAudio.PlayAt(sound, at, 1f, volume);
            if (library.vfxDustPuff != null) FeelPool.Spawn(library.vfxDustPuff, at, Quaternion.identity);
            if (player != null && (player.transform.position - at).sqrMagnitude <= shakeRange * shakeRange)
                CameraShake.Add(FeelSettings.Active.shakeBig);
        }

        void OnPumpStroke(HubPump_Manager pump, float litres)
        {
            Vector3 spout = PumpSpout(pump);
            if (litres > 0f)
            {
                FeelAudio.PlayAt(library.gush, spout, Random.Range(0.95f, 1.05f), 0.7f);
                float drop = FindLanding(spout, out var landing);
                if (library.vfxPumpGush != null) FitFall(FeelPool.Spawn(library.vfxPumpGush, spout, Down), drop);
                if (library.vfxDropletSplash != null)
                    StartCoroutine(SplashLater(landing, FallTime(1.4f, drop)));
            }
            else if (Time.time >= fullShownUntil)
            {
                fullShownUntil = Time.time + 2f;
                FloatingText.Show("FULL", pump.transform.position + Vector3.up * 2f, FloatingText.Mood.Neutral);
            }
        }

        // Droplets where the pump's gush lands, timed with the first drops arriving
        // (and a second, smaller one as the gush ends).
        System.Collections.IEnumerator SplashLater(Vector3 at, float delay)
        {
            yield return new WaitForSeconds(delay);
            FeelPool.Spawn(library.vfxDropletSplash, at, Quaternion.identity);
            yield return new WaitForSeconds(0.2f);
            FeelPool.Spawn(library.vfxDropletSplash, at, Quaternion.identity);
        }

        void OnBasinOpened(LockBasin_Manager basin)
        {
            Vector3 at = basin.transform.position;
            if (library.vfxDustPuff != null) FeelPool.Spawn(library.vfxDustPuff, at, Quaternion.identity);
            CameraShake.Add(FeelSettings.Active.shakeBig);
        }

        void OnBanked(float carried, float bonus)
        {
            FeelAudio.Play(library.waterBanked);
            Vector3 at = player != null ? player.transform.position : transform.position;
            if (library.vfxWaterSparkle != null) FeelPool.Spawn(library.vfxWaterSparkle, at + Vector3.up, Quaternion.identity);
            FloatingText.ShowAmount(carried + bonus, " L", at + Vector3.up * 2.2f);
        }

        void OnRewarded(float litres, string why)
        {
            FeelAudio.Play(library.reward);
            Vector3 at = player != null ? player.transform.position : transform.position;
            if (library.vfxRewardPop != null) FeelPool.Spawn(library.vfxRewardPop, at + Vector3.up, Quaternion.identity);
            FloatingText.ShowAmount(litres, " L", at + Vector3.up * 2.2f);
        }

        void OnZone2Turned(int oldTurns, int newTurns)
        {
            if (Quiet) return;
            FeelAudio.Play(library.zone2Turn);
            if (library.vfxZone2Dust != null) FeelPool.Spawn(library.vfxZone2Dust, zone2.transform.position, Quaternion.identity);
            CameraShake.Add(FeelSettings.Active.shakeBig);
        }

        void OnMarker(MarkerResult result) => FeelAudio.Play(library.marker);

        void OnNightStarted(int day)
        {
            FeelAudio.Play(library.lockdown);
            CameraShake.Add(FeelSettings.Active.shakeBig);
        }

        void OnLockdownWarning(float secondsLeft) => FeelAudio.Play(library.duskWarning);

        void OnStageChanged(int stage)
        {
            FeelAudio.Play(library.droughtSting);
            CameraShake.Add(FeelSettings.Active.shakeBig);
        }

        void OnPurchased(string id, BuyResult result)
        {
            if (result != BuyResult.Bought) { FeelAudio.Play(library.notEnough); return; }
            var info = UpgradeManager.Instance != null && UpgradeManager.Instance.Logic != null
                ? UpgradeManager.Instance.Logic.Get(id) : null;
            FeelAudio.Play(info != null && info.Tab == UpgradeTab.Skills ? library.unlock : library.buy);
        }
    }
}
