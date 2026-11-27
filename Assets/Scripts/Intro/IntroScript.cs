// ============================================================================
// IntroScript.cs
// The intro cutscene's words, sounds and timings, in one asset:
//     Assets/Resources/Intro/IntroScript.asset
// (Salok > Intro > Build Intro Scene makes it once with the default script; after that the
// asset is never overwritten, so edit it freely.)
//
// Each BEAT is a block of text shown on its own screen. One line of the Text box = one typed
// line. A line starting with [LOUDSPEAKER] is a loudspeaker line: other colour, typed fast,
// no clicks. {name} becomes the player's name, {NAME} the name in capitals.
// Each beat has its own speed, pauses and sound CUES (play a one-shot, start or stop a loop)
// fired before a given line.
//
// DESIGN RULES: none (intro).
//
// HOW TO USE: IntroDirector (in the Intro scene) plays it.
// HOW TO EDIT: select the asset; every field has a tooltip. See Docs/Agents/Intro_Cutscene.md.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>The intro cutscene's beats (text, speed, sound cues) and global timings.</summary>
    [CreateAssetMenu(menuName = "Salok/Intro Script", fileName = "IntroScript")]
    public class IntroScript : ScriptableObject
    {
        /// <summary>A line starting with this is a loudspeaker line.</summary>
        public const string LoudspeakerTag = "[LOUDSPEAKER]";

        [Header("Typing")]
        [Tooltip("Normal typing speed, characters per second.")]
        [Min(1f)] public float charsPerSecond = 35f;
        [Tooltip("Loudspeaker lines' typing speed, characters per second (no clicks).")]
        [Min(1f)] public float loudspeakerCharsPerSecond = 120f;
        [Tooltip("One typing click every this many characters (spaces don't count).")]
        [Min(1)] public int charsPerClick = 3;
        [Tooltip("Shortest time between two clicks, seconds. Each click also cuts off the one before, so they never pile up.")]
        [Min(0f)] public float minClickGap = 0.12f;
        [Tooltip("The typing click sound.")]
        public AudioClip clickClip;
        [Tooltip("Typing click volume (0-1), before the SFX setting.")]
        [Range(0f, 1f)] public float clickVolume = 0.18f;
        [Tooltip("Random pitch range for each click.")]
        public Vector2 clickPitch = new Vector2(0.92f, 1.04f);

        [Header("Between beats")]
        [Tooltip("Seconds the finished beat stays on screen.")]
        [Min(0f)] public float holdAfterBeat = 1.5f;
        [Tooltip("Seconds the beat takes to fade out.")]
        [Min(0f)] public float fadeOutSeconds = 0.6f;

        [Header("Title card")]
        [Tooltip("Seconds the title card takes to fade in.")]
        [Min(0f)] public float titleFadeIn = 1f;
        [Tooltip("Seconds the title card stays before the game starts.")]
        [Min(0f)] public float titleHold = 2f;

        [Header("Leaving")]
        [Tooltip("Seconds the loops still playing (wind, hub day) take to fade out once the game scene has started. " +
                 "The game's own ambience fades in at the same time, so the sound carries over.")]
        [Min(0f)] public float carryOverFade = 2.5f;

        [Header("Beats")]
        public List<IntroBeat> beats = new List<IntroBeat>();

        /// <summary>One typed line of a beat.</summary>
        public struct Line
        {
            /// <summary>The words, {name} already filled in.</summary>
            public string text;
            /// <summary>True for a [LOUDSPEAKER] line.</summary>
            public bool loudspeaker;
        }

        /// <summary>
        /// Splits a beat's text into lines: one per row, empty rows skipped, [LOUDSPEAKER] taken off
        /// and flagged, {name} / {NAME} filled with <paramref name="playerName"/>.
        /// </summary>
        public static List<Line> SplitLines(string text, string playerName)
        {
            var list = new List<Line>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (var row in text.Split('\n'))
            {
                string s = row.Trim();
                if (s.Length == 0) continue;
                bool loud = s.StartsWith(LoudspeakerTag, StringComparison.OrdinalIgnoreCase);
                if (loud) s = s.Substring(LoudspeakerTag.Length).Trim();
                list.Add(new Line { text = PlayerProfile.Fill(s, playerName), loudspeaker = loud });
            }
            return list;
        }
    }

    /// <summary>One screen of the intro.</summary>
    [Serializable]
    public class IntroBeat
    {
        [Tooltip("Only a name for the Inspector list.")]
        public string label = "Beat";
        [Tooltip("One row = one typed line. Start a row with [LOUDSPEAKER] for a loudspeaker line. " +
                 "{name} = the player's name, {NAME} = in capitals.")]
        [TextArea(3, 12)] public string text = "";
        [Tooltip("Typing speed for this beat: 1 = normal, 0.5 = half speed (the letter).")]
        [Min(0.05f)] public float speedScale = 1f;
        [Tooltip("Seconds of pause between two lines of this beat.")]
        [Min(0f)] public float pauseBetweenLines = 0.4f;
        [Tooltip("Extra seconds on a black screen after this beat has faded out.")]
        [Min(0f)] public float blackAfter = 0f;
        [Tooltip("Sounds started or stopped during this beat.")]
        public List<IntroCue> cues = new List<IntroCue>();
    }

    /// <summary>A sound change at one point of a beat.</summary>
    [Serializable]
    public class IntroCue
    {
        /// <summary>What the cue does.</summary>
        public enum Action
        {
            /// <summary>Plays the clip once.</summary>
            OneShot,
            /// <summary>Starts the clip looping, fading in.</summary>
            StartLoop,
            /// <summary>Fades out the loop playing this clip.</summary>
            StopLoop,
            /// <summary>Fades out every loop (silence).</summary>
            StopAllLoops
        }

        [Tooltip("OneShot = play once. StartLoop = loop it, fading in. StopLoop = fade out that clip's loop. StopAllLoops = silence.")]
        public Action action = Action.OneShot;
        [Tooltip("The sound (not needed for StopAllLoops).")]
        public AudioClip clip;
        [Tooltip("Fires just before this line is typed (0 = the beat's first line). A number past the last line " +
                 "fires once the beat's text is finished.")]
        [Min(0)] public int beforeLine;
        [Tooltip("Extra seconds after that moment (the text does not wait for it).")]
        [Min(0f)] public float delay;
        [Tooltip("Volume (0-1), before the Settings slider.")]
        [Range(0f, 1f)] public float volume = 0.7f;
        [Tooltip("Fade in (StartLoop) or fade out (StopLoop / StopAllLoops) seconds.")]
        [Min(0f)] public float fadeSeconds = 2f;
    }
}
