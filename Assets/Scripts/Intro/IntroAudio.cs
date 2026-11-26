// ============================================================================
// IntroAudio.cs
// The intro's sounds: typing clicks, one-shots and a few looping channels that fade in and
// out (wind, tension, hub day). Every AudioSource has a SettingsVolume, so the Settings
// sliders work: clicks and one-shots follow SFX, loops follow Music.
// When the intro ends, CarryOver keeps the loops alive across the scene change and fades
// them out while the game's own ambience (AmbienceDirector) fades in, so the hub-day sound
// carries on instead of cutting.
//
// DESIGN RULES: none (intro).
//
// HOW TO USE: on the "Intro Audio" object in the Intro scene (the Build Intro Scene menu
// makes it with its children). IntroDirector calls it.
// HOW TO EDIT: the AudioSources are children you can see; clips and volumes are in the
// IntroScript asset.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Typing clicks, one-shots and fading loops for the intro.</summary>
    public class IntroAudio : MonoBehaviour
    {
        [Tooltip("Plays the typing clicks (SettingsVolume: Sfx).")]
        public AudioSource clicks;
        [Tooltip("Plays the one-shot cues (SettingsVolume: Sfx).")]
        public AudioSource oneShots;
        [Tooltip("Looping channels for ambience cues (SettingsVolume: Music). Three is enough for the script.")]
        public AudioSource[] loops = new AudioSource[0];

        // Per loop channel: fade state (no allocation while fading).
        SettingsVolume[] loopVolumes;
        float[] level, target, speed;
        SettingsVolume clicksVolume, oneShotVolume;
        bool carrying;

        void Awake()
        {
            clicksVolume = Ensure(clicks, SettingsVolume.Kind.Sfx);
            oneShotVolume = Ensure(oneShots, SettingsVolume.Kind.Sfx);
            int n = loops != null ? loops.Length : 0;
            loopVolumes = new SettingsVolume[n];
            level = new float[n];
            target = new float[n];
            speed = new float[n];
            for (int i = 0; i < n; i++)
            {
                loopVolumes[i] = Ensure(loops[i], SettingsVolume.Kind.Music);
                if (loops[i] == null) continue;
                loops[i].loop = true;
                loops[i].playOnAwake = false;
                loopVolumes[i].BaseVolume = 0f;
            }
            if (clicksVolume != null) clicksVolume.BaseVolume = 1f;
            if (oneShotVolume != null) oneShotVolume.BaseVolume = 1f;
        }

        static SettingsVolume Ensure(AudioSource s, SettingsVolume.Kind kind)
        {
            if (s == null) return null;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            var v = s.GetComponent<SettingsVolume>();
            if (v == null) { v = s.gameObject.AddComponent<SettingsVolume>(); v.kind = kind; }
            return v;
        }

        /// <summary>One typing click at <paramref name="volume"/> with a random pitch in <paramref name="pitch"/>.</summary>
        public void Click(AudioClip clip, float volume, Vector2 pitch)
        {
            if (clicks == null || clip == null) return;
            clicks.pitch = Random.Range(pitch.x, pitch.y);
            clicks.PlayOneShot(clip, volume);
        }

        /// <summary>Plays <paramref name="clip"/> once.</summary>
        public void OneShot(AudioClip clip, float volume)
        {
            if (oneShots == null || clip == null) return;
            oneShots.PlayOneShot(clip, volume);
        }

        /// <summary>Starts <paramref name="clip"/> looping (or re-targets it if it is already playing), fading to <paramref name="volume"/>.</summary>
        public void StartLoop(AudioClip clip, float volume, float fade)
        {
            if (clip == null) return;
            int i = Find(clip);
            if (i < 0) i = FreeChannel();
            if (i < 0) { Debug.LogWarning("[IntroAudio] No free loop channel for " + clip.name + ". Add another loop AudioSource.", this); return; }
            var s = loops[i];
            if (s.clip != clip || !s.isPlaying)
            {
                s.clip = clip;
                level[i] = 0f;
                loopVolumes[i].BaseVolume = 0f;
                s.Play();
            }
            SetFade(i, volume, fade);
        }

        /// <summary>Fades out the loop playing <paramref name="clip"/>.</summary>
        public void StopLoop(AudioClip clip, float fade)
        {
            int i = Find(clip);
            if (i >= 0) SetFade(i, 0f, fade);
        }

        /// <summary>Fades out every loop.</summary>
        public void StopAllLoops(float fade)
        {
            for (int i = 0; i < Count; i++) SetFade(i, 0f, fade);
        }

        /// <summary>
        /// The intro is over: this object survives the scene change and fades every loop out over
        /// <paramref name="fade"/> seconds, then removes itself.
        /// </summary>
        public void CarryOver(float fade)
        {
            if (carrying) return;
            carrying = true;
            transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);
            if (clicks != null) clicks.Stop();
            StopAllLoops(fade);
        }

        int Count => loops != null ? loops.Length : 0;

        int Find(AudioClip clip)
        {
            if (clip == null) return -1;
            for (int i = 0; i < Count; i++)
                if (loops[i] != null && loops[i].clip == clip && loops[i].isPlaying) return i;
            return -1;
        }

        int FreeChannel()
        {
            for (int i = 0; i < Count; i++)
                if (loops[i] != null && (!loops[i].isPlaying || (level[i] <= 0f && target[i] <= 0f))) return i;
            return -1;
        }

        void SetFade(int i, float to, float seconds)
        {
            target[i] = to;
            float distance = Mathf.Abs(to - level[i]);
            speed[i] = seconds > 0f ? Mathf.Max(distance, 0.0001f) / seconds : 1000f;
        }

        void Update()
        {
            // Real time: the game scene may start frozen (How to Play card) during the carry-over.
            float dt = AudioListener.pause ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool anyPlaying = false;
            for (int i = 0; i < Count; i++)
            {
                var s = loops[i];
                if (s == null || !s.isPlaying) continue;
                if (level[i] != target[i])
                {
                    level[i] = Mathf.MoveTowards(level[i], target[i], speed[i] * dt);
                    loopVolumes[i].BaseVolume = level[i];
                }
                if (level[i] <= 0f && target[i] <= 0f) { s.Stop(); continue; }
                anyPlaying = true;
            }
            if (carrying && !anyPlaying && (oneShots == null || !oneShots.isPlaying)) Destroy(gameObject);
        }
    }
}
