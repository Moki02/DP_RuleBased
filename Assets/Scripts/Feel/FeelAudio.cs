// ============================================================================
// FeelAudio.cs
// Plays the game-feel sounds. One-shots come from a small pool of AudioSources (made once,
// re-used: no garbage on the web build): 2D for UI and world events, 3D at a position for
// things in the world (pump, gates, footsteps). Loops (pumping, boiling, ambience) use
// FeelLoop, which fades in and out instead of cutting.
// Every sound follows the player's Settings: SFX volume, or Music volume for ambience.
//
// DESIGN RULES: none (feel only).
//
// HOW TO USE (code):
//     FeelAudio.Play(library.uiClick);                       // 2D
//     FeelAudio.PlayAt(library.gateSlam, gate.position);     // 3D, quieter far away
//     var loop = new FeelLoop(gameObject, library.boilLoop, music: false);
//     loop.Update(isBoiling, Time.unscaledDeltaTime);        // every frame
// The pool is made by itself the first time a sound plays.
// HOW TO EDIT: 3D distances are the constants below. Volumes are in GameFeelLibrary.
// ============================================================================

using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Pooled one-shot sound player for the game-feel sounds.</summary>
    public class FeelAudio : MonoBehaviour
    {
        /// <summary>A 3D sound is at full volume within this many metres.</summary>
        public const float NearDistance = 3f;
        /// <summary>A 3D sound fades out completely at this many metres.</summary>
        public const float FarDistance = 35f;
        const int PoolSize3D = 10;

        static FeelAudio instance;

        AudioSource source2D;
        AudioSource[] pool3D;
        int next3D;

        static FeelAudio Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[FeelAudio]");
                    instance = go.AddComponent<FeelAudio>();
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        void Awake()
        {
            source2D = gameObject.AddComponent<AudioSource>();
            source2D.playOnAwake = false;
            source2D.spatialBlend = 0f;

            pool3D = new AudioSource[PoolSize3D];
            for (int i = 0; i < PoolSize3D; i++)
            {
                var child = new GameObject("3D " + i);
                child.transform.SetParent(transform, false);
                pool3D[i] = Make3D(child);
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Sets up an AudioSource as a 3D game-world sound.</summary>
        public static AudioSource Make3D(GameObject host)
        {
            var s = host.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = NearDistance;
            s.maxDistance = FarDistance;
            s.dopplerLevel = 0f;
            return s;
        }

        /// <summary>The player's SFX volume setting (0..1).</summary>
        public static float SfxSetting => GameSettings.SfxVolume;

        /// <summary>The player's Music volume setting (0..1); used for ambience.</summary>
        public static float MusicSetting => GameSettings.MusicVolume;

        /// <summary>Plays a 2D sound (UI, world events). <paramref name="volumeScale"/> multiplies the library volume.</summary>
        public static void Play(FeelSound sound, float volumeScale = 1f)
        {
            if (sound == null || !sound.IsSet) return;
            Instance.source2D.PlayOneShot(sound.clip, sound.volume * volumeScale * SfxSetting);
        }

        /// <summary>Plays a 3D sound at <paramref name="position"/> (quieter further from the listener).</summary>
        public static void PlayAt(FeelSound sound, Vector3 position, float pitch = 1f, float volumeScale = 1f)
        {
            if (sound == null || !sound.IsSet) return;
            var self = Instance;
            var s = self.pool3D[self.next3D];
            self.next3D = (self.next3D + 1) % self.pool3D.Length;
            s.transform.position = position;
            s.pitch = pitch;
            s.clip = sound.clip;
            s.volume = sound.volume * volumeScale * SfxSetting;
            s.Play();
        }
    }

    /// <summary>
    /// A looping sound that fades in while something is happening and fades out when it stops.
    /// Not a component: the owner calls Update every frame.
    /// </summary>
    public class FeelLoop
    {
        readonly AudioSource source;
        readonly FeelSound sound;
        readonly bool music;
        float fade;

        /// <summary>Seconds to fade fully in.</summary>
        public float fadeIn = 0.25f;
        /// <summary>Seconds to fade fully out.</summary>
        public float fadeOut = 0.4f;

        /// <summary>How far faded in (0..1).</summary>
        public float Fade => fade;

        /// <summary>Makes a loop on <paramref name="host"/>. <paramref name="spatial"/> = true for a 3D sound at the host.
        /// <paramref name="music"/> = true to follow the Music volume (ambience).</summary>
        public FeelLoop(GameObject host, FeelSound sound, bool music = false, bool spatial = false)
        {
            this.sound = sound;
            this.music = music;
            source = spatial ? FeelAudio.Make3D(host) : host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            if (!spatial) source.spatialBlend = 0f;
            source.volume = 0f;
            if (sound != null) source.clip = sound.clip;
        }

        /// <summary>The AudioSource (to move a 3D loop).</summary>
        public AudioSource Source => source;

        /// <summary>
        /// Call every frame. <paramref name="on"/> = should it be playing; <paramref name="level"/> (0..1)
        /// scales the volume (e.g. louder at lower HYD); <paramref name="pitch"/> sets the pitch.
        /// </summary>
        public void Update(bool on, float dt, float level = 1f, float pitch = 1f)
        {
            if (sound == null || !sound.IsSet) return;
            float target = on ? 1f : 0f;
            float speed = on ? (fadeIn > 0f ? 1f / fadeIn : 1000f) : (fadeOut > 0f ? 1f / fadeOut : 1000f);
            fade = Mathf.MoveTowards(fade, target, speed * dt);

            if (fade <= 0f)
            {
                if (source.isPlaying) source.Stop();
                return;
            }
            if (!source.isPlaying)
            {
                source.clip = sound.clip;
                source.Play();
            }
            float setting = music ? FeelAudio.MusicSetting : FeelAudio.SfxSetting;
            source.volume = sound.volume * fade * Mathf.Clamp01(level) * setting;
            source.pitch = pitch;
        }

        /// <summary>Stops straight away.</summary>
        public void Stop()
        {
            fade = 0f;
            if (source != null) source.Stop();
        }
    }
}
