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
        readonly List<GameObject> tapStreams = new List<GameObject>();
        readonly List<bool> tapWasRunning = new List<bool>();
        readonly List<float> tapFillTimer = new List<float>();
        float fullShownUntil;

        // Lock basins
        readonly List<LockBasin_Manager> basins = new List<LockBasin_Manager>();
        readonly List<FeelLoop> basinLoops = new List<FeelLoop>();
        readonly List<GameObject> basinStreams = new List<GameObject>();

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
                tapStreams.Add(null);
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
                basinStreams.Add(null);
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
                steam[i] = Toggle(steam[i], boiling, library.vfxSteam, p.transform.position + Vector3.up * 1.2f);
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
                tapStreams[i] = Toggle(tapStreams[i], running, library.vfxPourStream, t.transform.position + Vector3.up * 1.2f);

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
                basinStreams[i] = Toggle(basinStreams[i], pouring, library.vfxPourStream, b.transform.position + Vector3.up * 1.4f);
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
            Vector3 spout = pump.transform.position + Vector3.up * 0.8f;
            if (litres > 0f)
            {
                FeelAudio.PlayAt(library.gush, spout, Random.Range(0.95f, 1.05f), 0.7f);
                if (library.vfxWaterSplash != null) FeelPool.Spawn(library.vfxWaterSplash, spout, Quaternion.identity);
            }
            else if (Time.time >= fullShownUntil)
            {
                fullShownUntil = Time.time + 2f;
                FloatingText.Show("FULL", pump.transform.position + Vector3.up * 2f, FloatingText.Mood.Neutral);
            }
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
