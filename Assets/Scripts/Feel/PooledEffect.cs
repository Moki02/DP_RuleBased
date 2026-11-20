// ============================================================================
// PooledEffect.cs
// Goes on the top object of an effect prefab (dust puff, splash, sparkle...). When the
// effect is spawned through FeelPool it restarts its particles and sound, and when the
// last particle is gone it hides itself and goes back to the pool.
//
// DESIGN RULES: none (performance helper).
//
// HOW TO USE: already on every prefab in Assets/VFX/Effects. Spawn with FeelPool.Spawn.
// Looping effects (rain, blowing sand) never go back by themselves: call FeelPool.Release
// or turn the object off.
// HOW TO EDIT: "Extra Time" keeps the object alive a little longer (for a trailing sound).
// ============================================================================

using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Restarts an effect when spawned and returns it to FeelPool when it has finished.</summary>
    [DisallowMultipleComponent]
    public class PooledEffect : MonoBehaviour
    {
        [Tooltip("Seconds to wait after the particles finish before going back to the pool.")]
        [Min(0f)] public float extraTime = 0.1f;

        /// <summary>The prefab this copy was made from (set by FeelPool).</summary>
        [System.NonSerialized] public GameObject SourcePrefab;

        ParticleSystem[] systems;
        AudioSource[] sounds;
        bool loops;
        float lifetime;
        float timer = -1f;

        void Awake()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
            sounds = GetComponentsInChildren<AudioSource>(true);
            lifetime = 0f;
            loops = false;
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                if (main.loop) loops = true;
                float life = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;
                if (life > lifetime) lifetime = life;
            }
            for (int i = 0; i < sounds.Length; i++)
            {
                if (sounds[i].loop) loops = true;
                else if (sounds[i].clip != null && sounds[i].clip.length > lifetime) lifetime = sounds[i].clip.length;
            }
        }

        /// <summary>Restarts every particle system and sound on this effect.</summary>
        public void Play()
        {
            if (systems == null) Awake();
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Clear(true);
                systems[i].Play(true);
            }
            for (int i = 0; i < sounds.Length; i++)
                if (sounds[i].clip != null) sounds[i].Play();
            // Looping effects, and objects with no particles or sound (floating text, UI bursts),
            // look after themselves.
            bool selfManaged = loops || (systems.Length == 0 && sounds.Length == 0);
            timer = selfManaged ? -1f : lifetime + extraTime;
        }

        void Update()
        {
            if (timer < 0f) return;
            timer -= Time.deltaTime;
            if (timer < 0f) FeelPool.Release(gameObject);
        }
    }
}
