// ============================================================================
// FeelPool.cs
// Re-uses effect objects instead of making and destroying a new one every time (which is
// slow on the web build). Ask it for a copy of a prefab; when the effect is done it goes
// back in the pool, hidden, ready for next time.
//
// DESIGN RULES: none (performance helper).
//
// HOW TO USE (code):
//     FeelPool.Spawn(dustPuffPrefab, gate.position, Quaternion.identity);
// Particle effects return by themselves when they finish (PooledEffect sets this up).
// Anything else: call FeelPool.Release(obj) when done.
// HOW TO EDIT: nothing to tune. "Prewarm" makes copies ahead of time to avoid a hitch.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>A simple object pool for effects, keyed by prefab.</summary>
    public static class FeelPool
    {
        static readonly Dictionary<GameObject, Stack<GameObject>> free = new Dictionary<GameObject, Stack<GameObject>>();
        static Transform root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            free.Clear();
            root = null;
        }

        static Transform Root
        {
            get
            {
                if (root == null)
                {
                    var go = new GameObject("[FeelPool]");
                    root = go.transform;
                }
                return root;
            }
        }

        /// <summary>Makes <paramref name="count"/> hidden copies of the prefab ahead of time.</summary>
        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            var stack = StackFor(prefab);
            for (int i = stack.Count; i < count; i++)
            {
                var go = Create(prefab);
                go.SetActive(false);
                stack.Push(go);
            }
        }

        /// <summary>Takes a copy of <paramref name="prefab"/> from the pool (or makes one), places it and turns it on.</summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null) return null;
            var stack = StackFor(prefab);
            GameObject go = null;
            while (stack.Count > 0 && go == null) go = stack.Pop(); // skip copies destroyed with a scene
            if (go == null) go = Create(prefab);

            var t = go.transform;
            t.SetParent(parent != null ? parent : Root, false);
            t.SetPositionAndRotation(position, rotation);
            go.SetActive(true);

            var effect = go.GetComponent<PooledEffect>();
            if (effect != null) effect.Play();
            return go;
        }

        /// <summary>Puts a spawned copy back in the pool (hidden). Objects not made by the pool are destroyed.</summary>
        public static void Release(GameObject go)
        {
            if (go == null) return;
            var tag = go.GetComponent<PooledEffect>();
            if (tag == null || tag.SourcePrefab == null)
            {
                Object.Destroy(go);
                return;
            }
            go.SetActive(false);
            go.transform.SetParent(Root, false);
            StackFor(tag.SourcePrefab).Push(go);
        }

        static Stack<GameObject> StackFor(GameObject prefab)
        {
            if (!free.TryGetValue(prefab, out var stack))
            {
                stack = new Stack<GameObject>();
                free.Add(prefab, stack);
            }
            return stack;
        }

        static GameObject Create(GameObject prefab)
        {
            var go = Object.Instantiate(prefab, Root, false);
            go.name = prefab.name;
            var tag = go.GetComponent<PooledEffect>();
            if (tag == null) tag = go.AddComponent<PooledEffect>();
            tag.SourcePrefab = prefab;
            return go;
        }
    }
}
