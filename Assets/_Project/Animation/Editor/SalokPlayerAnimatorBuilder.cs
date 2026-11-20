// ============================================================================
// SalokPlayerAnimatorBuilder.cs  (Editor only)
// Builds and wires the player's Animator Controller, AC_Salok_Player, from Synty's
// AC_Polygon_Feminine (the pack's file is copied, never edited):
//   1. Checks the female model (Characters.fbx) is rigged Humanoid with a valid Avatar.
//   2. Copies the Synty controller to Assets/_Project/Animation/AC_Salok_Player.controller
//      (only if it isn't there yet, so an artist's later changes are kept).
//   3. Swaps any *_RootMotion_* or Masculine clip for the in-place Feminine one, and fills the
//      body-look blend tree whose clips are missing in the pack.
//   4. Adds LocomotionPlaybackSpeed (scales the locomotion and crouch blend trees only).
//   5. Adds the action layers: "Actions" (full body: Collapse, WakeUp, Pump, Pour) and
//      "UpperBodyActions" (Drink, so the player can walk while drinking). Each rests in "Empty".
//      Existing action states are left alone, so swapped-in clips survive a rebuild.
//   6. PlayerPrefab: this controller, Apply Root Motion off, the female Avatar.
//   7. Salok_Environment: removes the scene's Avatar override (it pointed at the old male
//      placeholder) and adds PlayerSyntyAnimator to the player.
//
// HOW TO USE: Salok > Animation > Build Player Animator (or batch:
// -executeMethod SalokPlayerAnimatorBuilder.BuildBatch). Safe to run again.
// HOW TO EDIT: paths are the constants below. Clip swaps: see Assets/_Project/Animation/README.md.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SalokPlayerAnimatorBuilder
{
    const string SourceController = "Assets/Art/AnimationBaseLocomotion/Animations/Polygon/AC_Polygon_Feminine.controller";
    const string Folder = "Assets/_Project/Animation";
    const string TargetController = Folder + "/AC_Salok_Player.controller";
    const string UpperBodyMaskPath = Folder + "/AM_Salok_UpperBody.mask";
    const string OldController = "Assets/Art/PlaceHolder/Animator/Player.controller";
    const string FemaleModel = "Assets/Art/Synty/PolygonApocalypseWasteland/Models/Characters/Characters.fbx";
    const string IdleClipFbx = "Assets/Art/AnimationBaseLocomotion/Animations/Polygon/Feminine/Idle/A_Idle_Standing_Femn.fbx";
    const string BodyLookFbx = "Assets/Art/AnimationBaseLocomotion/Animations/Polygon/Neutral/Additive/Look/A_BodyLook_Additive_Neut.fbx";
    const string PlayerPrefabPath = "Assets/Prefabs/Player Prefab/PlayerPrefab.prefab";
    const string GameScene = "Assets/Scenes/Salok_Environment.unity";

    public const string PlaybackParam = "LocomotionPlaybackSpeed";
    public const string ActionsLayer = "Actions";
    public const string UpperBodyLayer = "UpperBodyActions";
    public const string PlaceholderTag = "PLACEHOLDER";

    /// <summary>Seconds the WakeUp placeholder lasts before returning to locomotion.</summary>
    const float WakeUpPlaceholderSeconds = 1f;

    static readonly List<string> Log = new List<string>();

    [MenuItem("Salok/Animation/Build Player Animator")]
    public static void BuildMenu()
    {
        Build(true);
        EditorUtility.DisplayDialog("Player Animator", string.Join("\n", Log.Take(40)), "OK");
    }

    /// <summary>Batch entry point. Throws (non-zero exit) if something required is wrong.</summary>
    public static void BuildBatch()
    {
        Build(true);
    }

    /// <summary>Batch: only checks (no changes). Throws if a check fails.</summary>
    public static void VerifyBatch()
    {
        Log.Clear();
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetController);
        if (ac == null) throw new Exception("AC_Salok_Player missing");
        Verify(ac);
        Flush();
    }

    static void Build(bool includeScene)
    {
        Log.Clear();
        try
        {
            Avatar avatar = CheckFemaleModel();
            AnimatorController ac = CreateOrLoadController();

            RemapClips(ac);
            FixBodyLook(ac);
            AddPlaybackParameter(ac);
            AddActionLayers(ac);
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();

            WirePrefab(ac, avatar);
            if (includeScene)
                WireScene(ac, avatar);

            Verify(ac);
        }
        finally
        {
            Flush();
        }
    }

    static void Flush()
    {
        foreach (string line in Log)
            Debug.Log("[SalokAnimBuilder] " + line);
    }

    // ------------------------------------------------------------------ 1. Model

    static Avatar CheckFemaleModel()
    {
        var importer = AssetImporter.GetAtPath(FemaleModel) as ModelImporter;
        if (importer == null)
            throw new Exception("Female model not found: " + FemaleModel);

        if (importer.animationType != ModelImporterAnimationType.Human ||
            importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            Log.Add("Characters.fbx: rig set to Humanoid (create Avatar from this model) and reimported.");
        }
        else
        {
            Log.Add("Characters.fbx: rig is already Humanoid, Avatar created from this model.");
        }

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(FemaleModel).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            throw new Exception($"Characters.fbx Avatar is not a valid humanoid (found: {avatar}, valid: {avatar?.isValid}, human: {avatar?.isHuman}).");
        Log.Add($"Female Avatar '{avatar.name}': valid {avatar.isValid}, humanoid {avatar.isHuman}.");
        return avatar;
    }

    // ------------------------------------------------------------------ 2. Controller copy

    static AnimatorController CreateOrLoadController()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            AssetDatabase.CreateFolder("Assets", "_Project");
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/_Project", "Animation");

        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetController);
        if (ac != null)
        {
            Log.Add("AC_Salok_Player already exists: repairing/adding only (nothing overwritten).");
            return ac;
        }

        if (!AssetDatabase.CopyAsset(SourceController, TargetController))
            throw new Exception("Could not copy " + SourceController);
        AssetDatabase.ImportAsset(TargetController);
        ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetController);
        Log.Add("Copied AC_Polygon_Feminine to " + TargetController + ".");
        return ac;
    }

    // ------------------------------------------------------------------ 3. Clips

    static IEnumerable<AnimatorState> AllStates(AnimatorStateMachine sm)
    {
        foreach (var child in sm.states)
            yield return child.state;
        foreach (var sub in sm.stateMachines)
            foreach (var s in AllStates(sub.stateMachine))
                yield return s;
    }

    static IEnumerable<BlendTree> AllBlendTrees(Motion motion)
    {
        if (motion is BlendTree tree)
        {
            yield return tree;
            foreach (var child in tree.children)
                foreach (var t in AllBlendTrees(child.motion))
                    yield return t;
        }
    }

    static IEnumerable<BlendTree> AllBlendTrees(AnimatorController ac)
    {
        var seen = new HashSet<BlendTree>();
        foreach (var layer in ac.layers)
            foreach (var state in AllStates(layer.stateMachine))
                foreach (var tree in AllBlendTrees(state.motion))
                    if (seen.Add(tree)) yield return tree;
    }

    static string InPlaceName(string name)
    {
        string n = name.Replace("RootMotionVertical", "").Replace("RootMotion", "");
        while (n.Contains("__")) n = n.Replace("__", "_");
        return n.Replace("_Masc", "_Femn");
    }

    static Motion RemapClip(Motion motion, string where)
    {
        if (!(motion is AnimationClip clip))
            return motion;

        string path = AssetDatabase.GetAssetPath(clip);
        string file = Path.GetFileName(path);
        bool rootMotion = file.Contains("RootMotion");
        bool masculine = path.Contains("/Masculine/");
        if (!rootMotion && !masculine)
            return motion;

        string newPath = Path.GetDirectoryName(path).Replace('\\', '/').Replace("/Masculine/", "/Feminine/") + "/" + InPlaceName(file);
        var clips = AssetDatabase.LoadAllAssetsAtPath(newPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToList();
        AnimationClip replacement = clips.FirstOrDefault(c => c.name == InPlaceName(clip.name)) ??
                                    (clips.Count == 1 ? clips[0] : null);
        if (replacement == null)
        {
            Log.Add($"WARNING: no in-place/Feminine version of {file} ({where}); kept as is.");
            return motion;
        }

        Log.Add($"Clip swap in {where}: {file} -> {Path.GetFileName(newPath)}");
        return replacement;
    }

    static void RemapClips(AnimatorController ac)
    {
        foreach (var layer in ac.layers)
        {
            foreach (var state in AllStates(layer.stateMachine))
            {
                state.motion = RemapClip(state.motion, "state " + state.name);
                EditorUtility.SetDirty(state);
            }
        }

        foreach (var tree in AllBlendTrees(ac))
        {
            var children = tree.children;
            bool changed = false;
            for (int i = 0; i < children.Length; i++)
            {
                Motion m = RemapClip(children[i].motion, "blend tree " + tree.name);
                if (m != children[i].motion) { children[i].motion = m; changed = true; }
            }
            if (changed)
            {
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }
        }
    }

    // The pack's body-look blend tree points at clips that aren't in the pack: use the
    // A_BodyLook_Additive_Neut clips (L, R, U, D) and the same T-pose centre as the head look.
    static void FixBodyLook(AnimatorController ac)
    {
        var trees = AllBlendTrees(ac).ToList();
        BlendTree head = trees.FirstOrDefault(t => t.name == "HeadLook_BlendTree");
        BlendTree body = trees.FirstOrDefault(t => t.name == "BodyLook_BlendTree");

        if (head != null)
            foreach (var c in head.children)
                Log.Add($"HeadLook clip at ({c.position.x:0},{c.position.y:0}): {(c.motion != null ? c.motion.name : "MISSING")}");

        if (body == null)
        {
            Log.Add("WARNING: BodyLook_BlendTree not found.");
            return;
        }

        var bodyClips = AssetDatabase.LoadAllAssetsAtPath(BodyLookFbx).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToList();
        Motion centre = head?.children.FirstOrDefault(c => c.position == Vector2.zero).motion;

        var children = body.children;
        bool changed = false;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].motion != null)
                continue;

            Vector2 p = children[i].position;
            string suffix = p.x < -0.5f ? "_L_" : p.x > 0.5f ? "_R_" : p.y > 0.5f ? "_U_" : p.y < -0.5f ? "_D_" : null;
            Motion m = suffix != null ? bodyClips.FirstOrDefault(c => c.name.Contains(suffix)) : centre;
            if (m == null)
            {
                Log.Add($"WARNING: no clip found for body look at {p}.");
                continue;
            }
            children[i].motion = m;
            changed = true;
            Log.Add($"BodyLook fix: ({p.x:0},{p.y:0}) -> {m.name}");
        }
        if (changed)
        {
            body.children = children;
            EditorUtility.SetDirty(body);
        }
    }

    // ------------------------------------------------------------------ 4. Playback speed

    static void AddPlaybackParameter(AnimatorController ac)
    {
        if (!ac.parameters.Any(p => p.name == PlaybackParam))
        {
            ac.AddParameter(new AnimatorControllerParameter
            {
                name = PlaybackParam,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f
            });
            Log.Add("Added parameter " + PlaybackParam + " (default 1).");
        }

        foreach (var state in AllStates(ac.layers[0].stateMachine))
        {
            if (state.name != "LocomotionBlendTree" && state.name != "CrouchingBlendTree")
                continue;
            state.speedParameterActive = true;
            state.speedParameter = PlaybackParam;
            EditorUtility.SetDirty(state);
            Log.Add($"State {state.name}: speed × {PlaybackParam}.");
        }
    }

    // ------------------------------------------------------------------ 5. Actions

    static AvatarMask GetOrCreateUpperBodyMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
        if (mask != null)
            return mask;

        mask = new AvatarMask();
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
        AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
        Log.Add("Created upper-body Avatar Mask " + UpperBodyMaskPath + ".");
        return mask;
    }

    static int GetOrAddLayer(AnimatorController ac, string name, AvatarMask mask)
    {
        var layers = ac.layers;
        for (int i = 0; i < layers.Length; i++)
            if (layers[i].name == name)
                return i;

        var sm = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(sm, ac);
        ac.AddLayer(new AnimatorControllerLayer
        {
            name = name,
            stateMachine = sm,
            defaultWeight = 0f,             // PlayerSyntyAnimator fades it in while an action plays
            blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = mask
        });
        Log.Add($"Added layer {name}{(mask != null ? " (upper-body mask)" : " (full body)")}.");
        return ac.layers.Length - 1;
    }

    static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        return AllStates(sm).FirstOrDefault(s => s.name == name);
    }

    static AnimatorState GetOrAddState(AnimatorStateMachine sm, string name, Motion motion, bool writeDefaults,
                                       Vector3 position, string tag, out bool created)
    {
        var state = FindState(sm, name);
        created = state == null;
        if (state != null)
            return state;

        state = sm.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = writeDefaults;
        if (!string.IsNullOrEmpty(tag))
            state.tag = tag;
        return state;
    }

    static void AddExitToEmpty(AnimatorState from, AnimatorState empty, float exitTime, float duration)
    {
        if (from.transitions.Any(t => t.destinationState == empty))
            return;
        var t = from.AddTransition(empty);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = duration;
        t.interruptionSource = TransitionInterruptionSource.None;
    }

    static Motion OldMotion(string stateName)
    {
        var old = AssetDatabase.LoadAssetAtPath<AnimatorController>(OldController);
        if (old == null) return null;
        foreach (var layer in old.layers)
        {
            var s = FindState(layer.stateMachine, stateName);
            if (s != null) return s.motion;
        }
        return null;
    }

    static void AddActionLayers(AnimatorController ac)
    {
        AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(IdleClipFbx).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (idle == null) throw new Exception("Idle clip not found: " + IdleClipFbx);

        Motion drink = OldMotion("Drink");
        Motion collapse = OldMotion("Collapse");
        if (drink == null) Log.Add("WARNING: old Drink clip not found; Drink uses the idle placeholder.");
        if (collapse == null) Log.Add("WARNING: old Collapse clip not found; Collapse uses the idle placeholder.");

        // Full-body actions (the player can't walk during these).
        int full = GetOrAddLayer(ac, ActionsLayer, null);
        var fullSm = ac.layers[full].stateMachine;
        var empty = GetOrAddState(fullSm, PlayerSyntyAnimator.EmptyState, null, false, new Vector3(300, 0), null, out _);
        fullSm.defaultState = empty;

        GetOrAddState(fullSm, "Collapse", collapse ?? idle, true, new Vector3(300, 120), null, out bool c1);
        if (c1) Log.Add("Actions/Collapse: old Player.controller clip (stays down until the revive rebinds the Animator).");

        var wake = GetOrAddState(fullSm, "WakeUp", idle, true, new Vector3(550, 120), PlaceholderTag, out bool c2);
        if (c2)
        {
            // PLACEHOLDER: the idle clip, cut to 1 s. Swap in a get-up clip and set this
            // transition's Exit Time to ~0.9 (see README).
            AddExitToEmpty(wake, empty, WakeUpPlaceholderSeconds / Mathf.Max(0.1f, idle.length), 0.25f);
            Log.Add("Actions/WakeUp: PLACEHOLDER (idle clip, 1 s), returns to Empty by itself.");
        }

        GetOrAddState(fullSm, "Pump", idle, true, new Vector3(50, 120), PlaceholderTag, out bool c3);
        if (c3) Log.Add("Actions/Pump: PLACEHOLDER (idle clip, loops; code returns when E is released).");

        GetOrAddState(fullSm, "Pour", idle, true, new Vector3(50, 240), PlaceholderTag, out bool c4);
        if (c4) Log.Add("Actions/Pour: PLACEHOLDER (idle clip, loops; code returns when E is released).");

        // Upper body only: drinking while walking.
        var mask = GetOrCreateUpperBodyMask();
        int upper = GetOrAddLayer(ac, UpperBodyLayer, mask);
        var upperSm = ac.layers[upper].stateMachine;
        var upperEmpty = GetOrAddState(upperSm, PlayerSyntyAnimator.EmptyState, null, false, new Vector3(300, 0), null, out _);
        upperSm.defaultState = upperEmpty;

        var drinkState = GetOrAddState(upperSm, "Drink", drink ?? idle, true, new Vector3(300, 120), drink == null ? PlaceholderTag : null, out bool c5);
        if (c5)
        {
            AddExitToEmpty(drinkState, upperEmpty, 0.95f, 0.15f);
            Log.Add("UpperBodyActions/Drink: old Player.controller clip (Consume, keeps its EndDrinkAnimation event).");
        }
    }

    // ------------------------------------------------------------------ 6. Prefab

    static void WirePrefab(AnimatorController ac, Avatar avatar)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null)
                throw new Exception("PlayerPrefab has no Animator on its root.");
            animator.runtimeAnimatorController = ac;
            animator.applyRootMotion = false;
            animator.avatar = avatar;
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Log.Add("PlayerPrefab: controller AC_Salok_Player, Apply Root Motion off, Avatar " + avatar.name + ".");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------------ 7. Scene

    static void WireScene(AnimatorController ac, Avatar avatar)
    {
        var scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        var players = UnityEngine.Object.FindObjectsByType<Player_Manager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (players.Length == 0)
            throw new Exception("No Player_Manager in " + GameScene);

        foreach (var pm in players)
        {
            var animator = pm.GetComponent<Animator>();
            if (animator == null)
            {
                Log.Add($"WARNING: {pm.name} has no Animator.");
                continue;
            }

            // Drop scene overrides so the prefab's values (female Avatar, new controller) apply.
            if (PrefabUtility.IsPartOfPrefabInstance(animator))
            {
                var so = new SerializedObject(animator);
                foreach (string prop in new[] { "m_Avatar", "m_Controller", "m_ApplyRootMotion" })
                {
                    var sp = so.FindProperty(prop);
                    if (sp != null && sp.prefabOverride)
                    {
                        PrefabUtility.RevertPropertyOverride(sp, InteractionMode.AutomatedAction);
                        Log.Add($"Scene {pm.name}: reverted the {prop} override.");
                    }
                }
            }

            animator.runtimeAnimatorController = ac;
            animator.applyRootMotion = false;
            animator.avatar = avatar;

            if (pm.GetComponent<PlayerSyntyAnimator>() == null)
            {
                pm.gameObject.AddComponent<PlayerSyntyAnimator>();
                Log.Add($"Scene {pm.name}: added PlayerSyntyAnimator.");
            }
            EditorUtility.SetDirty(animator);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Log.Add("Saved " + GameScene + ".");
    }

    // ------------------------------------------------------------------ Checks

    static void Verify(AnimatorController ac)
    {
        var problems = new List<string>();

        // Every clip: Feminine or Neutral, in-place.
        var clips = new HashSet<AnimationClip>();
        foreach (var layer in ac.layers)
            foreach (var state in AllStates(layer.stateMachine))
            {
                if (state.motion is AnimationClip c) clips.Add(c);
                foreach (var tree in AllBlendTrees(state.motion))
                    foreach (var child in tree.children)
                    {
                        if (child.motion is AnimationClip cc) clips.Add(cc);
                        else if (child.motion == null) problems.Add($"missing clip in blend tree {tree.name} at {child.position}");
                    }
            }
        foreach (var c in clips)
        {
            string p = AssetDatabase.GetAssetPath(c);
            if (p.Contains("RootMotion")) problems.Add("root-motion clip still used: " + p);
            if (p.Contains("/Masculine/")) problems.Add("masculine clip still used: " + p);
        }
        Log.Add($"Clips used: {clips.Count} ({clips.Count(c => AssetDatabase.GetAssetPath(c).Contains("/Feminine/"))} Feminine, " +
                $"{clips.Count(c => AssetDatabase.GetAssetPath(c).Contains("/Neutral/"))} Neutral, " +
                $"{clips.Count(c => AssetDatabase.GetAssetPath(c).Contains("/PlaceHolder/"))} old placeholder).");

        foreach (string param in new[] { "MoveSpeed", "CurrentGait", "IsStopped", "IsStarting", "LocomotionStartDirection",
                     "MovementInputTapped", "MovementInputPressed", "MovementInputHeld", "IsTurningInPlace", "IsGrounded",
                     "FallingDuration", "IsCrouching", "IsJumping", "IsWalking", "IsStrafing", "LeanValue", "HeadLookX",
                     "HeadLookY", "BodyLookX", "BodyLookY", "CameraRotationOffset", PlaybackParam })
            if (!ac.parameters.Any(p => p.name == param)) problems.Add("missing parameter " + param);

        foreach (var (layerName, states) in new[] {
                     (ActionsLayer, new[] { "Empty", "Collapse", "WakeUp", "Pump", "Pour" }),
                     (UpperBodyLayer, new[] { "Empty", "Drink" }) })
        {
            var layer = ac.layers.FirstOrDefault(l => l.name == layerName);
            if (layer == null) { problems.Add("missing layer " + layerName); continue; }
            foreach (string s in states)
                if (FindState(layer.stateMachine, s) == null) problems.Add($"missing state {layerName}/{s}");
        }

        // Drink keeps its animation event.
        var drinkLayer = ac.layers.FirstOrDefault(l => l.name == UpperBodyLayer);
        var drinkClip = drinkLayer != null ? FindState(drinkLayer.stateMachine, "Drink")?.motion as AnimationClip : null;
        bool hasEndEvent = drinkClip != null && AnimationUtility.GetAnimationEvents(drinkClip).Any(e => e.functionName == "EndDrinkAnimation");
        if (!hasEndEvent) problems.Add("Drink clip has no EndDrinkAnimation event");
        else Log.Add("Drink clip " + drinkClip.name + " has the EndDrinkAnimation event.");

        // The prefab.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        var pa = prefab != null ? prefab.GetComponent<Animator>() : null;
        if (pa == null || pa.runtimeAnimatorController != ac) problems.Add("PlayerPrefab Animator doesn't use AC_Salok_Player");
        else if (pa.applyRootMotion) problems.Add("PlayerPrefab Animator has Apply Root Motion on");
        else if (pa.avatar == null || !pa.avatar.isHuman) problems.Add("PlayerPrefab Avatar isn't humanoid");
        else Log.Add($"PlayerPrefab OK: AC_Salok_Player, root motion off, Avatar {pa.avatar.name}.");

        if (problems.Count > 0)
        {
            foreach (string p in problems) Log.Add("PROBLEM: " + p);
            throw new Exception("Player animator check failed: " + string.Join("; ", problems));
        }
        Log.Add("All checks passed.");
    }
}
