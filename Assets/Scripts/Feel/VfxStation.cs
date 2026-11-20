// ============================================================================
// VfxStation.cs
// One effect on display in FeelLab: a spot, a label and a Play button. Click Play to spawn
// the effect from the pool. Looping effects (rain, blowing sand) start and stop.
//
// DESIGN RULES: none (FeelLab tool only).
//
// HOW TO USE: made by Salok > Feel > Build FeelLab. To show another effect, duplicate a
// station and drag a different prefab into "Effect".
// HOW TO EDIT: move the station to move where the effect appears.
// ============================================================================

using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Spawns one effect prefab at this spot when Play is clicked (FeelLab).</summary>
    public class VfxStation : MonoBehaviour
    {
        [Tooltip("The effect prefab to show (from Assets/VFX/Effects).")]
        public GameObject effect;
        [Tooltip("Where it appears. Empty = this object's position.")]
        public Transform spawnPoint;
        [Tooltip("Also shake the camera when played (0 = no shake).")]
        [Range(0f, 1f)] public float shake;

        GameObject looping;

        /// <summary>Plays the effect (or starts / stops it if it loops).</summary>
        public void Play()
        {
            if (effect == null) return;
            if (looping != null && looping.activeSelf)
            {
                FeelPool.Release(looping);
                looping = null;
                return;
            }
            var at = spawnPoint != null ? spawnPoint : transform;
            var go = FeelPool.Spawn(effect, at.position, at.rotation);
            if (IsLooping(go)) looping = go;
            if (shake > 0f) CameraShake.Add(shake);
        }

        static bool IsLooping(GameObject go)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>();
            for (int i = 0; i < systems.Length; i++)
                if (systems[i].main.loop) return true;
            return false;
        }
    }
}
