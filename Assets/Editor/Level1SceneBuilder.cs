using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using static BuilderUtils;

// PacStudent > Build Level1 Scene creates the animation clips, Animator Controllers, pellet prefabs and the
// Level1 scene (full level layout from LevelGenerator, PacStudent, the sharks and the background music).
// Re-running it rebuilds Level1 from scratch. PacStudent > Assign Audio Clips only re-links the audio.
public static class Level1SceneBuilder
{
    const string TilesDir = "Assets/Tiles";
    const string AnimDir = "Assets/Animation";
    const string PrefabDir = "Assets/Prefabs";
    const string ScenePath = "Assets/Scenes/Level1.unity";

    // Ghost 1..4
    static readonly string[] SharkNames = { "Bruce", "Jeff", "Kenny", "Lenny" };
    static readonly string[] Directions = { "Right", "Down", "Left", "Up" };

    static readonly List<string> warnings = new List<string>();

    [MenuItem("PacStudent/Build Level1 Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        warnings.Clear();

        RuntimeAnimatorController pacController = BuildPacStudentAnimator();
        RuntimeAnimatorController[] ghostControllers = SharkNames.Select(BuildGhostAnimator).ToArray();
        RuntimeAnimatorController powerController = BuildPowerPelletAnimator();
        GameObject pelletPrefab = BuildPelletPrefab("Pellet", LoadSprite(SpritesDir + "/Items/Pellet.png"), null);
        GameObject powerPrefab = BuildPelletPrefab("PowerPellet", LoadSprite(SpritesDir + "/Items/PowerPellet_01.png"), powerController);

        BuildScene(pacController, ghostControllers, pelletPrefab, powerPrefab);
        AssignAudioClips();

        SetBuildScenes();
        AssetDatabase.SaveAssets();

        string report = "Level1 scene built.";
        if (warnings.Count > 0) report += "\nStill to do:\n - " + string.Join("\n - ", warnings);
        Debug.Log(report);
    }

    // ---------------------------------------------------------------- Audio

    [MenuItem("PacStudent/Assign Audio Clips")]
    public static void AssignAudioClips()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);

        List<AudioClip> clips = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir })
            .Select(guid => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)))
            .ToList();
        AudioClip Find(string clipName)
        {
            AudioClip clip = clips.FirstOrDefault(c => string.Equals(c.name, clipName, System.StringComparison.OrdinalIgnoreCase));
            if (clip == null) Debug.LogWarning($"Audio clip '{clipName}' missing from {AudioDir}");
            return clip;
        }

        BackgroundMusicController music = Object.FindAnyObjectByType<BackgroundMusicController>();
        music.introMusic = Find("Music_Intro");
        music.normalMusic = Find("Music_GhostNormal");
        // Not played by the A3 scene, but they must be in the Audio Clips folder
        foreach (string other in new[] { "Music_StartScene", "Music_GhostScared", "Music_GhostDead", "SFX_Move", "SFX_EatPellet",
                     "SFX_EatGhost", "SFX_EatCherry", "SFX_WallCollision", "SFX_Death" })
            Find(other);

        EditorUtility.SetDirty(music);
        EditorSceneManager.SaveScene(scene);
    }

    // ---------------------------------------------------------------- Animation

    // The A3 "...Animator" controllers cycle through all of their states so each one can be previewed. They are kept in
    // the project, but the scene uses a copy without the cycle transitions so the game scripts choose the state.
    static RuntimeAnimatorController BuildPacStudentAnimator()
    {
        string dir = AnimDir + "/PacStudent/";
        string sprites = SpritesDir + "/PacStudent/PacStudent_";
        List<(string, AnimationClip)> states = new List<(string, AnimationClip)>();
        foreach (string direction in Directions)
            states.Add(("Walk_" + direction, CreateClip($"{dir}PacStudent_Walk_{direction}.anim", LoadFrames(sprites + direction + "_"), 10f, true)));
        states.Add(("Dead", CreateClip(dir + "PacStudent_Dead.anim", LoadFrames(sprites + "Dead_"), 3f, false)));
        CreateCyclingController(dir + "PacStudentAnimator.controller", states);
        return CreateController(dir + "PacStudent.controller", states);
    }

    static RuntimeAnimatorController BuildGhostAnimator(string shark)
    {
        string dir = $"{AnimDir}/Ghosts/{shark}/";
        EnsureFolder(dir.TrimEnd('/'));
        string sprites = $"{SpritesDir}/Ghosts/Shark_{shark}_";
        Color scaredBlue = new Color(0.35f, 0.5f, 1f);
        Color deadGhost = new Color(0.75f, 0.85f, 1f, 0.35f);
        List<(string, AnimationClip)> states = new List<(string, AnimationClip)>();

        foreach (string direction in Directions)
            states.Add(("Walk_" + direction, CreateClip($"{dir}{shark}_Walk_{direction}.anim", LoadFrames(sprites + direction + "_"), 8f, true)));

        // Hand-drawn scared/dead frames are used when they exist (per shark, or shared by all sharks);
        // otherwise the swimming frames are tinted as a stand-in
        Sprite[] scaredLeft = null;
        foreach (string direction in Directions)
        {
            Sprite[] scared = FirstFound(sprites + "Scared_" + direction + "_", $"{SpritesDir}/Ghosts/Shark_Scared_{direction}_");
            if (direction == "Left") scaredLeft = scared;
            states.Add(("Scared_" + direction, scared.Length > 0
                ? CreateClip($"{dir}{shark}_Scared_{direction}.anim", scared, 8f, true)
                : CreateClip($"{dir}{shark}_Scared_{direction}.anim", LoadFrames(sprites + direction + "_"), 6f, true, scaredBlue, scaredBlue, 1)));
        }
        if (scaredLeft.Length == 0) warnings.Add($"Shark_{shark}: no scared sprites yet, using blue-tinted swimming frames (draw Shark_Scared_Left_01.png etc.)");

        Sprite[] recoveringFrames = scaredLeft.Length > 0 ? scaredLeft : LoadFrames(sprites + "Left_");
        states.Add(("Recovering", CreateClip($"{dir}{shark}_Recovering.anim", recoveringFrames, 6f, true, scaredBlue, Color.white, 4)));

        Sprite[] dead = FirstFound(sprites + "Dead_", SpritesDir + "/Ghosts/Shark_Dead_");
        if (dead.Length == 0) warnings.Add($"Shark_{shark}: no dead sprites yet, using see-through swimming frames");
        states.Add(("Dead", dead.Length > 0
            ? CreateClip($"{dir}{shark}_Dead.anim", dead, 6f, true)
            : CreateClip($"{dir}{shark}_Dead.anim", LoadFrames(sprites + "Left_"), 4f, true, deadGhost, deadGhost, 1)));

        CreateCyclingController($"{AnimDir}/Ghosts/GhostAnimator_{shark}.controller", states);
        return CreateController($"{AnimDir}/Ghosts/Ghost_{shark}.controller", states);
    }

    static RuntimeAnimatorController BuildPowerPelletAnimator()
    {
        string path = AnimDir + "/Pellets/PowerPellet_Flash.anim";
        AnimationClip clip = ReuseClip(path, 30f);

        Sprite[] frames = LoadFrames(SpritesDir + "/Items/PowerPellet_");
        if (frames.Length > 1) SetSpriteCurve(clip, frames, frames.Length / 0.6f);
        else warnings.Add("Power pellet has only 1 frame; draw PowerPellet_02.png (needs 2 visually distinct frames)");

        // Pulse and flash on top of the sprite frames
        AnimationCurve scale = new AnimationCurve(new Keyframe(0f, 1.1f), new Keyframe(0.3f, 0.65f), new Keyframe(0.6f, 1.1f));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale.x"), scale);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale.y"), scale);
        AnimationCurve alpha = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.3f, 0.3f), new Keyframe(0.6f, 1f));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_Color.a"), alpha);
        SetLooping(clip, true);
        EditorUtility.SetDirty(clip);

        return CreateCyclingController(AnimDir + "/Pellets/PowerPelletAnimator.controller",
            new List<(string, AnimationClip)> { ("Flash", clip) });
    }

    static AnimationClip CreateClip(string path, Sprite[] frames, float fps, bool loop,
        Color? colourA = null, Color? colourB = null, int colourSteps = 1)
    {
        AnimationClip clip = ReuseClip(path, fps);
        if (frames.Length == 0) warnings.Add($"No sprites found for {Path.GetFileNameWithoutExtension(path)}");
        else SetSpriteCurve(clip, frames, fps);

        if (colourA.HasValue)
        {
            float length = Mathf.Max(frames.Length, 1) / fps;
            string[] channels = { "r", "g", "b", "a" };
            for (int c = 0; c < 4; c++)
            {
                AnimationCurve curve = new AnimationCurve();
                for (int step = 0; step <= colourSteps; step++)
                {
                    Color colour = step % 2 == 0 ? colourA.Value : (colourB ?? colourA.Value);
                    curve.AddKey(length * step / colourSteps, colour[c]);
                }
                for (int k = 0; k < curve.length; k++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Constant);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_Color." + channels[c]), curve);
            }
        }

        SetLooping(clip, loop);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    // Rebuilds overwrite the existing asset in place so its GUID (and every reference to it) stays the same
    static AnimationClip ReuseClip(string path, float fps)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        else
        {
            clip.ClearCurves();
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
        }
        clip.frameRate = fps;
        return clip;
    }

    static void SetSpriteCurve(AnimationClip clip, Sprite[] frames, float fps)
    {
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        // Extra key so the last frame is shown for a full frame before looping
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[frames.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
    }

    static void SetLooping(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    // States are chained in order and the last one returns to the first.
    // Looping clips leave after 3 loops or 2 seconds (whichever is longer); non-looping clips after one play (exit time 1).
    static AnimatorController CreateCyclingController(string path, List<(string name, AnimationClip clip)> states)
    {
        AnimatorController controller = CreateController(path, states);
        AnimatorState[] created = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();

        if (created.Length > 1)
        {
            for (int i = 0; i < created.Length; i++)
            {
                AnimationClip clip = states[i].clip;
                bool loops = AnimationUtility.GetAnimationClipSettings(clip).loopTime;
                float length = Mathf.Max(clip.length, 0.01f);
                AnimatorStateTransition transition = created[i].AddTransition(created[(i + 1) % created.Length]);
                transition.hasExitTime = true;
                transition.exitTime = loops ? Mathf.Max(3f, 2f / length) : 1f;
                transition.hasFixedDuration = true;
                transition.duration = 0f;
            }
        }
        return controller;
    }

    // States only, no transitions: the first state plays by default and scripts switch with Animator.Play
    static AnimatorController CreateController(string path, List<(string name, AnimationClip clip)> states)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState old in machine.states) machine.RemoveState(old.state);
        for (int i = 0; i < states.Count; i++)
        {
            AnimatorState state = machine.AddState(states[i].name, new Vector3(300f, i * 70f, 0f));
            state.motion = states[i].clip;
        }
        machine.defaultState = machine.states[0].state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    // ---------------------------------------------------------------- Prefabs

    static GameObject BuildPelletPrefab(string name, Sprite sprite, RuntimeAnimatorController controller)
    {
        GameObject go = new GameObject(name);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 1;
        if (controller != null) go.AddComponent<Animator>().runtimeAnimatorController = controller;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ---------------------------------------------------------------- Scene

    static void BuildScene(RuntimeAnimatorController pacController, RuntimeAnimatorController[] ghostControllers,
        GameObject pelletPrefab, GameObject powerPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        camera.orthographic = true;
        camera.orthographicSize = LevelGenerator.Rows / 2f + 1f; // whole level plus a small border
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.02f, 0.07f, 0.16f);

        GameObject audioObject = new GameObject("BackgroundMusic");
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.volume = 0.6f;
        audioObject.AddComponent<BackgroundMusicController>();

        // ---- Level: Grid > Walls tilemap, Pellets, PowerPellets
        GameObject level = new GameObject("Level");
        level.AddComponent<Grid>();
        level.AddComponent<LevelGenerator>();
        // Grid origin chosen so cell (col, -row) is centred on LevelGenerator.CellToWorld(col, row)
        level.transform.position = new Vector3(-LevelGenerator.Cols / 2f, (LevelGenerator.Rows - 1) / 2f - 0.5f, 0f);

        GameObject wallsObject = new GameObject("Walls");
        wallsObject.transform.SetParent(level.transform, false);
        Tilemap walls = wallsObject.AddComponent<Tilemap>();
        wallsObject.AddComponent<TilemapRenderer>();
        GameObject pellets = Child(level.transform, "Pellets");
        GameObject powerPellets = Child(level.transform, "PowerPellets");

        Dictionary<int, TileBase> tiles = new Dictionary<int, TileBase>
        {
            { 1, LoadTile("Tile_OutsideCorner") },
            { 2, LoadTile("Tile_OutsideWall") },
            { 3, LoadTile("Tile_InsideCorner") },
            { 4, LoadTile("Tile_InsideWall") },
            { 7, LoadTile("Tile_TJunction") },
            { 8, LoadTile("Tile_SharkCage") },
        };

        for (int row = 0; row < LevelGenerator.Rows; row++)
        {
            for (int col = 0; col < LevelGenerator.Cols; col++)
            {
                Vector2Int cell = new Vector2Int(col, row);
                int tile = LevelGenerator.TileAt(cell);
                if (tiles.TryGetValue(tile, out TileBase tileAsset))
                {
                    Vector3Int position = new Vector3Int(col, -row, 0);
                    walls.SetTile(position, tileAsset);
                    walls.SetTileFlags(position, TileFlags.None);
                    walls.SetTransformMatrix(position, Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, LevelGenerator.GetWallRotation(cell))));
                }
                else if (tile == 5 || tile == 6)
                {
                    GameObject prefab = tile == 6 ? powerPrefab : pelletPrefab;
                    Transform parent = tile == 6 ? powerPellets.transform : pellets.transform;
                    GameObject pellet = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    pellet.transform.position = LevelGenerator.CellToWorld(cell);
                    pellet.name = $"{prefab.name}_{col}_{row}";
                }
            }
        }

        // ---- Characters: PacStudent top-left, sharks in a line inside the cage
        GameObject characters = new GameObject("Characters");
        GameObject pac = SpriteObject(characters.transform, "PacStudent", LoadSprite(SpritesDir + "/PacStudent/PacStudent_Right_01.png"), 5);
        pac.transform.position = LevelGenerator.CellToWorld(new Vector2Int(1, 1));
        pac.AddComponent<Animator>().runtimeAnimatorController = pacController;

        GameObject ghosts = Child(characters.transform, "Ghosts");
        for (int i = 0; i < SharkNames.Length; i++)
        {
            GameObject ghost = SpriteObject(ghosts.transform, $"Ghost{i + 1}_{SharkNames[i]}",
                LoadSprite($"{SpritesDir}/Ghosts/Shark_{SharkNames[i]}_Right_01.png"), 4);
            ghost.transform.position = LevelGenerator.CellToWorld(new Vector2Int(12 + i, 14));
            ghost.AddComponent<Animator>().runtimeAnimatorController = ghostControllers[i];
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static GameObject Child(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject SpriteObject(Transform parent, string name, Sprite sprite, int sortingOrder)
    {
        GameObject go = Child(parent, name);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return go;
    }

    // ---------------------------------------------------------------- Helpers

    static Sprite[] FirstFound(params string[] prefixes)
    {
        foreach (string prefix in prefixes)
        {
            Sprite[] frames = LoadFrames(prefix);
            if (frames.Length > 0) return frames;
        }
        return new Sprite[0];
    }

    static TileBase LoadTile(string name) => AssetDatabase.LoadAssetAtPath<TileBase>($"{TilesDir}/{name}.asset");
}
