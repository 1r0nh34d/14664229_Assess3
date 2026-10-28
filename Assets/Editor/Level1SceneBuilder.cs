using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// PacStudent > Build Level1 Scene creates the animation clips, Animator Controllers, pellet prefabs and the
// Level1 scene (full level layout, characters, an example of every sprite and the background music).
// Re-running it rebuilds Level1 from scratch. PacStudent > Assign Audio Clips only re-links the audio.
public static class Level1SceneBuilder
{
    const string SpritesDir = "Assets/Sprites";
    const string TilesDir = "Assets/Tiles";
    const string AnimDir = "Assets/Animation";
    const string PrefabDir = "Assets/Prefabs";
    const string AudioDir = "Assets/Audio Clips";
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

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
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

    // Every Animator cycles through all of its states so each one can be previewed in Play mode
    static RuntimeAnimatorController BuildPacStudentAnimator()
    {
        string dir = AnimDir + "/PacStudent/";
        string sprites = SpritesDir + "/PacStudent/PacStudent_";
        List<(string, AnimationClip)> states = new List<(string, AnimationClip)>();
        foreach (string direction in Directions)
            states.Add(("Walk_" + direction, CreateClip($"{dir}PacStudent_Walk_{direction}.anim", LoadFrames(sprites + direction + "_"), 10f, true)));
        states.Add(("Dead", CreateClip(dir + "PacStudent_Dead.anim", LoadFrames(sprites + "Dead_"), 3f, false)));
        return CreateCyclingController(dir + "PacStudentAnimator.controller", states);
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

        return CreateCyclingController($"{AnimDir}/Ghosts/GhostAnimator_{shark}.controller", states);
    }

    static RuntimeAnimatorController BuildPowerPelletAnimator()
    {
        string path = AnimDir + "/Pellets/PowerPellet_Flash.anim";
        AssetDatabase.DeleteAsset(path);
        AnimationClip clip = new AnimationClip { frameRate = 30f };

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
        AssetDatabase.CreateAsset(clip, path);

        return CreateCyclingController(AnimDir + "/Pellets/PowerPelletAnimator.controller",
            new List<(string, AnimationClip)> { ("Flash", clip) });
    }

    static AnimationClip CreateClip(string path, Sprite[] frames, float fps, bool loop,
        Color? colourA = null, Color? colourB = null, int colourSteps = 1)
    {
        AssetDatabase.DeleteAsset(path);
        AnimationClip clip = new AnimationClip { frameRate = fps };
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
        AssetDatabase.CreateAsset(clip, path);
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
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        List<AnimatorState> created = new List<AnimatorState>();
        for (int i = 0; i < states.Count; i++)
        {
            AnimatorState state = machine.AddState(states[i].name, new Vector3(300f, i * 70f, 0f));
            state.motion = states[i].clip;
            created.Add(state);
        }
        machine.defaultState = created[0];

        if (created.Count > 1)
        {
            for (int i = 0; i < created.Count; i++)
            {
                AnimationClip clip = states[i].clip;
                bool loops = AnimationUtility.GetAnimationClipSettings(clip).loopTime;
                float length = Mathf.Max(clip.length, 0.01f);
                AnimatorStateTransition transition = created[i].AddTransition(created[(i + 1) % created.Count]);
                transition.hasExitTime = true;
                transition.exitTime = loops ? Mathf.Max(3f, 2f / length) : 1f;
                transition.hasFixedDuration = true;
                transition.duration = 0f;
            }
        }
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
        camera.orthographicSize = LevelLayout.Rows / 2f + 1f; // whole level plus a small border
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
        // Grid origin chosen so cell (col, -row) is centred on LevelLayout.CellToWorld(col, row)
        level.transform.position = new Vector3(-LevelLayout.Cols / 2f, (LevelLayout.Rows - 1) / 2f - 0.5f, 0f);

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

        for (int row = 0; row < LevelLayout.Rows; row++)
        {
            for (int col = 0; col < LevelLayout.Cols; col++)
            {
                Vector2Int cell = new Vector2Int(col, row);
                int tile = LevelLayout.TileAt(cell);
                if (tiles.TryGetValue(tile, out TileBase tileAsset))
                {
                    Vector3Int position = new Vector3Int(col, -row, 0);
                    walls.SetTile(position, tileAsset);
                    walls.SetTileFlags(position, TileFlags.None);
                    walls.SetTransformMatrix(position, Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, LevelLayout.GetWallRotation(cell))));
                }
                else if (tile == 5 || tile == 6)
                {
                    GameObject prefab = tile == 6 ? powerPrefab : pelletPrefab;
                    Transform parent = tile == 6 ? powerPellets.transform : pellets.transform;
                    GameObject pellet = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    pellet.transform.position = LevelLayout.CellToWorld(cell);
                    pellet.name = $"{prefab.name}_{col}_{row}";
                }
            }
        }

        // ---- Characters: PacStudent top-left, sharks in a line inside the cage
        GameObject characters = new GameObject("Characters");
        GameObject pac = SpriteObject(characters.transform, "PacStudent", LoadSprite(SpritesDir + "/PacStudent/PacStudent_Right_01.png"), 5);
        pac.transform.position = LevelLayout.CellToWorld(new Vector2Int(1, 1));
        pac.AddComponent<Animator>().runtimeAnimatorController = pacController;

        GameObject ghosts = Child(characters.transform, "Ghosts");
        for (int i = 0; i < SharkNames.Length; i++)
        {
            GameObject ghost = SpriteObject(ghosts.transform, $"Ghost{i + 1}_{SharkNames[i]}",
                LoadSprite($"{SpritesDir}/Ghosts/Shark_{SharkNames[i]}_Right_01.png"), 4);
            ghost.transform.position = LevelLayout.CellToWorld(new Vector2Int(12 + i, 14));
            ghost.AddComponent<Animator>().runtimeAnimatorController = ghostControllers[i];
        }

        // ---- Items that are not part of the level layout, shown beside it
        GameObject items = new GameObject("ItemShowcase");
        float x = -LevelLayout.Cols / 2f - 3f;
        Sprite cherrySprite = AssetDatabase.FindAssets("Cherry t:Sprite", new[] { SpritesDir })
            .Select(guid => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid)))
            .FirstOrDefault();
        GameObject cherry = SpriteObject(items.transform, "BonusCherry", cherrySprite, 5);
        cherry.transform.position = new Vector3(x, 2f, 0f);
        if (cherrySprite == null)
        {
            warnings.Add("No bonus cherry sprite found (add Assets/Sprites/Items/Cherry.png and rebuild)");
            cherry.GetComponent<SpriteRenderer>().sprite = LoadSprite(SpritesDir + "/Items/PowerPellet_01.png");
            cherry.GetComponent<SpriteRenderer>().color = new Color(1f, 0.35f, 0.45f);
        }
        GameObject lives = Child(items.transform, "LifeIndicators");
        for (int i = 0; i < 3; i++)
        {
            GameObject life = SpriteObject(lives.transform, "Life" + (i + 1), LoadSprite(SpritesDir + "/Items/LifeIcon.png"), 5);
            life.transform.position = new Vector3(x - 1f + i, -1f, 0f);
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

    static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    static Sprite[] LoadFrames(string prefix)
    {
        List<Sprite> frames = new List<Sprite>();
        for (int i = 1; i < 20; i++)
        {
            Sprite sprite = LoadSprite($"{prefix}{i:00}.png");
            if (sprite == null) break;
            frames.Add(sprite);
        }
        return frames.ToArray();
    }

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

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

// The A3 levelMap mirrored into the full level, plus the neighbour checks used to rotate each wall piece.
// Cells are Vector2Int(x = column, y = row) with row 0 at the top.
public static class LevelLayout
{
    // 0 empty, 1 outside corner, 2 outside wall, 3 inside corner, 4 inside wall,
    // 5 pellet, 6 power pellet, 7 T junction, 8 ghost exit wall
    static readonly int[,] levelMap =
    {
        {1,2,2,2,2,2,2,2,2,2,2,2,2,7},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,4},
        {2,6,4,0,0,4,5,4,0,0,0,4,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,3},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,5},
        {2,5,3,4,4,3,5,3,3,5,3,4,4,4},
        {2,5,3,4,4,3,5,4,4,5,3,4,4,3},
        {2,5,5,5,5,5,5,4,4,5,5,5,5,4},
        {1,2,2,2,2,1,5,4,3,4,4,3,0,4},
        {0,0,0,0,0,2,5,4,3,4,4,3,0,3},
        {0,0,0,0,0,2,5,4,4,0,0,0,0,0},
        {0,0,0,0,0,2,5,4,4,0,3,4,4,8},
        {2,2,2,2,2,1,5,3,3,0,4,0,0,0},
        {0,0,0,0,0,0,5,0,0,0,4,0,0,0},
    };

    static readonly Vector2Int Up = new Vector2Int(0, -1);
    static readonly Vector2Int Down = new Vector2Int(0, 1);
    static readonly Vector2Int Left = new Vector2Int(-1, 0);
    static readonly Vector2Int Right = new Vector2Int(1, 0);

    // Ordered so that rotating a piece +90 degrees maps each direction to the next one
    static readonly Vector2Int[] RotationOrder = { Left, Down, Right, Up };

    static int[,] fullMap;

    static int[,] FullMap => fullMap ?? (fullMap = BuildFullMap());
    public static int Rows => FullMap.GetLength(0);
    public static int Cols => FullMap.GetLength(1);

    // Mirror horizontally, then vertically without repeating the quadrant's bottom row
    static int[,] BuildFullMap()
    {
        int quadRows = levelMap.GetLength(0);
        int quadCols = levelMap.GetLength(1);
        int rows = quadRows * 2 - 1;
        int cols = quadCols * 2;
        int[,] map = new int[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                map[r, c] = levelMap[r < quadRows ? r : rows - 1 - r, c < quadCols ? c : cols - 1 - c];
        return map;
    }

    public static int TileAt(Vector2Int cell) =>
        cell.x >= 0 && cell.x < Cols && cell.y >= 0 && cell.y < Rows ? FullMap[cell.y, cell.x] : -1;

    public static Vector3 CellToWorld(Vector2Int cell) =>
        new Vector3(cell.x - (Cols - 1) / 2f, (Rows - 1) / 2f - cell.y, 0f);

    static bool IsWall(Vector2Int cell)
    {
        int tile = TileAt(cell);
        return (tile >= 1 && tile <= 4) || tile == 7 || tile == 8;
    }

    static bool IsStraight(int tile) => tile == 2 || tile == 4 || tile == 8;

    static bool StraightIsHorizontal(Vector2Int cell)
    {
        int horizontal = (IsWall(cell + Left) ? 1 : 0) + (IsWall(cell + Right) ? 1 : 0);
        int vertical = (IsWall(cell + Up) ? 1 : 0) + (IsWall(cell + Down) ? 1 : 0);
        return horizontal >= vertical;
    }

    // How strongly the wall next to cell (in direction dir) joins onto it
    static int ConnectScore(Vector2Int cell, Vector2Int dir)
    {
        Vector2Int neighbour = cell + dir;
        if (!IsWall(neighbour)) return 0;
        if (IsStraight(TileAt(neighbour))) return StraightIsHorizontal(neighbour) == (dir.y == 0) ? 2 : 0;
        return 1;
    }

    // Base sprites: corners join Left+Down, the T junction stem points Down,
    // outside/ghost walls are horizontal and inside walls are vertical
    public static float GetWallRotation(Vector2Int cell)
    {
        switch (TileAt(cell))
        {
            case 2:
            case 8:
                return StraightIsHorizontal(cell) ? 0f : 90f;
            case 4:
                return StraightIsHorizontal(cell) ? 90f : 0f;
            case 1:
            case 3:
            {
                int best = 0, bestScore = -1;
                for (int k = 0; k < 4; k++)
                {
                    int a = ConnectScore(cell, RotationOrder[k]);
                    int b = ConnectScore(cell, RotationOrder[(k + 1) % 4]);
                    if (a == 0 || b == 0) continue;
                    if (a + b > bestScore) { bestScore = a + b; best = k; }
                }
                return best * 90f;
            }
            case 7:
                for (int k = 0; k < 4; k++)
                {
                    Vector2Int dir = RotationOrder[k];
                    if (ConnectScore(cell, dir) > 0 && ConnectScore(cell, -dir) == 0) return (k - 1) * 90f;
                }
                return 0f;
        }
        return 0f;
    }
}
