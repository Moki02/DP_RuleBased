// ============================================================================
// AutoSystems.cs
// Adds game systems to a scene by themselves, so new systems don't need anyone to edit
// the scene or the Build Playtest Scene menu. A system asks once:
//
//     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//     static void Register() => AutoSystems.Register<MySystem>();
//
// Then, every time a scene loads that has a BalanceProvider (the maze game scenes),
// MySystem is added to the "Systems" object if it isn't in the scene yet. Old scenes
// with no BalanceProvider (Zone1Map, TestingScene) are left alone.
//
// DESIGN RULES: none of its own (plumbing).
//
// HOW TO USE: call Register<T>() from a [RuntimeInitializeOnLoadMethod] as above.
// HOW TO EDIT: nothing to tune. To stop a system being added, remove its Register line.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastDrop
{
    /// <summary>Adds registered systems to every game scene (scenes with a BalanceProvider).</summary>
    public static class AutoSystems
    {
        static readonly List<Type> types = new List<Type>();
        static bool hooked;

        /// <summary>The name of the object the systems go on.</summary>
        public const string SystemsObjectName = "Systems";

        /// <summary>Asks for <typeparamref name="T"/> to be added to every game scene.</summary>
        public static void Register<T>() where T : MonoBehaviour
        {
            if (!types.Contains(typeof(T))) types.Add(typeof(T));
            if (hooked) return;
            hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Only game scenes: they have a BalanceProvider (set up by the Build Playtest Scene menu).
            if (UnityEngine.Object.FindAnyObjectByType<BalanceProvider>() == null) return;
            var systems = GameObject.Find(SystemsObjectName);
            if (systems == null) systems = new GameObject(SystemsObjectName);
            foreach (var t in types)
                if (UnityEngine.Object.FindAnyObjectByType(t) == null)
                    systems.AddComponent(t);
        }
    }

    /// <summary>Short ways to read Build and Skills effects (works even with no UpgradeManager).</summary>
    public static class Skills
    {
        /// <summary>An effect number from owned upgrades, or <paramref name="fallback"/>.</summary>
        public static float F(string field, float fallback) =>
            UpgradeManager.Instance != null ? UpgradeManager.Instance.GetFloat(field, fallback) : fallback;

        /// <summary>A true/false effect from owned upgrades, or <paramref name="fallback"/>.</summary>
        public static bool B(string field, bool fallback) =>
            UpgradeManager.Instance != null ? UpgradeManager.Instance.GetBool(field, fallback) : fallback;

        /// <summary>True if this Build item or skill is owned.</summary>
        public static bool Owns(string id) =>
            UpgradeManager.Instance != null && UpgradeManager.Instance.Logic != null && UpgradeManager.Instance.Logic.Owns(id);
    }
}
