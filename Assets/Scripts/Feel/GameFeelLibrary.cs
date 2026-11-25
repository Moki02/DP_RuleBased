// ============================================================================
// GameFeelLibrary.cs
// One asset that says WHICH sound and WHICH effect plays at each moment of the game, and
// how loud. The GameFeel scripts (GameFeelDirector, PlayerFeel, AmbienceDirector,
// UiButtonSounds) only read this asset, so swapping a sound never needs code.
//
// DESIGN RULES: none (feel only; no rule numbers, no GameBalance values).
//
// HOW TO USE: the asset is Assets/VFX/GameFeelLibrary.asset, filled by
// Salok > Feel > Install Game Feel. The GameFeel object in the game scene points at it.
// HOW TO EDIT: select the asset. Drag a different clip into a slot to change a sound, move
// the volume slider (0..1) to make it quieter or louder. Empty slot = silent.
// The team's picks and their sources are listed in Assets/Audio/CREDITS.md.
// ============================================================================

using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>A sound and how loud it plays.</summary>
    [System.Serializable]
    public class FeelSound
    {
        [Tooltip("The sound. Empty = silent.")]
        public AudioClip clip;
        [Tooltip("How loud (0..1). The player's SFX / Music volume setting is applied on top.")]
        [Range(0f, 1f)] public float volume = 0.8f;

        /// <summary>True if there is a clip to play.</summary>
        public bool IsSet => clip != null && volume > 0f;
    }

    /// <summary>Which sound and effect plays at each game moment.</summary>
    [CreateAssetMenu(menuName = "Salok/Game Feel Library", fileName = "GameFeelLibrary")]
    public class GameFeelLibrary : ScriptableObject
    {
        [Header("Player")]
        [Tooltip("Walking on sand (one long recording, loops while walking on sand).")]
        public FeelSound footstepSandLoop = new FeelSound();
        [Tooltip("One step on stone (maze floor); played every step, pitch varies a little.")]
        public FeelSound footstepStone = new FeelSound();
        [Tooltip("Breathing while sprinting (loop).")]
        public FeelSound sprintBreath = new FeelSound();
        [Tooltip("A gulp while drinking from the canteen or the tap.")]
        public FeelSound drink = new FeelSound();
        [Tooltip("Filling the canteen at the hub tap.")]
        public FeelSound canteenFill = new FeelSound();
        [Tooltip("The player collapses.")]
        public FeelSound collapse = new FeelSound();
        [Tooltip("Heartbeat while HYD is low (loop, louder as HYD drops).")]
        public FeelSound lowHydHeartbeat = new FeelSound();
        [Tooltip("Heavy breathing while HYD is low (loop, louder as HYD drops).")]
        public FeelSound lowHydBreath = new FeelSound();
        [Tooltip("A cough every few seconds while sick.")]
        public FeelSound cough = new FeelSound();

        [Header("Hub water")]
        [Tooltip("Pumping (loop while E is held on the pump).")]
        public FeelSound pumpLoop = new FeelSound();
        [Tooltip("Water gushing into the Raw Basin (each stroke that adds water).")]
        public FeelSound gush = new FeelSound();
        [Tooltip("Boiling bubbles (loop while the pot boils).")]
        public FeelSound boilLoop = new FeelSound();
        [Tooltip("Water pouring (loop): into Clean Storage, into a lock basin, and the running tap.")]
        public FeelSound pourLoop = new FeelSound();
        [Tooltip("The tap valve twisting open (when the tap starts).")]
        public FeelSound tapOpen = new FeelSound();
        [Tooltip("Water banked at the hub (+L).")]
        public FeelSound waterBanked = new FeelSound();

        [Header("Gates, doors and the valve minigame")]
        [Tooltip("The valve wheel turning (when the minigame starts).")]
        public FeelSound valveTurn = new FeelSound();
        [Tooltip("The needle ticking (loop while the minigame runs).")]
        public FeelSound needleTick = new FeelSound();
        [Tooltip("Minigame: green hit.")]
        public FeelSound minigameHit = new FeelSound();
        [Tooltip("Minigame: miss.")]
        public FeelSound minigameMiss = new FeelSound();
        [Tooltip("Minigame: won (the valve opens).")]
        public FeelSound minigameWin = new FeelSound();
        [Tooltip("Minigame: lost.")]
        public FeelSound minigameLose = new FeelSound();
        [Tooltip("A metal gate sliding open.")]
        public FeelSound gateOpen = new FeelSound();
        [Tooltip("A gate or door slamming shut (night).")]
        public FeelSound gateSlam = new FeelSound();
        [Tooltip("A room door rising open.")]
        public FeelSound doorOpen = new FeelSound();

        [Header("Maze")]
        [Tooltip("Zone 2 turning (one 7.5 s clip).")]
        public FeelSound zone2Turn = new FeelSound();
        [Tooltip("Filling the canteen at a room cistern.")]
        public FeelSound cistern = new FeelSound();
        [Tooltip("Opening a survivor note.")]
        public FeelSound note = new FeelSound();
        [Tooltip("Opening or closing the map.")]
        public FeelSound mapOpen = new FeelSound();
        [Tooltip("Placing or removing a map marker.")]
        public FeelSound marker = new FeelSound();
        [Tooltip("A map section revealed / one-time water reward.")]
        public FeelSound reward = new FeelSound();

        [Header("Night and world")]
        [Tooltip("Each lockdown warning before night.")]
        public FeelSound duskWarning = new FeelSound();
        [Tooltip("Night lockdown starts.")]
        public FeelSound lockdown = new FeelSound();
        [Tooltip("Thunder (random during a storm).")]
        public FeelSound thunder = new FeelSound();
        [Tooltip("The drought moves to a new stage.")]
        public FeelSound droughtSting = new FeelSound();

        [Header("UI")]
        [Tooltip("Any button clicked.")]
        public FeelSound uiClick = new FeelSound();
        [Tooltip("The mouse moves onto a button.")]
        public FeelSound uiHover = new FeelSound();
        [Tooltip("The Build / Skills menu opens or closes.")]
        public FeelSound menuOpen = new FeelSound();
        [Tooltip("A Build upgrade bought.")]
        public FeelSound buy = new FeelSound();
        [Tooltip("A skill learned.")]
        public FeelSound unlock = new FeelSound();
        [Tooltip("A buy that failed (not enough water, not at the hub).")]
        public FeelSound notEnough = new FeelSound();
        [Tooltip("The Win scene opens.")]
        public FeelSound winSting = new FeelSound();
        [Tooltip("The Game Over scene opens.")]
        public FeelSound gameOverSting = new FeelSound();

        [Header("Ambience (loops, crossfaded)")]
        [Tooltip("In the hub by day.")]
        public FeelSound ambienceHubDay = new FeelSound();
        [Tooltip("In the maze by day (dry wind).")]
        public FeelSound ambienceMazeDay = new FeelSound();
        [Tooltip("At night.")]
        public FeelSound ambienceNight = new FeelSound();
        [Tooltip("Tension in the last minute before lockdown (on top).")]
        public FeelSound ambienceTension = new FeelSound();
        [Tooltip("During a heatwave (on top).")]
        public FeelSound ambienceHeatwave = new FeelSound();
        [Tooltip("During the thunderstorm's rain (on top).")]
        public FeelSound ambienceStorm = new FeelSound();
        [Tooltip("Light rain under the storm (on top).")]
        public FeelSound ambienceRain = new FeelSound();
        [Tooltip("Birds for a short while after dawn (on top).")]
        public FeelSound ambienceDawn = new FeelSound();
        [Tooltip("The main menu.")]
        public FeelSound ambienceMenu = new FeelSound();

        [Header("Effects (prefabs from Assets/VFX/Effects)")]
        [Tooltip("Dust under each footstep on sand.")]
        public GameObject vfxFootstepDust;
        [Tooltip("Gush of water falling from the pump spout each stroke.")]
        public GameObject vfxPumpGush;
        [Tooltip("Small droplets where falling water lands (pump, tap, basin).")]
        public GameObject vfxDropletSplash;
        [Tooltip("Steam over the boiling pot (loop).")]
        public GameObject vfxSteam;
        [Tooltip("Water stream at the tap / into a lock basin (loop).")]
        public GameObject vfxPourStream;
        [Tooltip("Sparkle when water is banked.")]
        public GameObject vfxWaterSparkle;
        [Tooltip("Dust when a gate slides or a door rises.")]
        public GameObject vfxDustPuff;
        [Tooltip("Dust around zone 2 when it turns.")]
        public GameObject vfxZone2Dust;
        [Tooltip("Reward pop when a map section pays out.")]
        public GameObject vfxRewardPop;
        [Tooltip("Flies around the player while sick (loop).")]
        public GameObject vfxSickFlies;
        [Tooltip("Rain during the storm (loop, follows the camera).")]
        public GameObject vfxRain;
        [Tooltip("Blowing dust during a heatwave (loop, follows the camera).")]
        public GameObject vfxDustBlowing;
        [Tooltip("Sand streaks during a heatwave (loop, follows the camera).")]
        public GameObject vfxSandStreaks;
    }
}
